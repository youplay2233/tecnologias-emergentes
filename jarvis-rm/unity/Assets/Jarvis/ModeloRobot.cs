using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using GLTFast;
using UnityEngine;

// Un robot cargado desde la laptop (GLB con una pieza por nodo, ver jarvis/modelos/convertir.py).
// Jerarquía:  ModeloRobot (se puede agarrar con la mano)
//               ├─ Pedestal (anillos holográficos)
//               └─ Pivote  (giro y escala; está en el centro del robot)
//                    └─ Contenido (desplazado para que el centro del robot quede en el pivote)
//                         └─ escena glTF  ->  piezas (nodo "link3") con sus mallas ("link3__0", ...)
public class ModeloRobot : MonoBehaviour
{
    public float altoInicial = 0.55f;       // la dimensión mayor del robot se muestra con este tamaño (m)
    public float alturaPedestal = 0.02f;
    public float velocidad = 4f;            // qué tan rápido se acercan las animaciones a su objetivo

    // Los robots de MuJoCo miran hacia +X; así quedan de frente al usuario
    private static readonly Quaternion Frente = Quaternion.Euler(0f, 90f, 0f);

    private class Pieza
    {
        public string nombre;
        public Transform t;
        public Renderer[] renderers;
        public Material[][] originales;
        public Vector3 casa;
        public Vector3 direccion;
        public float abajo;        // y más baja de la pieza, relativa al centro del robot (espacio del Contenido)
        public float subeBaja;     // cuánto se mueve en y por unidad de despiece (espacio del Contenido)
        public bool visible = true;
        public bool resaltada;
        public Rigidbody cuerpo;      // para tomarla con la mano cuando el robot está despiezado
        public BoxCollider caja;
        public Vector3 libre;         // cuánto la movió el usuario desde su lugar en el despiece
        public Quaternion rotCasa;
        public bool volviendo;        // regresando a su lugar (al armar o volver a despiezar)
        public float tamanoMayor;     // dimensión mayor de la pieza en su propio espacio (sin escalar)
        public bool inspeccionada;    // frente al usuario, en grande y girando
        public bool enVuelo;          // lanzada: la mueve la física hasta que encesta o falla
        public bool desechada;        // la tiraron a la basura: oculta hasta que se vuelva a armar
        public bool suelta;           // el usuario la movió: conserva su giro hasta que regrese a su lugar
        public bool etiquetada;       // con su nombre a la vista, sin cambiar de color
        public Pieza padre;           // cadena cinemática (de quién cuelga)
        public readonly List<Articulacion> juntas = new List<Articulacion>();
        public Quaternion poseQ = Quaternion.identity;
        public Vector3 poseT;
        public Vector3 posePos;       // dónde queda su centro con las articulaciones movidas
        public Quaternion poseRot = Quaternion.identity;
        public TextMesh etiqueta;
        public LineRenderer linea;
    }

    private class Articulacion
    {
        public string nombre;
        public bool lineal;
        public Vector3 ancla, eje;   // en el espacio de la escena glTF, con el robot en reposo
        public float min = float.NegativeInfinity, max = float.PositiveInfinity, reposo, valor, objetivo;
    }

    [Serializable] private class InfoModelo { public InfoPieza[] piezas; public InfoArticulacion[] articulaciones; }
    [Serializable] private class InfoPieza { public string nombre; public string padre; }
    [Serializable] private class InfoArticulacion { public string nombre; public string tipo; public string pieza; public string marcador; public float[] rango; public float reposo; }

    // glTFast refleja el eje X al pasar de glTF (mano derecha) a Unity (mano izquierda): los giros cambian de sentido
    private const float SentidoDeGiro = -1f;

    private readonly List<Articulacion> articulaciones = new List<Articulacion>();
    private readonly Dictionary<string, Articulacion> porArticulacion = new Dictionary<string, Articulacion>();
    private List<Pieza> ordenCinematico = new List<Pieza>();
    private string animacion = "";
    private Quaternion? orientacionExterna;   // giro en el mundo que dicta otro (la mano espejo)
    private float inicioAnimacion;
    public bool TieneArticulaciones { get { return articulaciones.Count > 0; } }

    public string Clave { get; private set; }
    public string Nombre { get; private set; }
    public bool Cargado { get { return contenido != null; } }
    public bool Cargando { get; private set; }

    private Transform pivote, contenido, pedestal;
    private TextMesh letrero;
    private GltfImport importacion;
    private readonly List<Pieza> piezas = new List<Pieza>();
    private readonly Dictionary<string, Pieza> porNombre = new Dictionary<string, Pieza>();
    private Vector3 tamano;                  // del robot, en el espacio del Contenido (sin escalar)
    private float despiece, despieceObjetivo;
    private float escala = 1f, escalaObjetivo = 1f;
    private Quaternion giroObjetivo = Quaternion.identity;
    private BoxCollider caja;
    private float aparicion;                 // 0 -> 1 al cargar: el robot "se materializa"

    void Awake()
    {
        Rigidbody cuerpo = gameObject.AddComponent<Rigidbody>();
        cuerpo.isKinematic = true;   // ManoRM lo puede tomar con el botón de agarre; al soltarlo se queda flotando
        cuerpo.useGravity = false;
        caja = gameObject.AddComponent<BoxCollider>();
        caja.enabled = false;

        pivote = new GameObject("Pivote").transform;
        pivote.SetParent(transform, false);
        CrearPedestal();
    }

    // ------------------------------------------------------------------ carga

    public async Task<string> Cargar(string clave, string nombre, string url)
    {
        if (Cargando) return "Ya estoy cargando otro modelo.";
        Cargando = true;
        try
        {
            Cerrar();
            letrero.text = "CARGANDO  " + nombre.ToUpperInvariant() + "...";
            GltfImport gltf = new GltfImport(materialGenerator: new MaterialesRobot());
            if (!await gltf.Load(url))
            {
                gltf.Dispose();
                letrero.text = "";
                return "No pude descargar el modelo desde " + url + ".";
            }

            Transform c = new GameObject("Contenido").transform;
            c.SetParent(pivote, false);
            if (!await gltf.InstantiateMainSceneAsync(c))
            {
                Destroy(c.gameObject);
                gltf.Dispose();
                letrero.text = "";
                return "Descargué el modelo pero no pude armarlo en la escena.";
            }

            importacion = gltf;
            contenido = c;
            Clave = clave;
            Nombre = nombre;
            Preparar();
            string cinematica = await CargarCinematica(url);
            letrero.text = nombre.ToUpperInvariant() + "\n<size=60>" + piezas.Count + " PIEZAS</size>";
            Holo.Sonido("abrir", transform.position);
            return nombre + " cargado frente al usuario, con " + piezas.Count + " piezas" + cinematica + ".";
        }
        finally
        {
            Cargando = false;
        }
    }

    private void Preparar()
    {
        pivote.localRotation = Frente;
        pivote.localScale = Vector3.one;
        giroObjetivo = Frente;

        Dictionary<Pieza, Vector3> explicitas = new Dictionary<Pieza, Vector3>();

        // Piezas: los nodos cuyas mallas se llaman "<pieza>__<n>"
        foreach (Transform t in contenido.GetComponentsInChildren<Transform>(true))
        {
            if (t == contenido || t.name.Contains("__")) continue;
            string prefijo = t.name + "__";
            Renderer[] rs = t.GetComponentsInChildren<Renderer>(true).Where(r => r.name.StartsWith(prefijo, StringComparison.Ordinal)).ToArray();
            if (rs.Length == 0) continue;
            Pieza p = new Pieza { nombre = t.name, t = t, renderers = rs, casa = t.localPosition };
            Transform marca = t.Find(t.name + "__despiece");   // dirección de despiece explícita (ver convertir.py)
            if (marca != null) explicitas[p] = marca.localPosition;
            p.originales = rs.Select(r => r.sharedMaterials).ToArray();
            piezas.Add(p);
            porNombre[p.nombre] = p;
        }

        // Caja del robot y centro de cada pieza, en el espacio del Contenido
        Bounds total = new Bounds();
        bool primera = true;
        Dictionary<Pieza, Vector3> centros = new Dictionary<Pieza, Vector3>();
        foreach (Pieza p in piezas)
        {
            Bounds b = Caja(p.renderers);
            centros[p] = b.center;
            if (primera) { total = b; primera = false; }
            else total.Encapsulate(b);
        }
        tamano = total.size;
        contenido.localPosition = -total.center;
        foreach (Pieza p in piezas)
        {
            Bounds b = Caja(p.renderers);
            p.abajo = b.min.y - total.center.y;
            // Se mueve en el espacio de su padre (la escena glTF), así que la dirección se convierte a ese espacio
            Vector3 explicita;
            p.direccion = explicitas.TryGetValue(p, out explicita)
                ? explicita
                : p.t.parent.InverseTransformVector(contenido.TransformVector(centros[p] - total.center));
            p.subeBaja = contenido.InverseTransformVector(p.t.parent.TransformVector(p.direccion)).y;

            // El nodo de la pieza se pasa a su centro (las mallas se compensan): así gira y se agranda
            // sobre sí misma cuando se toma con la mano o se inspecciona
            Vector3 centroLocal = p.t.parent.InverseTransformPoint(contenido.TransformPoint(centros[p]));
            Vector3 delta = centroLocal - p.t.localPosition;
            p.t.localPosition += delta;
            foreach (Transform hijo in p.t) hijo.localPosition -= delta;
            p.casa = p.t.localPosition;
        }

        foreach (Pieza p in piezas)
        {
            p.rotCasa = p.t.localRotation;
            p.t.gameObject.AddComponent<PiezaRM>().nombre = p.nombre;
            p.cuerpo = p.t.gameObject.AddComponent<Rigidbody>();
            p.cuerpo.isKinematic = true;
            p.cuerpo.useGravity = false;
            Bounds local = Caja(p.renderers, p.t);
            p.caja = p.t.gameObject.AddComponent<BoxCollider>();
            p.caja.center = local.center;
            p.caja.size = Vector3.Max(local.size, Vector3.one * 0.01f);
            p.caja.enabled = false;
            p.tamanoMayor = Mathf.Max(local.size.x, Mathf.Max(local.size.y, local.size.z));
            p.posePos = p.casa;          // sin articulaciones movidas, la pose es la de reposo
            p.poseRot = p.rotCasa;
            p.t.GetComponent<PiezaRM>().modelo = this;
        }

        ordenCinematico = new List<Pieza>(piezas);   // hasta que llegue el JSON con la cadena real

        float mayor = Mathf.Max(tamano.x, Mathf.Max(tamano.y, tamano.z));
        escala = escalaObjetivo = mayor > 0.0001f ? altoInicial / mayor : 1f;
        despiece = despieceObjetivo = 0f;
        aparicion = 0f;
        caja.enabled = true;
        Aplicar(1f);
    }

    // Caja de un grupo de renderers en el espacio del Contenido (con las 8 esquinas de cada caja del mundo)
    private Bounds Caja(Renderer[] rs)
    {
        return Caja(rs, contenido);
    }

    private static Bounds Caja(Renderer[] rs, Transform espacio)
    {
        Bounds b = new Bounds();
        bool primera = true;
        foreach (Renderer r in rs)
        {
            Bounds w = r.bounds;
            for (int i = 0; i < 8; i++)
            {
                Vector3 esquina = w.center + Vector3.Scale(w.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                Vector3 local = espacio.InverseTransformPoint(esquina);
                if (primera) { b = new Bounds(local, Vector3.zero); primera = false; }
                else b.Encapsulate(local);
            }
        }
        return b;
    }

    // ------------------------------------------------------------------ cinemática (movimiento de articulaciones)

    private async Task<string> CargarCinematica(string urlGlb)
    {
        articulaciones.Clear();
        porArticulacion.Clear();
        animacion = "";
        int q = urlGlb.IndexOf('?');
        string url = q < 0 ? urlGlb.Replace(".glb", ".json") : urlGlb.Substring(0, q).Replace(".glb", ".json") + urlGlb.Substring(q);
        InfoModelo info = null;
        using (UnityEngine.Networking.UnityWebRequest web = UnityEngine.Networking.UnityWebRequest.Get(url))
        {
            var operacion = web.SendWebRequest();
            while (!operacion.isDone) await Task.Yield();
            if (web.result == UnityEngine.Networking.UnityWebRequest.Result.Success) info = JsonUtility.FromJson<InfoModelo>(web.downloadHandler.text);
        }
        if (info == null || contenido == null) return "";

        Pieza p, padre;
        foreach (InfoPieza ip in info.piezas ?? new InfoPieza[0])
        {
            if (porNombre.TryGetValue(ip.nombre, out p) && !string.IsNullOrEmpty(ip.padre) && porNombre.TryGetValue(ip.padre, out padre)) p.padre = padre;
        }
        foreach (InfoArticulacion ia in info.articulaciones ?? new InfoArticulacion[0])
        {
            if (string.IsNullOrEmpty(ia.marcador) || !porNombre.TryGetValue(ia.pieza, out p)) continue;
            Transform marca = Buscar(p.t, ia.marcador);
            Transform punta = marca != null ? Buscar(marca, ia.marcador + "__punta") : null;
            if (punta == null) continue;
            Articulacion a = new Articulacion { nombre = ia.nombre, lineal = ia.tipo == "lineal", reposo = ia.reposo };
            a.ancla = p.t.parent.InverseTransformPoint(marca.position);
            a.eje = (p.t.parent.InverseTransformPoint(punta.position) - a.ancla).normalized;
            if (ia.rango != null && ia.rango.Length == 2) { a.min = ia.rango[0]; a.max = ia.rango[1]; }
            a.valor = a.objetivo = a.reposo;
            p.juntas.Add(a);
            articulaciones.Add(a);
            porArticulacion[a.nombre] = a;
        }
        // Orden de la cadena: cada pieza después de la pieza de la que cuelga
        ordenCinematico = piezas.OrderBy(Profundidad).ToList();
        return articulaciones.Count > 0 ? " y " + articulaciones.Count + " articulaciones que se pueden mover" : "";
    }

    private static int Profundidad(Pieza p)
    {
        int d = 0;
        for (Pieza x = p.padre; x != null && d < 64; x = x.padre) d++;
        return d;
    }

    private static Transform Buscar(Transform raiz, string nombre)
    {
        foreach (Transform t in raiz.GetComponentsInChildren<Transform>(true)) if (t.name == nombre) return t;
        return null;
    }

    // Grados (giro) o centímetros (lineal), en valor absoluto como en el JSON del modelo
    public string MoverArticulacion(string nombre, float cantidad)
    {
        Articulacion a;
        if (!Cargado || !porArticulacion.TryGetValue(nombre, out a)) return "No existe la articulación " + nombre + ".";
        animacion = "";
        float valor = a.lineal ? cantidad / 100f : cantidad * Mathf.Deg2Rad;
        a.objetivo = Mathf.Clamp(valor, a.min, a.max);
        bool limitado = !Mathf.Approximately(a.objetivo, valor);
        float mostrado = a.lineal ? a.objetivo * 100f : a.objetivo * Mathf.Rad2Deg;
        return nombre + " a " + mostrado.ToString("0", System.Globalization.CultureInfo.InvariantCulture) + (a.lineal ? " cm" : " grados")
               + (limitado ? " (era su límite)" : "") + ".";
    }

    // Para la mano espejo: fija el ángulo (radianes) cada cuadro, sin animación
    public bool FijarArticulacion(string nombre, float radianes)
    {
        Articulacion a;
        if (!porArticulacion.TryGetValue(nombre, out a)) return false;
        a.objetivo = Mathf.Clamp(radianes, a.min, a.max);
        return true;
    }

    // La mano espejo dicta el giro del modelo (en el mundo); null lo devuelve a su vista normal
    public Quaternion GiroActual { get { return pivote.rotation; } }

    public void OrientarDesde(Quaternion? mundo)
    {
        if (!mundo.HasValue && orientacionExterna.HasValue) giroObjetivo = pivote.localRotation;   // se queda como estaba
        orientacionExterna = mundo;
    }

    public void DetenerAnimacion()
    {
        animacion = "";
    }

    public string Animar(string modo)
    {
        if (!Cargado) return "No hay modelo abierto.";
        if (articulaciones.Count == 0) return Nombre + " no tiene articulaciones que se puedan mover.";
        switch (modo)
        {
            case "demostracion":
            case "caminar":
                animacion = modo;
                inicioAnimacion = Time.time;
                return modo == "caminar" ? Nombre + " caminando en su lugar." : "Demostración de las articulaciones de " + Nombre + ".";
            default:
                animacion = "";
                foreach (Articulacion a in articulaciones) a.objetivo = a.reposo;
                return Nombre + " en su postura de reposo.";
        }
    }

    private void AnimarArticulaciones()
    {
        float t = Time.time - inicioAnimacion;
        if (animacion == "demostracion")
        {
            for (int i = 0; i < articulaciones.Count; i++)
            {
                Articulacion a = articulaciones[i];
                float rango = float.IsInfinity(a.max - a.min) ? (a.lineal ? 0.05f : 2f) : a.max - a.min;
                float amplitud = Mathf.Min(rango * 0.4f, a.lineal ? 0.04f : 0.7f);
                float centro = Mathf.Clamp(a.reposo, a.min + amplitud, a.max - amplitud);
                a.objetivo = Mathf.Clamp(centro + amplitud * Mathf.Sin(t * 1.3f + i * 0.8f), a.min, a.max);
            }
        }
        else if (animacion == "caminar")
        {
            const float w = 5f;
            foreach (Articulacion a in articulaciones)
            {
                string n = a.nombre.ToLowerInvariant();
                float fase = FaseDePata(n);
                if (float.IsNaN(fase)) { a.objetivo = a.reposo; continue; }
                float s = Mathf.Sin(t * w + fase);
                float hacia = (a.max - a.reposo) > (a.reposo - a.min) ? 1f : -1f;   // hacia dónde se dobla más
                if (n.Contains("_hy") || n.Contains("thigh") || n.Contains("hip_pitch") || n.Contains("shoulder_pitch"))
                    a.objetivo = a.reposo + 0.35f * s;
                else if (n.Contains("_kn") || n.Contains("calf") || n.Contains("knee") || n.Contains("elbow"))
                    a.objetivo = a.reposo + hacia * 0.55f * Mathf.Max(0f, s);
                else
                    a.objetivo = a.reposo;
                a.objetivo = Mathf.Clamp(a.objetivo, a.min, a.max);
            }
        }
    }

    // Trote: patas en diagonal van juntas; en el humanoide, cada brazo va contra su pierna
    private static float FaseDePata(string n)
    {
        bool brazo = n.Contains("shoulder") || n.Contains("elbow");
        if (n.StartsWith("fl") || n.StartsWith("hr") || n.StartsWith("rr")) return 0f;
        if (n.StartsWith("fr") || n.StartsWith("hl") || n.StartsWith("rl")) return Mathf.PI;
        if (n.StartsWith("left")) return brazo ? Mathf.PI : 0f;
        if (n.StartsWith("right")) return brazo ? 0f : Mathf.PI;
        return float.NaN;
    }

    private void CalcularPose(float k)
    {
        foreach (Articulacion a in articulaciones) a.valor = Mathf.Lerp(a.valor, a.objetivo, k);
        foreach (Pieza p in ordenCinematico)
        {
            Quaternion q = p.padre != null ? p.padre.poseQ : Quaternion.identity;
            Vector3 t = p.padre != null ? p.padre.poseT : Vector3.zero;
            foreach (Articulacion a in p.juntas)
            {
                float d = a.valor - a.reposo;
                if (a.lineal)
                {
                    t += q * (a.eje * d);
                }
                else
                {
                    Quaternion giro = Quaternion.AngleAxis(SentidoDeGiro * d * Mathf.Rad2Deg, a.eje);
                    t = q * (a.ancla - giro * a.ancla) + t;
                    q = q * giro;
                }
            }
            p.poseQ = q;
            p.poseT = t;
            p.posePos = q * p.casa + t;
            p.poseRot = q * p.rotCasa;
        }
    }

    public void Cerrar()
    {
        orientacionExterna = null;   // un modelo nuevo no hereda el giro de la mano espejo
        articulaciones.Clear();
        porArticulacion.Clear();
        ordenCinematico.Clear();
        animacion = "";
        foreach (Pieza p in piezas) QuitarEtiqueta(p);
        piezas.Clear();
        porNombre.Clear();
        if (contenido != null) Destroy(contenido.gameObject);
        contenido = null;
        if (importacion != null) importacion.Dispose();
        importacion = null;
        caja.enabled = false;
        Clave = Nombre = null;
        if (letrero != null) letrero.text = "";
    }

    // ------------------------------------------------------------------ órdenes

    public string Despiezar(float separacion)
    {
        if (!Cargado) return "No hay modelo abierto.";
        despieceObjetivo = Mathf.Clamp(separacion, 0f, 2.5f);
        Reacomodar();   // lo que el usuario movió, inspeccionó o tiró regresa a su lugar
        Holo.Sonido(separacion > despiece ? "abrir" : "cerrar", transform.position);
        return despieceObjetivo <= 0.01f ? Nombre + " armado." : Nombre + " despiezado (separación " + despieceObjetivo.ToString("0.#", System.Globalization.CultureInfo.InvariantCulture) + ").";
    }

    public string Girar(float giro, float inclinacion, string vista)
    {
        if (!Cargado) return "No hay modelo abierto.";
        giroObjetivo = Quaternion.Euler(inclinacion, giro, 0f) * Frente;
        Holo.Sonido("blip", transform.position);
        return "Mostrando la vista de " + (string.IsNullOrEmpty(vista) ? "otro lado" : vista) + ".";
    }

    public string Escalar(float alto)
    {
        if (!Cargado || tamano.y <= 0.0001f) return "No hay modelo abierto.";
        escalaObjetivo = Mathf.Clamp(alto, 0.1f, 2.5f) / tamano.y;
        return "Ahora mide " + Mathf.RoundToInt(alto * 100f) + " cm de alto.";
    }

    public string Piezas(string accion, string[] nombres)
    {
        if (!Cargado) return "No hay modelo abierto.";
        List<Pieza> elegidas = new List<Pieza>();
        List<string> faltan = new List<string>();
        foreach (string n in nombres ?? new string[0])
        {
            Pieza p;
            if (porNombre.TryGetValue(n, out p)) elegidas.Add(p);
            else faltan.Add(n);
        }
        if (elegidas.Count == 0) return "No encontré esas piezas: " + string.Join(", ", faltan);

        switch (accion)
        {
            case "aislar":
                foreach (Pieza p in piezas) Visible(p, elegidas.Contains(p));
                foreach (Pieza p in elegidas) Resaltar(p, true);
                break;
            case "ocultar":
                foreach (Pieza p in elegidas) Visible(p, false);
                break;
            case "mostrar":
                foreach (Pieza p in elegidas) Visible(p, true);
                break;
            case "resaltar":
                foreach (Pieza p in piezas) Resaltar(p, elegidas.Contains(p));
                break;
            case "etiquetar":   // solo el nombre, sin cambiar el color
                foreach (Pieza p in elegidas)
                {
                    p.etiquetada = true;
                    CrearEtiqueta(p);
                }
                break;
            default:
                return "Acción desconocida: " + accion;
        }
        Holo.Sonido("confirmar", transform.position);
        string hecho = accion + ": " + string.Join(", ", elegidas.Select(p => p.nombre));
        return faltan.Count > 0 ? hecho + ". No existen: " + string.Join(", ", faltan) : hecho + ".";
    }

    public Rigidbody CuerpoDe(string nombre)
    {
        Pieza p;
        return porNombre.TryGetValue(nombre, out p) ? p.cuerpo : null;
    }

    public string MostrarTodo()
    {
        if (!Cargado) return "No hay modelo abierto.";
        foreach (Pieza p in piezas)
        {
            Visible(p, true);
            Resaltar(p, false);
            p.etiquetada = false;
            QuitarEtiqueta(p);
        }
        Reacomodar();
        return "Todas las piezas visibles y en su lugar, sin resaltar.";
    }

    private void Reacomodar()
    {
        foreach (Pieza p in piezas)
        {
            if (p.enVuelo) continue;
            p.volviendo = true;
            p.inspeccionada = false;
            if (p.desechada)
            {
                p.desechada = false;
                p.t.localScale = Vector3.one * 0.01f;   // reaparece creciendo en su lugar
                foreach (Renderer r in p.renderers) r.enabled = p.visible;
            }
        }
    }

    // Trae una pieza frente al usuario, en grande y girando, para analizarla
    public string Inspeccionar(string nombre)
    {
        if (!Cargado) return "No hay modelo abierto.";
        Pieza p;
        if (!porNombre.TryGetValue(nombre, out p)) return "No existe la pieza " + nombre + ".";
        foreach (Pieza otra in piezas)
        {
            if (otra.inspeccionada && otra != p) { otra.inspeccionada = false; otra.volviendo = true; }
        }
        if (p.desechada) Reacomodar();
        Visible(p, true);
        p.inspeccionada = true;
        p.volviendo = false;
        Resaltar(p, false);
        Holo.Sonido("abrir", p.t.position);
        return p.nombre + " frente al usuario, en grande y girando.";
    }

    // Lanza piezas al bote de basura (con la voz); encesten o no, desaparecen hasta que se vuelva a armar
    public string Desechar(string[] nombres)
    {
        if (!Cargado) return "No hay modelo abierto.";
        if (BoteBasura.Instancia == null) return "No hay bote de basura en la escena.";
        int n = 0;
        foreach (string nombre in nombres ?? new string[0])
        {
            Pieza p;
            if (!porNombre.TryGetValue(nombre, out p) || p.desechada || p.enVuelo) continue;
            Lanzar(p, BoteBasura.Instancia.VelocidadHacia(p.t.position, 0.9f + 0.15f * n));
            n++;
        }
        return n == 0 ? "No encontré esas piezas." : n + " pieza(s) lanzada(s) al bote de basura.";
    }

    // La mano soltó una pieza con impulso (ManoRM) o la voz pidió tirarla
    public void Lanzar(PiezaRM pieza, Vector3 velocidad)
    {
        Pieza p;
        if (porNombre.TryGetValue(pieza.nombre, out p)) Lanzar(p, velocidad);
    }

    private void Lanzar(Pieza p, Vector3 velocidad)
    {
        p.enVuelo = true;
        p.inspeccionada = false;
        p.caja.enabled = true;
        ChocarConElRobot(p, false);   // al salir atraviesa a sus vecinas en lugar de frenarse con ellas
        Proyectil.Lanzar(p.cuerpo, velocidad, encesto =>
        {
            ChocarConElRobot(p, true);
            p.enVuelo = false;
            p.desechada = true;
            p.cuerpo.isKinematic = true;
            p.cuerpo.useGravity = false;
            foreach (Renderer r in p.renderers) r.enabled = false;
            p.libre = Vector3.zero;
            p.t.localRotation = p.rotCasa;
            Resaltar(p, false);
        });
    }

    private void Visible(Pieza p, bool ver)
    {
        p.visible = ver;
        foreach (Renderer r in p.renderers) r.enabled = ver;
        if (!ver) Resaltar(p, false);
    }

    private void Resaltar(Pieza p, bool si)
    {
        if (p.resaltada == si) return;
        p.resaltada = si;
        for (int i = 0; i < p.renderers.Length; i++)
        {
            if (si)
            {
                Material[] ms = new Material[p.originales[i].Length];
                for (int k = 0; k < ms.Length; k++)
                {
                    ms[k] = Holo.MatPiezaResalte != null ? new Material(Holo.MatPiezaResalte) : p.originales[i][k];
                }
                p.renderers[i].materials = ms;
            }
            else
            {
                p.renderers[i].sharedMaterials = p.originales[i];
            }
        }
        if (si) CrearEtiqueta(p);
        else if (!p.etiquetada) QuitarEtiqueta(p);
    }

    // ------------------------------------------------------------------ etiquetas con línea

    private void CrearEtiqueta(Pieza p)
    {
        if (p.etiqueta != null) return;
        p.etiqueta = Holo.Texto(p.nombre.ToUpperInvariant(), transform, Vector3.zero, 0.028f, Holo.Cian, TextAnchor.MiddleLeft, "Etiqueta_" + p.nombre);
        p.linea = Holo.Linea(transform, "Linea_" + p.nombre, Holo.CianTenue, 0.0015f, true, 3);
    }

    private void QuitarEtiqueta(Pieza p)
    {
        if (p.etiqueta != null) Destroy(p.etiqueta.gameObject);
        if (p.linea != null) Destroy(p.linea.gameObject);
        p.etiqueta = null;
        p.linea = null;
    }

    private void ActualizarEtiquetas()
    {
        Camera cam = Camera.main;
        Vector3 derecha = cam != null ? cam.transform.right : transform.right;
        Vector3 centro = pivote.position;
        int izquierdas = 0, derechas = 0;
        foreach (Pieza p in piezas)
        {
            if (p.etiqueta == null) continue;
            Bounds b = p.renderers[0].bounds;
            foreach (Renderer r in p.renderers) b.Encapsulate(r.bounds);
            // Cada etiqueta sale hacia el lado donde está su pieza; se escalonan para no encimarse
            bool aLaDerecha = Vector3.Dot(b.center - centro, derecha) >= 0f;
            int fila = aLaDerecha ? derechas++ : izquierdas++;
            Vector3 lado = aLaDerecha ? derecha : -derecha;
            Vector3 codo = b.center + Vector3.up * (0.04f + 0.035f * fila) + lado * (b.extents.magnitude * 0.5f + 0.05f);
            Vector3 fin = codo + lado * 0.07f;
            p.linea.SetPosition(0, b.center);
            p.linea.SetPosition(1, codo);
            p.linea.SetPosition(2, fin);
            p.etiqueta.anchor = aLaDerecha ? TextAnchor.MiddleLeft : TextAnchor.MiddleRight;
            p.etiqueta.transform.position = fin + lado * 0.008f;
            if (cam != null) p.etiqueta.transform.rotation = Quaternion.LookRotation(p.etiqueta.transform.position - cam.transform.position);
        }
    }

    // ------------------------------------------------------------------ animación

    void Update()
    {
        AnimarPedestal();
        if (!Cargado) return;
        float k = 1f - Mathf.Exp(-velocidad * Time.deltaTime);
        despiece = Mathf.Lerp(despiece, despieceObjetivo, k);
        escala = Mathf.Lerp(escala, escalaObjetivo, k);
        Quaternion giro = orientacionExterna.HasValue ? Quaternion.Inverse(transform.rotation) * orientacionExterna.Value : giroObjetivo;
        pivote.localRotation = Quaternion.Slerp(pivote.localRotation, giro, orientacionExterna.HasValue ? Mathf.Min(1f, k * 3f) : k);
        aparicion = Mathf.MoveTowards(aparicion, 1f, Time.deltaTime * 1.5f);
        Aplicar(k);
        ActualizarEtiquetas();
    }

    private void Aplicar(float k)
    {
        // Al despiezarse se encoge un poco para que todas las piezas sigan a la vista
        float e = escala * Mathf.SmoothStep(0.6f, 1f, aparicion) / (1f + 0.45f * despiece);
        pivote.localScale = Vector3.one * e;
        // La pieza más baja queda justo sobre el pedestal, esté armado o despiezado
        float minimo = -tamano.y * 0.5f;
        foreach (Pieza p in piezas)
        {
            if (p.visible) minimo = Mathf.Min(minimo, p.abajo + p.subeBaja * despiece);
        }
        pivote.localPosition = Vector3.up * (alturaPedestal - minimo * e);
        // Armado se toma el robot entero; despiezado, cada pieza por separado
        bool sueltas = despieceObjetivo > 0.05f;
        if (animacion.Length > 0) AnimarArticulaciones();
        CalcularPose(Mathf.Min(1f, k * 2.5f));
        foreach (Pieza p in piezas)
        {
            if (p.enVuelo) continue;                       // la mueve la física (Proyectil)
            p.caja.enabled = sueltas && p.visible && !p.desechada;
            if (p.desechada) continue;
            Vector3 lugar = p.posePos + p.direccion * despiece;
            if (ManoRM.Agarrados.Contains(p.cuerpo))
            {
                // La mueve la mano y se agranda para verla de cerca, como Tony Stark
                p.suelta = true;
                p.libre = p.t.localPosition - lugar;
                p.volviendo = false;
                p.inspeccionada = false;
                p.t.localScale = Vector3.Lerp(p.t.localScale, Vector3.one * Aumento(p, 0.16f), k);
                continue;
            }
            if (p.inspeccionada)
            {
                Camera cam = Camera.main;
                Vector3 frente = cam != null ? Vector3.ProjectOnPlane(cam.transform.forward, Vector3.up).normalized : transform.forward;
                Vector3 destino = cam != null ? cam.transform.position + frente * 0.5f + Vector3.down * 0.08f : pivote.position + Vector3.up * 0.35f;
                p.t.position = Vector3.Lerp(p.t.position, destino, k);
                p.t.rotation = Quaternion.AngleAxis(Time.deltaTime * 30f, Vector3.up) * p.t.rotation;
                p.t.localScale = Vector3.Lerp(p.t.localScale, Vector3.one * Aumento(p, 0.28f), k);
                p.libre = p.t.localPosition - lugar;
                continue;
            }
            p.t.localScale = Vector3.Lerp(p.t.localScale, Vector3.one, k);
            if (p.volviendo)
            {
                p.libre = Vector3.Lerp(p.libre, Vector3.zero, k);
                p.t.localRotation = Quaternion.Slerp(p.t.localRotation, p.poseRot, k);
                if (p.libre.sqrMagnitude < 1e-8f && Quaternion.Angle(p.t.localRotation, p.poseRot) < 0.5f)
                {
                    p.libre = Vector3.zero;
                    p.t.localRotation = p.poseRot;
                    p.volviendo = false;
                    p.suelta = false;
                }
            }
            else if (!p.suelta)
            {
                p.t.localRotation = p.poseRot;   // sigue a sus articulaciones
            }
            p.t.localPosition = lugar + p.libre;
        }

        // Collider para agarrarlo con la mano: la caja del robot armado
        float radio = Mathf.Max(tamano.x, tamano.z) * 0.5f * e;
        caja.enabled = Cargado && !sueltas;
        caja.center = pivote.localPosition;
        caja.size = new Vector3(radio * 2f, tamano.y * e, radio * 2f) * (1f + despiece);
    }

    private void ChocarConElRobot(Pieza p, bool chocar)
    {
        Physics.IgnoreCollision(p.caja, caja, !chocar);
        foreach (Pieza otra in piezas)
        {
            if (otra != p) Physics.IgnoreCollision(p.caja, otra.caja, !chocar);
        }
    }

    // Escala local para que la pieza mida "metros" en el mundo (nunca la achica)
    private static float Aumento(Pieza p, float metros)
    {
        float padre = p.t.parent != null ? p.t.parent.lossyScale.x : 1f;
        float actual = p.tamanoMayor * padre;
        return actual > 0.0001f ? Mathf.Max(1f, metros / actual) : 1f;
    }

    // ------------------------------------------------------------------ pedestal

    private LineRenderer anilloGiro;

    private void CrearPedestal()
    {
        pedestal = new GameObject("Pedestal").transform;
        pedestal.SetParent(transform, false);
        Holo.Arco(pedestal, "Anillo_exterior", 0.24f, 0f, 360f, Holo.CianTenue, 0.003f, true);
        Holo.Arco(pedestal, "Anillo_interior", 0.2f, 0f, 360f, new Color(0.35f, 0.92f, 1f, 0.18f), 0.002f, true);
        anilloGiro = Holo.Arco(pedestal, "Anillo_giro", 0.22f, 0f, 100f, Holo.Cian, 0.006f, true, 48);
        for (int i = 0; i < 24; i++)
        {
            float a = i * 15f * Mathf.Deg2Rad;
            Vector3 d = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
            LineRenderer marca = Holo.Linea(pedestal, "Marca_" + i, new Color(0.35f, 0.92f, 1f, 0.3f), 0.0015f, false, 2);
            marca.SetPosition(0, d * 0.245f);
            marca.SetPosition(1, d * (i % 6 == 0 ? 0.27f : 0.255f));
        }
        letrero = Holo.Texto("", pedestal, new Vector3(0f, 0.005f, -0.3f), 0.03f, Holo.Cian, TextAnchor.UpperCenter, "Letrero");
        letrero.transform.localRotation = Quaternion.Euler(60f, 0f, 0f);   // inclinado, como grabado en el piso
    }

    private void AnimarPedestal()
    {
        if (anilloGiro != null) anilloGiro.transform.localRotation = Quaternion.Euler(0f, Time.time * (Cargando ? 240f : 25f), 0f);
    }
}
