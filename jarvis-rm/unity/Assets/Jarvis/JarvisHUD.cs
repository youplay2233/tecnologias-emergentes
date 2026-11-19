using System;
using System.Globalization;
using System.Text;
using UnityEngine;
using UnityEngine.SceneManagement;

// JARVIS EN REALIDAD MIXTA
//
// El cerebro (Claude, en la laptop: `uv run jarvis-rm` en ~/jarvis) manda órdenes por WebSocket y aquí
// se ejecutan: el reactor muestra el estado, el panel lo que Jarvis responde, el robot se carga y se
// despieza, y la tableta muestra los modelos y documentos disponibles.
//
// Controles:  A = hablar con Jarvis (o di «Oye Jarvis») · B = lanzar al bote lo que tengas en la mano
//             (si no tienes nada, trae el HUD al frente) · X = tableta · gatillo = pulsar botones
//             agarre del control o pinza con la mano = tomar el robot, sus piezas, la tableta o los objetos
// En el editor: barra espaciadora = A, T = tableta, clic = pulsar botones, arrastrar = agarrar, B = al bote.
public class JarvisHUD : MonoBehaviour
{
    [Header("Conexión con la laptop")]
    [Tooltip("Vacío = buscar la laptop sola por la red. O escribe ws://IP_DE_LA_LAPTOP:8765")]
    public string direccion = "";
    public int puertoBaliza = 8766;
    public string token = "";

    private JarvisEnlace enlace;
    private Transform hud;
    private NucleoJarvis nucleo;
    private PanelHolo panel;
    private TextMesh oido, consola;
    private ModeloRobot modelo;
    private TabletaJarvis tableta;
    private ObjetosFisicos objetos;
    private AudioVisor audioVisor;
    private ManoEspejo espejo;

    private string estado = "reposo";
    private string ultimoCiclo = "";
    private float proximoRefresco;
    private float proximoRegistro;
    private string direccionGuardada = "";

    void Start()
    {
        if (!EntradaRM.XRActivo && Camera.allCamerasCount == 0) CamaraEscritorio.Crear();
        if (!EntradaRM.XRActivo) gameObject.AddComponent<ArrastreRaton>();
        ConstruirHUD();
        Conectar();
        if (EntradaRM.XRActivo)
        {
            Invoke("Recentrar", 1.0f);   // espera a que el visor tenga la posición de la cabeza
#if TEC_META_XR
            // Sin límite de zona de juego: con passthrough el sistema permite ocultar el guardian
            if (OVRManager.instance != null) OVRManager.instance.shouldBoundaryVisibilityBeSuppressed = true;
#endif
        }
    }

    // ------------------------------------------------------------------ escena

    private void ConstruirHUD()
    {
        hud = new GameObject("HUD").transform;
        hud.SetParent(transform, false);

        // El usuario está en el origen mirando hacia +Z; todo queda a la altura de los ojos o del pecho
        nucleo = Crear<NucleoJarvis>("Reactor", new Vector3(0.48f, 1.48f, 1.25f), 22f);
        panel = Crear<PanelHolo>("Panel", new Vector3(-0.52f, 1.42f, 1.3f), -24f);
        consola = Holo.Texto("", Crear<Transform>("Consola", new Vector3(0.48f, 1.25f, 1.25f), 22f), Vector3.zero, 0.022f,
                             new Color(0.6f, 0.95f, 1f, 0.85f), TextAnchor.UpperCenter, "Texto");
        oido = Holo.Texto("", Crear<Transform>("Oido", new Vector3(0f, 1.78f, 1.45f), 0f), Vector3.zero, 0.032f,
                          new Color(1f, 0.92f, 0.6f), TextAnchor.MiddleCenter, "Texto");

        modelo = Crear<ModeloRobot>("Robot", new Vector3(0f, 0.85f, 0.95f), 0f);
        objetos = gameObject.AddComponent<ObjetosFisicos>();
        espejo = gameObject.AddComponent<ManoEspejo>();
        espejo.modelo = modelo;
        Crear<BoteBasura>("Bote_de_basura", new Vector3(0.6f, 0f, 0.75f), 0f);   // en el piso, adelante a la derecha

        // Micrófono y bocina del visor: Jarvis te oye y le oyes aunque te alejes de la laptop
        audioVisor = gameObject.AddComponent<AudioVisor>();
        audioVisor.origenDeLaVoz = nucleo.transform;
        audioVisor.enlace = () => enlace;

        tableta = Crear<TabletaJarvis>("Tableta", new Vector3(-0.25f, 1.15f, 0.55f), -20f);
        tableta.alPedir = Pedir;
        tableta.gameObject.SetActive(false);
    }

    private T Crear<T>(string nombre, Vector3 pos, float giroY) where T : Component
    {
        GameObject go = new GameObject(nombre);
        go.transform.SetParent(hud, false);
        go.transform.localPosition = pos;
        go.transform.localRotation = Quaternion.Euler(0f, giroY, 0f);
        return typeof(T) == typeof(Transform) ? go.transform as T : go.AddComponent<T>();
    }

    // Pone el HUD frente a donde mira el usuario (botón B)
    private void Recentrar()
    {
        Vector3 pos;
        Quaternion rot;
        if (!FrenteAlUsuario(out pos, out rot)) return;
        hud.position = pos;
        hud.rotation = rot;
        siguiendo = false;
        Holo.Sonido("blip", pos, 0.2f);
    }

    private static bool FrenteAlUsuario(out Vector3 pos, out Quaternion rot)
    {
        pos = Vector3.zero;
        rot = Quaternion.identity;
        Camera cam = Camera.main;
        if (cam == null) return false;
        Vector3 frente = Vector3.ProjectOnPlane(cam.transform.forward, Vector3.up).normalized;
        if (frente.sqrMagnitude < 0.01f) return false;
        Vector3 p = cam.transform.position;
        pos = new Vector3(p.x, 0f, p.z);   // y = 0 es el piso real (origen de seguimiento en el piso)
        rot = Quaternion.LookRotation(frente);
        return true;
    }

    // Modo libre: si te alejas más de un metro o volteas mucho, el HUD (con el robot) se reacomoda
    // suavemente frente a ti. "Quédate aquí" lo fija; "sígueme" lo vuelve a activar.
    private bool seguir = true;
    private bool siguiendo;
    private Vector3 destinoHUD;
    private Quaternion rotacionHUD;

    private void Seguir()
    {
        Camera cam = Camera.main;
        if (cam == null) return;
        if (!siguiendo)
        {
            Vector3 p = cam.transform.position;
            float distancia = Vector2.Distance(new Vector2(p.x, p.z), new Vector2(hud.position.x, hud.position.z));
            Vector3 mirada = Vector3.ProjectOnPlane(cam.transform.forward, Vector3.up);
            float angulo = Vector3.Angle(mirada, hud.forward);
            if ((distancia > 1.0f || angulo > 70f) && FrenteAlUsuario(out destinoHUD, out rotacionHUD)) siguiendo = true;
            return;
        }
        float k = 1f - Mathf.Exp(-2.5f * Time.deltaTime);
        hud.position = Vector3.Lerp(hud.position, destinoHUD, k);
        hud.rotation = Quaternion.Slerp(hud.rotation, rotacionHUD, k);
        if (Vector3.Distance(hud.position, destinoHUD) < 0.02f && Quaternion.Angle(hud.rotation, rotacionHUD) < 2f) siguiendo = false;
    }

    // ------------------------------------------------------------------ enlace

    private void Conectar()
    {
        // Dirección fija sin recompilar:  adb push direccion.txt /sdcard/Android/data/com.arielramos.jarvisrm/files/direccion.txt
        string archivo = System.IO.Path.Combine(Application.persistentDataPath, "direccion.txt");
        if (string.IsNullOrEmpty(direccion) && System.IO.File.Exists(archivo)) direccion = System.IO.File.ReadAllText(archivo).Trim();
        direccionGuardada = PlayerPrefs.GetString("jarvis.direccion", "");

        enlace = new JarvisEnlace();
        enlace.Iniciar(string.IsNullOrEmpty(direccion) ? direccionGuardada : direccion, puertoBaliza, Saludo());
        if (string.IsNullOrEmpty(direccion) && !string.IsNullOrEmpty(direccionGuardada)) Invoke("ReintentarBuscando", 6f);
    }

    private string Saludo()
    {
        return JsonUtility.ToJson(new SaludoJarvis { dispositivo = SystemInfo.deviceModel, escena = SceneManager.GetActiveScene().name, token = token });
    }

    private void ReintentarBuscando()
    {
        if (enlace != null && enlace.Conectado) return;
        PlayerPrefs.DeleteKey("jarvis.direccion");
        if (enlace != null) enlace.Dispose();
        enlace = new JarvisEnlace();
        enlace.Iniciar("", puertoBaliza, Saludo());
    }

    void OnDestroy()
    {
        if (enlace != null) enlace.Dispose();
    }

    // Lo que se pide desde la tableta llega a Jarvis como si lo hubieras dicho
    private void Pedir(string texto)
    {
        if (enlace == null || !enlace.Conectado)
        {
            panel.Mostrar("SIN ENLACE", "No estoy conectado a la laptop. En la laptop: uv run jarvis-rm");
            return;
        }
        oido.text = Holo.Envolver("Tú (tableta): " + texto, 50);
        enlace.Enviar(JsonUtility.ToJson(new EventoJarvis { nombre = "pedido", detalle = texto }));
    }

    // ------------------------------------------------------------------ ciclo

    void Update()
    {
        string json;
        int procesados = 0;
        while (procesados++ < 20 && enlace.Recibir(out json))
        {
            MensajeJarvis m = null;
            try { m = JsonUtility.FromJson<MensajeJarvis>(json); }
            catch (Exception) { m = null; }
            if (m != null && !string.IsNullOrEmpty(m.tipo)) Ejecutar(m);
        }
        byte[] binario;
        int audios = 0;
        while (audios++ < 8 && enlace.RecibirBinario(out binario)) audioVisor.Recibir(binario);

        if (EntradaRM.BotonA() || Input.GetKeyDown(KeyCode.Space)) enlace.Enviar("{\"tipo\":\"boton\",\"nombre\":\"A\"}");
        // B: si tienes algo en la mano, va al bote de basura; si no, el HUD se pone frente a ti
        if (EntradaRM.BotonB() && !ManoRM.LanzarAlBote()) Recentrar();
        if (EntradaRM.BotonX() || Input.GetKeyDown(KeyCode.T)) tableta.Mostrar(!tableta.gameObject.activeSelf);
        if (!EntradaRM.XRActivo && Input.GetMouseButtonDown(0)) ClicDelRaton();

        nucleo.Estado(estado, enlace.Conectado);
        if (EntradaRM.XRActivo && seguir) Seguir();
        if (Time.unscaledTime >= proximoRefresco)
        {
            proximoRefresco = Time.unscaledTime + 0.25f;
            PintarConsola();
            if (EntradaRM.XRActivo && Time.unscaledTime >= proximoRegistro)
            {
                proximoRegistro = Time.unscaledTime + 2f;
                Debug.Log("[Jarvis] Manos: " + ManoRM.Diagnostico().Replace("\n", " | "));
            }
            if (enlace.Conectado && string.IsNullOrEmpty(direccion) && direccionGuardada != enlace.Servidor)
            {
                direccionGuardada = enlace.Servidor;   // la próxima vez conecta más rápido
                PlayerPrefs.SetString("jarvis.direccion", direccionGuardada);
            }
        }
    }

    private void ClicDelRaton()
    {
        foreach (Camera cam in Camera.allCameras)
        {
            if (!cam.isActiveAndEnabled) continue;
            Vector3 punto;
            BotonHolo b = PunteroRM.Apuntado(cam.ScreenPointToRay(Input.mousePosition), 20f, out punto);
            if (b != null) { b.Pulsar(); return; }
        }
    }

    // ------------------------------------------------------------------ órdenes

    private async void Ejecutar(MensajeJarvis m)
    {
        string detalle = null;
        bool ok = true;
        try
        {
            switch (m.tipo)
            {
                case "estado": estado = string.IsNullOrEmpty(m.valor) ? "reposo" : m.valor; break;
                case "oido": oido.text = Holo.Envolver("Tú: " + m.texto, 50); break;
                case "decir": panel.Mostrar("J.A.R.V.I.S.", m.texto); break;
                case "panel": panel.Mostrar(m.titulo, m.texto); detalle = "Panel mostrado."; break;
                case "metricas":
                    ultimoCiclo = "escucha " + m.escucha_ms + " ms · cerebro " + m.cerebro_ms + " ms · voz " + m.voz_ms + " ms";
                    break;
                case "catalogo": tableta.Catalogo(m.modelos, m.documentos); break;
                case "tableta":
                    tableta.Mostrar(m.activa);
                    detalle = m.activa ? "Tableta visible frente al usuario." : "Tableta oculta.";
                    break;
                case "modelo": detalle = await Modelo(m); break;
                case "crear": detalle = objetos.Crear(m); break;
                case "lanzar": detalle = objetos.Lanzar(m); break;
                case "gravedad": detalle = objetos.Gravedad(m.activa); break;
                case "limpiar": detalle = objetos.Limpiar(); break;
                case "captura":   // solo sin visor: lo usa la prueba automática desde la laptop
                    if (EntradaRM.XRActivo) throw new InvalidOperationException("Las capturas solo funcionan sin visor.");
                    ScreenCapture.CaptureScreenshot(m.texto);
                    detalle = "Captura guardada en " + m.texto;
                    break;
                case "prueba_agarre":   // solo sin visor: mueve una pieza como si la tomara la mano
                    if (EntradaRM.XRActivo) throw new InvalidOperationException("Solo sin visor.");
                    Rigidbody pieza = m.piezas != null && m.piezas.Length > 0 ? modelo.CuerpoDe(m.piezas[0]) : null;
                    if (pieza == null) throw new ArgumentException("No hay esa pieza.");
                    StartCoroutine(MoverComoMano(pieza, hud.right * m.numero + Vector3.up * m.numero2));
                    detalle = "Moviendo " + m.piezas[0] + ".";
                    break;
                case "prueba_lanzar":   // solo sin visor: lanza una pieza hacia el frente (para probar un tiro fallado)
                    if (EntradaRM.XRActivo) throw new InvalidOperationException("Solo sin visor.");
                    Rigidbody lanzada = m.piezas != null && m.piezas.Length > 0 ? modelo.CuerpoDe(m.piezas[0]) : null;
                    if (lanzada == null) throw new ArgumentException("No hay esa pieza.");
                    modelo.Lanzar(lanzada.GetComponent<PiezaRM>(), hud.forward * m.numero + Vector3.up * m.numero2);
                    detalle = "Lanzada " + m.piezas[0] + ".";
                    break;
                case "salir":
                    if (EntradaRM.XRActivo) throw new InvalidOperationException("Solo sin visor.");
                    Invoke("Salir", 0.5f);
                    detalle = "Cerrando.";
                    break;
                case "espejo":
                    detalle = espejo.Activar(m.activa);
                    break;
                case "hud":
                    if (m.texto == "fijo") { seguir = false; siguiendo = false; detalle = "HUD fijo en este lugar."; }
                    else if (m.texto == "seguir") { seguir = true; detalle = "El HUD sigue al usuario."; }
                    else { Recentrar(); detalle = "HUD frente al usuario."; }
                    break;
                case "bote":
                    if (BoteBasura.Instancia == null) throw new InvalidOperationException("No hay bote de basura.");
                    detalle = BoteBasura.Instancia.Mover(m.texto);
                    break;
                case "pitido": Holo.Sonido("abrir", nucleo.transform.position, 0.3f); break;   // Jarvis te escucha
                case "bienvenida": case "pong": break;
                default:
                    ok = false;
                    detalle = "Esta app no conoce la orden «" + m.tipo + "».";
                    break;
            }
        }
        catch (Exception e)
        {
            ok = false;
            detalle = e.Message;
            Debug.LogException(e);
        }

        // Las órdenes con id esperan acuse: así el cerebro sabe qué pasó de verdad en la escena
        if (m.id > 0)
        {
            AcuseJarvis acuse = new AcuseJarvis { id = m.id, ok = ok, detalle = detalle ?? "Hecho.", objetos = objetos.Cantidad };
            enlace.Enviar(JsonUtility.ToJson(acuse));
        }
    }

    private System.Collections.IEnumerator MoverComoMano(Rigidbody rb, Vector3 delta)
    {
        ManoRM.Agarrados.Add(rb);
        Vector3 inicio = rb.position;
        for (float t = 0f; t < 1f; t += Time.deltaTime / 0.6f)
        {
            rb.transform.position = Vector3.Lerp(inicio, inicio + delta, Mathf.SmoothStep(0f, 1f, t));
            yield return null;
        }
        rb.transform.position = inicio + delta;
        ManoRM.Agarrados.Remove(rb);
    }

    private void Salir()
    {
        Application.Quit();
    }

    private async System.Threading.Tasks.Task<string> Modelo(MensajeJarvis m)
    {
        switch (m.accion)
        {
            case "cargar":
                if (espejo.activa) espejo.Activar(false);   // suelta también el giro de la muñeca
                return await modelo.Cargar(m.clave, string.IsNullOrEmpty(m.texto) ? m.clave : m.texto, m.url);
            case "despiezar": return modelo.Despiezar(m.numero);
            case "girar": return modelo.Girar(m.numero, m.numero2, m.texto);
            case "escala": return modelo.Escalar(m.numero);
            case "mostrar_todo": return modelo.MostrarTodo();
            case "cerrar":
                string nombre = modelo.Nombre;
                modelo.Cerrar();
                return string.IsNullOrEmpty(nombre) ? "No había modelo abierto." : nombre + " cerrado.";
            case "animar":
                espejo.activa = false;
                return modelo.Animar(m.texto);
            case "articulacion":
                espejo.activa = false;
                return modelo.MoverArticulacion(m.texto, m.numero);
            case "etiquetar":
                return modelo.Piezas("etiquetar", m.piezas);
            case "inspeccionar":
                return modelo.Inspeccionar(m.piezas != null && m.piezas.Length > 0 ? m.piezas[0] : "");
            case "desechar":
                return modelo.Desechar(m.piezas);
            case "aislar":
            case "ocultar":
            case "mostrar":
            case "resaltar":
                return modelo.Piezas(m.accion, m.piezas);
            default:
                throw new ArgumentException("Acción de modelo desconocida: " + m.accion);
        }
    }

    // ------------------------------------------------------------------ consola de estado

    private void PintarConsola()
    {
        StringBuilder sb = new StringBuilder();
        if (enlace.Conectado)
        {
            sb.Append("ENLACE  ").Append(enlace.Servidor.Replace("ws://", "")).Append('\n');
            if (enlace.Muestras > 0)
            {
                sb.Append("red ").Append(enlace.RttPromedioMs.ToString("0.0", CultureInfo.InvariantCulture)).Append(" ms");
                sb.Append(" · jitter ").Append(enlace.JitterMs.ToString("0.0", CultureInfo.InvariantCulture)).Append(" ms\n");
            }
            if (ultimoCiclo.Length > 0) sb.Append(ultimoCiclo).Append('\n');
            if (modelo.Cargado) sb.Append("modelo: ").Append(modelo.Nombre).Append('\n');
            if (EntradaRM.XRActivo) sb.Append(ManoRM.Diagnostico()).Append('\n');
            sb.Append("(A) hablar  (B) al bote / centrar  (X) tableta");
        }
        else
        {
            sb.Append("BUSCANDO EL CEREBRO DE JARVIS... (").Append(enlace.Estado).Append(")\n");
            sb.Append("En la laptop:  uv run jarvis-rm");
            if (!string.IsNullOrEmpty(enlace.UltimoError)) sb.Append('\n').Append(Holo.Envolver(enlace.UltimoError, 48));
        }
        consola.text = sb.ToString();
    }
}
