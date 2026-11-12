using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;
using UnityEngine.SceneManagement;

// JARVIS EN REALIDAD MIXTA: la "visión" del proyecto final.
//
// El cerebro (Claude, en la laptop) manda órdenes por la red y este script las ejecuta en la escena:
// muestra lo que Jarvis dice, cambia el estado del núcleo y crea o lanza objetos físicos.
// Reutiliza lo aprendido en las prácticas:
//   - GameObject, jerarquía y prefab (Práctica 1): el núcleo con sus anillos y el panel holográfico.
//   - Instantiate, AddForce y Destroy (Práctica 3, el cañón): crear y lanzar objetos.
//   - Rigidbody, gravedad y Colliders (Físicas S1 y S2): los objetos caen sobre el piso real.
//   - Latencia y protocolos: UDP para encontrar la laptop, WebSocket/TCP para las órdenes, y se mide el RTT.
[Serializable]
public class MensajeJarvis
{
    public string tipo;
    public int id;
    public string texto;
    public string titulo;
    public string valor;
    public string forma;
    public int cantidad;
    public string color;
    public float tamano;
    public float fuerza;
    public bool activa;
    public int escucha_ms;
    public int cerebro_ms;
    public int voz_ms;
}

[Serializable]
public class AcuseJarvis
{
    public string tipo = "ack";
    public int id;
    public bool ok = true;
    public string detalle;
    public int objetos;
}

[Serializable]
public class SaludoJarvis
{
    public string tipo = "hola";
    public string dispositivo;
    public string escena;
    public string token;
}

public class JarvisRM : MonoBehaviour
{
    [Header("Conexión con la laptop")]
    [Tooltip("Vacío = buscar la laptop sola por la red. O escribe ws://IP_DE_LA_LAPTOP:8765")]
    public string direccion = "";
    public int puertoBaliza = 8766;
    public string token = "";

    [Header("Escena")]
    public Transform nucleo;
    public Renderer nucleoRenderer;
    public TextMesh textoEstado;     // conexión y latencias
    public TextMesh textoUsuario;    // lo que Jarvis entendió
    public TextMesh tituloPanel;
    public TextMesh textoPanel;      // lo que Jarvis responde
    public Transform zonaDeObjetos;  // dónde aparecen los objetos si no hay visor

    [Header("Materiales: rojo, naranja, amarillo, verde, azul, morado, blanco, gris")]
    public Material[] materiales;

    private static readonly string[] Colores = { "rojo", "naranja", "amarillo", "verde", "azul", "morado", "blanco", "gris" };
    private const int MaximoDeObjetos = 60;

    private JarvisEnlace enlace;
    private readonly List<GameObject> creados = new List<GameObject>();
    private string estado = "reposo";
    private string ultimoCiclo = "";
    private Vector3 escalaNucleo = Vector3.one;
    private Material materialNucleo;
    private float proximoRefresco;
    private string direccionGuardada = "";

    void Start()
    {
        if (nucleo != null) escalaNucleo = nucleo.localScale;
        if (nucleoRenderer != null) materialNucleo = nucleoRenderer.material;   // copia propia para cambiarle el color

        // Dirección fija sin recompilar (cuando el descubrimiento por la red no llega, p. ej. subredes distintas):
        //   adb push direccion.txt /sdcard/Android/data/com.arielramos.practica1rm/files/direccion.txt
        string archivo = System.IO.Path.Combine(Application.persistentDataPath, "direccion.txt");
        if (string.IsNullOrEmpty(direccion) && System.IO.File.Exists(archivo)) direccion = System.IO.File.ReadAllText(archivo).Trim();

        string guardada = PlayerPrefs.GetString("jarvis.direccion", "");
        direccionGuardada = guardada;
        SaludoJarvis saludo = new SaludoJarvis
        {
            dispositivo = SystemInfo.deviceModel,
            escena = SceneManager.GetActiveScene().name,
            token = token
        };
        enlace = new JarvisEnlace();
        enlace.Iniciar(string.IsNullOrEmpty(direccion) ? guardada : direccion, puertoBaliza, JsonUtility.ToJson(saludo));

        // Si la dirección guardada ya no sirve, a los 6 segundos se vuelve a buscar por la red
        if (string.IsNullOrEmpty(direccion) && !string.IsNullOrEmpty(guardada)) Invoke("ReintentarBuscando", 6f);
    }

    private void ReintentarBuscando()
    {
        if (enlace != null && enlace.Conectado) return;
        PlayerPrefs.DeleteKey("jarvis.direccion");
        if (enlace != null) enlace.Dispose();
        SaludoJarvis saludo = new SaludoJarvis { dispositivo = SystemInfo.deviceModel, escena = SceneManager.GetActiveScene().name, token = token };
        enlace = new JarvisEnlace();
        enlace.Iniciar("", puertoBaliza, JsonUtility.ToJson(saludo));
    }

    void OnDestroy()
    {
        if (enlace != null) enlace.Dispose();
        Physics.gravity = new Vector3(0f, -9.81f, 0f);
    }

    void Update()
    {
        // 1. Órdenes que llegaron del cerebro
        string json;
        int procesados = 0;
        while (procesados++ < 20 && enlace.Recibir(out json))
        {
            MensajeJarvis m = null;
            try { m = JsonUtility.FromJson<MensajeJarvis>(json); }
            catch (Exception) { m = null; }
            if (m != null && !string.IsNullOrEmpty(m.tipo)) Ejecutar(m);
        }

        // 2. Botón A (o barra espaciadora): "te escucho", igual que decir «Hey Jarvis»
        if (EntradaRM.BotonA() || Input.GetKeyDown(KeyCode.Space))
        {
            enlace.Enviar("{\"tipo\":\"boton\",\"nombre\":\"A\"}");
        }

        AnimarNucleo();

        if (Time.unscaledTime >= proximoRefresco)
        {
            proximoRefresco = Time.unscaledTime + 0.25f;
            PintarEstado();
            if (enlace.Conectado && string.IsNullOrEmpty(direccion) && direccionGuardada != enlace.Servidor)
            {
                // Se recuerda la laptop para conectar más rápido la próxima vez
                direccionGuardada = enlace.Servidor;
                PlayerPrefs.SetString("jarvis.direccion", direccionGuardada);
            }
        }
    }

    // ---------------------------------------------------------------------- órdenes

    // Ejecuta una orden escrita a mano en JSON, igual que si hubiera llegado por la red.
    // Sirve para probar la escena sin la laptop: lo usa el Puente Jarvis al grabar la demostración.
    public void SimularOrden(string json)
    {
        MensajeJarvis m = JsonUtility.FromJson<MensajeJarvis>(json);
        if (m != null && !string.IsNullOrEmpty(m.tipo)) Ejecutar(m);
    }

    private void Ejecutar(MensajeJarvis m)
    {
        string detalle = null;
        bool ok = true;
        try
        {
            switch (m.tipo)
            {
                case "estado":
                    estado = string.IsNullOrEmpty(m.valor) ? "reposo" : m.valor;
                    break;
                case "oido":
                    if (textoUsuario != null) textoUsuario.text = "Tú: " + Envolver(m.texto, 46);
                    break;
                case "decir":
                    if (tituloPanel != null) tituloPanel.text = "JARVIS";
                    if (textoPanel != null) textoPanel.text = Envolver(m.texto, 34);
                    break;
                case "panel":
                    if (tituloPanel != null) tituloPanel.text = m.titulo;
                    if (textoPanel != null) textoPanel.text = Envolver(m.texto, 34);
                    detalle = "Panel mostrado.";
                    break;
                case "metricas":
                    ultimoCiclo = "Último ciclo: escucha " + m.escucha_ms + " ms · cerebro " + m.cerebro_ms + " ms · voz " + m.voz_ms + " ms";
                    break;
                case "crear":
                    detalle = Crear(m);
                    break;
                case "lanzar":
                    detalle = Lanzar(m);
                    break;
                case "gravedad":
                    detalle = Gravedad(m.activa);
                    break;
                case "limpiar":
                    detalle = Limpiar();
                    break;
            }
        }
        catch (Exception e)
        {
            ok = false;
            detalle = e.Message;
        }

        // Las órdenes con id esperan acuse: así el cerebro sabe qué pasó de verdad en la escena
        if (m.id > 0)
        {
            AcuseJarvis acuse = new AcuseJarvis { id = m.id, ok = ok, detalle = detalle ?? "Hecho.", objetos = creados.Count };
            enlace.Enviar(JsonUtility.ToJson(acuse));
        }
    }

    private string Crear(MensajeJarvis m)
    {
        int cantidad = Mathf.Clamp(m.cantidad, 1, 20);
        float tamano = Mathf.Clamp(m.tamano <= 0f ? 0.2f : m.tamano, 0.05f, 0.6f);
        Vector3 centro;
        Vector3 frente;
        Frente(out centro, out frente);
        Vector3 lado = Vector3.Cross(Vector3.up, frente);

        for (int i = 0; i < cantidad; i++)
        {
            // En fila frente al usuario y un poco escalonados en altura para que caigan uno tras otro
            float columna = (i % 5) - Mathf.Min(cantidad - 1, 4) / 2f;
            Vector3 pos = centro + frente * (1.3f + (i / 5) * (tamano + 0.1f)) + lado * columna * (tamano + 0.08f) + Vector3.up * (0.25f + 0.12f * i);
            Nuevo(m.forma, m.color, tamano, pos);
        }
        return cantidad + " " + Nombre(m.forma) + (cantidad > 1 ? "s" : "") + " de color " + ColorValido(m.color) + " frente al usuario.";
    }

    private string Lanzar(MensajeJarvis m)
    {
        float tamano = Mathf.Clamp(m.tamano <= 0f ? 0.15f : m.tamano, 0.05f, 0.6f);
        float velocidad = Mathf.Clamp(m.fuerza <= 0f ? 6f : m.fuerza, 1f, 20f);
        Vector3 centro;
        Vector3 frente;
        Frente(out centro, out frente);

        // Igual que el cañón: se crea el objeto y se le da un empujón hacia adelante
        GameObject go = Nuevo(m.forma, m.color, tamano, centro + frente * 0.6f + Vector3.down * 0.15f);
        Rigidbody rb = go.GetComponent<Rigidbody>();
        rb.AddForce((frente + Vector3.up * 0.25f).normalized * velocidad, ForceMode.VelocityChange);
        return Nombre(m.forma) + " lanzado a " + velocidad.ToString("0.#", CultureInfo.InvariantCulture) + " metros por segundo.";
    }

    private string Gravedad(bool activa)
    {
        Physics.gravity = activa ? new Vector3(0f, -9.81f, 0f) : Vector3.zero;
        foreach (GameObject go in creados)
        {
            if (go == null) continue;
            Rigidbody rb = go.GetComponent<Rigidbody>();
            rb.WakeUp();
            if (!activa) rb.AddForce(UnityEngine.Random.insideUnitSphere * 0.3f + Vector3.up * 0.4f, ForceMode.VelocityChange);
        }
        return activa ? "Gravedad encendida." : "Gravedad apagada: los objetos flotan.";
    }

    private string Limpiar()
    {
        int n = 0;
        foreach (GameObject go in creados)
        {
            if (go == null) continue;
            Destroy(go);
            n++;
        }
        creados.Clear();
        return n + " objetos borrados.";
    }

    private GameObject Nuevo(string forma, string color, float tamano, Vector3 pos)
    {
        PrimitiveType tipo;
        switch (forma)
        {
            case "esfera": tipo = PrimitiveType.Sphere; break;
            case "capsula": tipo = PrimitiveType.Capsule; break;
            case "cilindro": tipo = PrimitiveType.Cylinder; break;
            default: tipo = PrimitiveType.Cube; break;
        }

        GameObject go = GameObject.CreatePrimitive(tipo);
        go.name = "Jarvis_" + Nombre(forma) + "_" + (creados.Count + 1);
        go.transform.position = pos;
        go.transform.rotation = UnityEngine.Random.rotation;
        go.transform.localScale = (tipo == PrimitiveType.Capsule || tipo == PrimitiveType.Cylinder)
            ? new Vector3(tamano, tamano * 0.5f, tamano)
            : Vector3.one * tamano;

        int indice = Mathf.Max(0, Array.IndexOf(Colores, ColorValido(color)));
        if (materiales != null && indice < materiales.Length && materiales[indice] != null)
        {
            go.GetComponent<MeshRenderer>().sharedMaterial = materiales[indice];
        }
        go.AddComponent<Rigidbody>();
        creados.Add(go);

        // Límite para no llenar la memoria (como el Destroy de la bala)
        while (creados.Count > MaximoDeObjetos)
        {
            if (creados[0] != null) Destroy(creados[0]);
            creados.RemoveAt(0);
        }
        return go;
    }

    // Punto y dirección "frente al usuario": la cabeza en el visor, o la zona fija en el editor
    private void Frente(out Vector3 centro, out Vector3 frente)
    {
        Camera cam = Camera.main;
        if (cam != null && EntradaRM.XRActivo)
        {
            centro = cam.transform.position;
            frente = Vector3.ProjectOnPlane(cam.transform.forward, Vector3.up).normalized;
            if (frente.sqrMagnitude < 0.01f) frente = Vector3.forward;
        }
        else if (zonaDeObjetos != null)
        {
            centro = zonaDeObjetos.position;
            frente = zonaDeObjetos.forward;
        }
        else
        {
            centro = new Vector3(0f, 1.5f, 0f);
            frente = Vector3.forward;
        }
    }

    private static string ColorValido(string color)
    {
        return Array.IndexOf(Colores, color) >= 0 ? color : "azul";
    }

    private static string Nombre(string forma)
    {
        switch (forma)
        {
            case "esfera": return "esfera";
            case "capsula": return "cápsula";
            case "cilindro": return "cilindro";
            default: return "cubo";
        }
    }

    // ---------------------------------------------------------------------- presentación

    private void AnimarNucleo()
    {
        if (nucleo == null) return;
        float velocidad;
        float amplitud;
        Color color;
        switch (estado)
        {
            case "escuchando": velocidad = 6f; amplitud = 0.10f; color = new Color(0.2f, 1.4f, 0.5f); break;
            case "pensando": velocidad = 10f; amplitud = 0.05f; color = new Color(1.5f, 0.9f, 0.1f); break;
            case "hablando": velocidad = 4f; amplitud = 0.16f; color = new Color(0.9f, 1.3f, 1.6f); break;
            default: velocidad = 1.5f; amplitud = 0.03f; color = new Color(0.15f, 1.0f, 1.3f); break;
        }
        if (!enlace.Conectado) color = new Color(0.5f, 0.12f, 0.12f);

        nucleo.localScale = escalaNucleo * (1f + Mathf.Sin(Time.time * velocidad) * amplitud);
        if (materialNucleo != null) materialNucleo.SetColor("_EmissionColor", color);
    }

    private void PintarEstado()
    {
        if (textoEstado == null) return;
        StringBuilder sb = new StringBuilder();
        if (enlace.Conectado)
        {
            sb.Append("Conectado a ").Append(enlace.Servidor.Replace("ws://", "")).Append("  ·  ").Append(estado).Append('\n');
            if (enlace.Muestras > 0)
            {
                sb.Append("Red (ida y vuelta): ").Append(enlace.RttPromedioMs.ToString("0.0", CultureInfo.InvariantCulture)).Append(" ms");
                sb.Append("  ·  jitter ").Append(enlace.JitterMs.ToString("0.0", CultureInfo.InvariantCulture)).Append(" ms\n");
            }
            if (ultimoCiclo.Length > 0) sb.Append(ultimoCiclo).Append('\n');
            sb.Append("Objetos: ").Append(creados.Count).Append("  ·  (A) hablar con Jarvis");
        }
        else
        {
            sb.Append("Buscando el cerebro de Jarvis en la red... (").Append(enlace.Estado).Append(")\n");
            sb.Append("En la laptop:  uv run jarvis-rm");
            if (!string.IsNullOrEmpty(enlace.UltimoError)) sb.Append('\n').Append(Envolver(enlace.UltimoError, 60));
        }
        textoEstado.text = sb.ToString();
    }

    // TextMesh no parte renglones: se parte el texto por palabras
    public static string Envolver(string texto, int ancho)
    {
        if (string.IsNullOrEmpty(texto)) return "";
        StringBuilder sb = new StringBuilder();
        foreach (string parrafo in texto.Replace("\r", "").Split('\n'))
        {
            int enLinea = 0;
            foreach (string palabra in parrafo.Split(' '))
            {
                if (palabra.Length == 0) continue;
                if (enLinea > 0 && enLinea + 1 + palabra.Length > ancho)
                {
                    sb.Append('\n');
                    enLinea = 0;
                }
                if (enLinea > 0)
                {
                    sb.Append(' ');
                    enLinea++;
                }
                sb.Append(palabra);
                enLinea += palabra.Length;
            }
            sb.Append('\n');
        }
        return sb.ToString().TrimEnd('\n');
    }
}
