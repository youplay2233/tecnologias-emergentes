using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.Media;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace PuenteJarvis
{
    // Capturas de cámara y de ventanas del editor.
    public static class Capturas
    {
        public static string Camara(Orden o)
        {
            GameObject temporal = null;
            Camera cam = null;
            try
            {
                if (o.pos != null && o.pos.Length == 3)
                {
                    temporal = new GameObject("PJ_CamaraTemporal");
                    temporal.hideFlags = HideFlags.HideAndDontSave;
                    cam = temporal.AddComponent<Camera>();
                    cam.transform.position = new Vector3(o.pos[0], o.pos[1], o.pos[2]);
                    if (o.mirar != null && o.mirar.Length == 3) cam.transform.LookAt(new Vector3(o.mirar[0], o.mirar[1], o.mirar[2]));
                    cam.fieldOfView = o.fov > 1f ? o.fov : 55f;
                    cam.nearClipPlane = 0.05f;
                    cam.farClipPlane = 200f;
                    cam.clearFlags = CameraClearFlags.SolidColor;
                    cam.backgroundColor = new Color(0.075f, 0.085f, 0.11f, 1f);
                }
                else
                {
                    GameObject go = Inspeccion.Buscar(string.IsNullOrEmpty(o.nombre) ? "CamaraEvidencia" : o.nombre);
                    if (go != null) cam = go.GetComponent<Camera>();
                    if (cam == null) cam = Camera.main;
                    if (cam == null) throw new Exception("no hay cámara para capturar (ni CamaraEvidencia ni MainCamera)");
                }

                int ancho = o.ancho > 0 ? o.ancho : 1280;
                int alto = o.alto > 0 ? o.alto : 720;
                Texture2D tex = Renderizar(cam, ancho, alto, TextureFormat.RGB24);
                string archivo = Path.Combine(Puente.DirCapturas, NombreSeguro(o.salida, o.id) + ".png");
                File.WriteAllBytes(archivo, tex.EncodeToPNG());
                UnityEngine.Object.DestroyImmediate(tex);
                return archivo;
            }
            finally
            {
                if (temporal != null) UnityEngine.Object.DestroyImmediate(temporal);
            }
        }

        public static Texture2D Renderizar(Camera cam, int ancho, int alto, TextureFormat formato)
        {
            RenderTexture rt = new RenderTexture(ancho, alto, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            rt.antiAliasing = 4;
            Texture2D tex = new Texture2D(ancho, alto, formato, false);
            LeerCamara(cam, rt, tex);
            rt.Release();
            UnityEngine.Object.DestroyImmediate(rt);
            return tex;
        }

        public static void LeerCamara(Camera cam, RenderTexture rt, Texture2D tex)
        {
            RenderTexture objetivoPrevio = cam.targetTexture;
            RenderTexture activaPrevia = RenderTexture.active;
            bool habilitada = cam.enabled;
            try
            {
                cam.targetTexture = rt;
                cam.Render();
                RenderTexture.active = rt;
                tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0, false);
                tex.Apply(false);
            }
            finally
            {
                cam.targetTexture = objetivoPrevio;
                cam.enabled = habilitada;
                RenderTexture.active = activaPrevia;
            }
        }

        public static string NombreSeguro(string salida, string id)
        {
            string n = string.IsNullOrEmpty(salida) ? id : salida;
            foreach (char c in Path.GetInvalidFileNameChars()) n = n.Replace(c, '_');
            return n;
        }

        public static Type TipoDeVentana(string nombre)
        {
            Assembly ed = typeof(EditorWindow).Assembly;
            switch ((nombre ?? "").ToLowerInvariant())
            {
                case "inspector": return ed.GetType("UnityEditor.InspectorWindow");
                case "console": case "consola": return ed.GetType("UnityEditor.ConsoleWindow");
                case "hierarchy": case "jerarquia": return ed.GetType("UnityEditor.SceneHierarchyWindow");
                case "game": case "juego": return ed.GetType("UnityEditor.GameView");
                case "project": case "proyecto": return ed.GetType("UnityEditor.ProjectBrowser");
                case "scene": case "escena": return typeof(SceneView);
                default: return null;
            }
        }

        // Copia los pixeles de una ventana del editor (Inspector, Console, ...) a un PNG.
        public static string Ventana(EditorWindow ventana, Orden o)
        {
            float ppp = EditorGUIUtility.pixelsPerPoint;
            int ancho = Mathf.Max(8, Mathf.RoundToInt(ventana.position.width * ppp));
            int alto = Mathf.Max(8, Mathf.RoundToInt(ventana.position.height * ppp));
            Texture2D tex = null;
            string metodoUsado = "";

            // 1) Método interno del editor: GUIView.GrabPixels
            try
            {
                FieldInfo campo = typeof(EditorWindow).GetField("m_Parent", BindingFlags.Instance | BindingFlags.NonPublic);
                object vista = campo != null ? campo.GetValue(ventana) : null;
                MethodInfo grab = null;
                for (Type t = vista != null ? vista.GetType() : null; t != null && grab == null; t = t.BaseType)
                {
                    grab = t.GetMethod("GrabPixels", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.DeclaredOnly);
                }
                if (grab != null)
                {
                    RenderTexture rt = new RenderTexture(ancho, alto, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
                    rt.Create();
                    grab.Invoke(vista, new object[] { rt, new Rect(0, 0, ancho, alto) });
                    RenderTexture previa = RenderTexture.active;
                    RenderTexture.active = rt;
                    tex = new Texture2D(ancho, alto, TextureFormat.RGB24, false);
                    tex.ReadPixels(new Rect(0, 0, ancho, alto), 0, 0, false);
                    tex.Apply(false);
                    RenderTexture.active = previa;
                    rt.Release();
                    UnityEngine.Object.DestroyImmediate(rt);
                    metodoUsado = "GrabPixels";
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning("[PuenteJarvis] GrabPixels falló: " + (e.InnerException ?? e).Message);
                tex = null;
            }

            if (tex != null && Uniforme(tex))
            {
                Debug.LogWarning("[PuenteJarvis] GrabPixels devolvió una imagen de un solo color; se intenta leer la pantalla.");
                UnityEngine.Object.DestroyImmediate(tex);
                tex = null;
            }

            // 2) Respaldo: leer la pantalla en la posición de la ventana
            if (tex == null)
            {
                Color[] pixeles = UnityEditorInternal.InternalEditorUtility.ReadScreenPixel(ventana.position.position * ppp, ancho, alto);
                tex = new Texture2D(ancho, alto, TextureFormat.RGB24, false);
                tex.SetPixels(pixeles);
                tex.Apply(false);
                metodoUsado = "ReadScreenPixel";
                if (Uniforme(tex))
                {
                    UnityEngine.Object.DestroyImmediate(tex);
                    throw new Exception("la captura de " + ventana.titleContent.text + " salió de un solo color (" + ancho + "x" + alto + "); en este editor no se pueden leer los pixeles de la ventana");
                }
            }

            if (o.voltear) Voltear(tex);
            tex = RecortarVacio(tex);
            ancho = tex.width;
            alto = tex.height;

            string archivo = Path.Combine(Puente.DirCapturas, NombreSeguro(o.salida, o.id) + ".png");
            File.WriteAllBytes(archivo, tex.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(tex);
            return archivo + " (" + ancho + "x" + alto + ", " + metodoUsado + ")";
        }

        // Quita la franja vacía de abajo (un Inspector o una consola con poco contenido dejan mucho gris)
        private static Texture2D RecortarVacio(Texture2D tex)
        {
            int w = tex.width, h = tex.height;
            Color32[] p = tex.GetPixels32();   // la fila 0 es la de hasta abajo
            Color32 fondo = p[w / 2];
            int fila = 0;
            for (; fila < h; fila++)
            {
                bool vacia = true;
                for (int x = 0; x < w && vacia; x += 2)
                {
                    Color32 c = p[fila * w + x];
                    vacia = Mathf.Abs(c.r - fondo.r) <= 6 && Mathf.Abs(c.g - fondo.g) <= 6 && Mathf.Abs(c.b - fondo.b) <= 6;
                }
                if (!vacia) break;
            }
            int quitar = fila - 14;
            if (quitar < 40 || quitar >= h - 40) return tex;
            Texture2D recorte = new Texture2D(w, h - quitar, TextureFormat.RGB24, false);
            recorte.SetPixels(tex.GetPixels(0, quitar, w, h - quitar));
            recorte.Apply(false);
            UnityEngine.Object.DestroyImmediate(tex);
            return recorte;
        }

        // true si todos los pixeles muestreados son iguales (ventana sin dibujar)
        private static bool Uniforme(Texture2D tex)
        {
            Color32[] p = tex.GetPixels32();
            if (p.Length == 0) return true;
            Color32 c = p[0];
            int paso = Mathf.Max(1, p.Length / 4096);
            for (int i = 0; i < p.Length; i += paso)
            {
                if (Mathf.Abs(p[i].r - c.r) > 6 || Mathf.Abs(p[i].g - c.g) > 6 || Mathf.Abs(p[i].b - c.b) > 6) return false;
            }
            return true;
        }

        private static void Voltear(Texture2D tex)
        {
            Color32[] p = tex.GetPixels32();
            int w = tex.width, h = tex.height;
            Color32[] q = new Color32[p.Length];
            for (int y = 0; y < h; y++) Array.Copy(p, y * w, q, (h - 1 - y) * w, w);
            tex.SetPixels32(q);
            tex.Apply(false);
        }
    }

    [Serializable]
    public class Trabajo
    {
        public string tipo;
        public int fase;
        public Orden orden;
        public int cuadros;
        public int espera;
        public string inicio;
        public string archivoVideo;
        public string archivoCuadros;
        public string notas;
    }

    // Trabajos que duran varios cuadros y sobreviven a la recarga de dominio (entrar/salir de Play).
    public static class Trabajos
    {
        private const string Clave = "PJ.trabajo";
        private static Trabajo actual;

        // Estado que solo vive mientras dura la reproducción
        private static Camera camara;
        private static RenderTexture rt;
        private static Texture2D tex;
        private static MediaEncoder codificador;
        private static FileStream cuadrosMjpeg;
        private static int ultimoCuadro = -1;
        private static int siguienteAccion;
        private static bool capturando;
        private static double limite;
        private static EditorWindow ventanaTemporal;
        private static readonly List<Component> plegados = new List<Component>();
        private const float MinimoAncho = 400f, MinimoAlto = 500f;

        public static bool HayTrabajo { get { return actual != null; } }

        public static string Descripcion
        {
            get { return actual == null ? "" : actual.tipo + " fase " + actual.fase + " cuadros " + actual.cuadros; }
        }

        public static void Restaurar()
        {
            string json = SessionState.GetString(Clave, "");
            actual = string.IsNullOrEmpty(json) ? null : JsonUtility.FromJson<Trabajo>(json);
            limite = EditorApplication.timeSinceStartup + 120.0;
        }

        private static void Guardar()
        {
            SessionState.SetString(Clave, actual == null ? "" : JsonUtility.ToJson(actual));
        }

        public static void Iniciar(string tipo, Orden o)
        {
            actual = new Trabajo { tipo = tipo, fase = 0, orden = o, inicio = Puente.Ahora(), notas = "" };
            limite = EditorApplication.timeSinceStartup + 120.0;
            Guardar();
        }

        private static void Terminar(bool ok, string mensaje, string datos)
        {
            if (actual == null) return;
            Resultado r = new Resultado { id = actual.orden.id, cmd = actual.orden.cmd, ok = ok, mensaje = mensaje, datos = datos };
            actual = null;
            Guardar();
            Puente.EscribirResultado(r);
            Puente.EscribirEstado();
        }

        public static void Fallar(string motivo)
        {
            if (actual == null) return;
            CerrarCaptura();
            Time.captureFramerate = 0;
            bool estabaEnPlay = EditorApplication.isPlaying && actual.tipo == "grabar";
            Terminar(false, motivo, "");
            if (estabaEnPlay) EditorApplication.isPlaying = false;
        }

        // ---------------------------------------------------------------- cada 0.25 s

        public static void CadaTick()
        {
            if (actual == null) return;
            if (actual.tipo == "ventana") TickVentana();
            else if (actual.tipo == "grabar") TickGrabar();
        }

        private static void TickVentana()
        {
            Orden o = actual.orden;
            Type tipo = Capturas.TipoDeVentana(o.nombre);
            if (tipo == null)
            {
                Fallar("ventana desconocida: " + o.nombre + " (usa Inspector, Console, Hierarchy, Game, Scene o Project)");
                return;
            }

            if (actual.fase == 0)
            {
                if (!string.IsNullOrEmpty(o.ruta))
                {
                    GameObject go = Inspeccion.Buscar(o.ruta);
                    if (go == null)
                    {
                        Fallar("no encontré el objeto a seleccionar: " + o.ruta);
                        return;
                    }
                    Selection.activeGameObject = go;
                }
                EditorWindow v = EditorWindow.GetWindow(tipo, false, null, true);
                if (v.position.width < MinimoAncho || v.position.height < MinimoAlto)
                {
                    // La ventana acoplada es muy chica (por ejemplo, el editor no está maximizado):
                    // se abre una flotante del tamaño de la captura y se cierra al terminar.
                    ventanaTemporal = (EditorWindow)ScriptableObject.CreateInstance(tipo);
                    ventanaTemporal.Show();
                    ventanaTemporal.position = new Rect(80f, 40f, tipo.Name == "ConsoleWindow" ? 900f : 480f, 1000f);
                    v = ventanaTemporal;
                }
                // En el Inspector se pliegan Mesh Filter, Mesh Renderer y, en la UI, Rect Transform, Canvas Renderer e Image
                // (no dicen nada de la práctica) para que quepa lo importante (Rigidbody, On Click); se despliegan al terminar.
                if (tipo.Name == "InspectorWindow" && Selection.activeGameObject != null)
                {
                    foreach (Component c in Selection.activeGameObject.GetComponents<Component>())
                    {
                        string tipoC = c != null ? c.GetType().Name : "";
                        bool plegar = tipoC == "MeshFilter" || tipoC == "MeshRenderer" || tipoC == "CanvasRenderer" || tipoC == "Image" || tipoC == "RectTransform";
                        if (plegar && UnityEditorInternal.InternalEditorUtility.GetIsInspectorExpanded(c))
                        {
                            UnityEditorInternal.InternalEditorUtility.SetIsInspectorExpanded(c, false);
                            plegados.Add(c);
                        }
                    }
                    ActiveEditorTracker.sharedTracker.ForceRebuild();
                }
                v.Show();
                v.Focus();
                v.Repaint();
                actual.fase = 1;
                actual.espera = 6;
                Guardar();
                return;
            }

            if (actual.espera > 0)
            {
                actual.espera--;
                EditorWindow v = ventanaTemporal != null ? ventanaTemporal : EditorWindow.GetWindow(tipo, false, null, false);
                v.Repaint();
                return;
            }

            EditorWindow ventana = ventanaTemporal != null ? ventanaTemporal : EditorWindow.GetWindow(tipo, false, null, false);
            try
            {
                string datos = Capturas.Ventana(ventana, o);
                Terminar(true, "ventana capturada: " + o.nombre, datos);
            }
            finally
            {
                if (ventanaTemporal != null) ventanaTemporal.Close();
                ventanaTemporal = null;
                foreach (Component c in plegados)
                {
                    if (c != null) UnityEditorInternal.InternalEditorUtility.SetIsInspectorExpanded(c, true);
                }
                plegados.Clear();
            }
        }

        private static void TickGrabar()
        {
            Orden o = actual.orden;

            if (actual.fase == 0)
            {
                if (EditorApplication.isPlaying)
                {
                    Fallar("Unity ya estaba en Play; se detuvo para poder grabar. Manda la orden otra vez.");
                    return;
                }
                if (!string.IsNullOrEmpty(o.ruta)) EditorSceneManager.OpenScene(o.ruta, OpenSceneMode.Single);
                actual.fase = 1;
                Guardar();
                EditorApplication.isPlaying = true;   // provoca recarga de dominio; el trabajo sigue en fase 1
                return;
            }

            if (actual.fase == 1)
            {
                if (!EditorApplication.isPlaying)
                {
                    if (EditorApplication.timeSinceStartup > limite) Fallar("Unity no entró a Play en 2 minutos (¿errores de compilación?)");
                    return;
                }
                if (Time.frameCount < 5) return;
                IniciarCaptura();
                return;
            }

            if (actual.fase == 3)
            {
                if (EditorApplication.isPlaying || EditorApplication.isPlayingOrWillChangePlaymode) return;
                string datos = "video=" + actual.archivoVideo + "\ncuadros=" + actual.archivoCuadros + "\ntotal=" + actual.cuadros + "\n" + actual.notas;
                Terminar(true, "grabación terminada: " + actual.cuadros + " cuadros", datos);
            }
        }

        // ---------------------------------------------------------------- cada cuadro del editor

        public static void CadaCuadro()
        {
            if (actual == null || actual.tipo != "grabar" || actual.fase != 2) return;

            if (!capturando)
            {
                // Hubo una recarga de dominio a media grabación
                Fallar("la grabación se interrumpió (recarga de scripts durante Play)");
                return;
            }
            if (!EditorApplication.isPlaying)
            {
                Fallar("la reproducción se detuvo antes de terminar la grabación");
                return;
            }
            if (EditorApplication.isPaused) return;
            if (Time.frameCount == ultimoCuadro) return;
            ultimoCuadro = Time.frameCount;

            Orden o = actual.orden;
            int fps = o.fps > 0 ? o.fps : 30;
            float t = actual.cuadros / (float)fps;

            // Acciones programadas (por ejemplo: disparar el cañón cada segundo)
            while (o.acciones != null && siguienteAccion < o.acciones.Length && o.acciones[siguienteAccion].t <= t)
            {
                Accion a = o.acciones[siguienteAccion++];
                GameObject go = Inspeccion.Buscar(a.objetivo);
                if (go == null)
                {
                    actual.notas += "accion sin objetivo: " + a.objetivo + "\n";
                    continue;
                }
                if (string.IsNullOrEmpty(a.arg)) go.SendMessage(a.mensaje, SendMessageOptions.DontRequireReceiver);
                else go.SendMessage(a.mensaje, a.arg, SendMessageOptions.DontRequireReceiver);
            }

            if (camara == null)
            {
                // La práctica pudo cambiar de escena (Conexión de escenas): se sigue con la cámara de evidencia de la nueva
                GameObject nueva = Inspeccion.Buscar(string.IsNullOrEmpty(o.nombre) ? "CamaraEvidencia" : o.nombre);
                camara = nueva != null ? nueva.GetComponent<Camera>() : null;
                if (camara == null)
                {
                    Fallar("la cámara de la grabación desapareció");
                    return;
                }
            }

            Capturas.LeerCamara(camara, rt, tex);

            if (codificador != null)
            {
                try
                {
                    codificador.AddFrame(tex);
                }
                catch (Exception e)
                {
                    actual.notas += "codificador de video falló: " + e.Message + "\n";
                    try { codificador.Dispose(); } catch (Exception) { }
                    codificador = null;
                    actual.archivoVideo = "";
                }
            }
            if (cuadrosMjpeg != null)
            {
                byte[] jpg = tex.EncodeToJPG(88);
                cuadrosMjpeg.Write(jpg, 0, jpg.Length);
            }

            actual.cuadros++;
            int total = Mathf.RoundToInt((o.segundos > 0 ? o.segundos : 20f) * fps);
            if (actual.cuadros >= total)
            {
                CerrarCaptura();
                Time.captureFramerate = 0;
                actual.fase = 3;
                Guardar();
                EditorApplication.isPlaying = false;
            }
        }

        private static void IniciarCaptura()
        {
            Orden o = actual.orden;
            int fps = o.fps > 0 ? o.fps : 30;
            int ancho = o.ancho > 0 ? o.ancho : 1280;
            int alto = o.alto > 0 ? o.alto : 720;

            GameObject go = Inspeccion.Buscar(string.IsNullOrEmpty(o.nombre) ? "CamaraEvidencia" : o.nombre);
            camara = go != null ? go.GetComponent<Camera>() : null;
            if (camara == null) camara = Camera.main;
            if (camara == null)
            {
                Fallar("la escena no tiene CamaraEvidencia ni MainCamera");
                return;
            }

            rt = new RenderTexture(ancho, alto, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            rt.antiAliasing = 4;
            tex = new Texture2D(ancho, alto, TextureFormat.RGBA32, false);

            string nombre = Capturas.NombreSeguro(o.salida, o.id);

            // Siempre se guardan los cuadros como JPEG concatenados (.mjpeg): no depende de códecs
            actual.archivoCuadros = Path.Combine(Puente.DirVideos, nombre + ".mjpeg");
            cuadrosMjpeg = new FileStream(actual.archivoCuadros, FileMode.Create, FileAccess.Write);

            // Además se intenta un video listo para ver: WebM (VP8) en Linux, MP4 (H.264) en Windows/macOS
            bool linux = Application.platform == RuntimePlatform.LinuxEditor;
            actual.archivoVideo = o.sinwebm ? "" : Path.Combine(Puente.DirVideos, nombre + (linux ? ".webm" : ".mp4"));
            if (!o.sinwebm)
            try
            {
                VideoTrackEncoderAttributes video = linux
                    ? new VideoTrackEncoderAttributes(new VP8EncoderAttributes { keyframeDistance = 30 })
                    : new VideoTrackEncoderAttributes(new H264EncoderAttributes { gopSize = 30, numConsecutiveBFrames = 0, profile = VideoEncodingProfile.H264High });
                video.frameRate = new MediaRational(fps);
                video.width = (uint)ancho;
                video.height = (uint)alto;
                video.targetBitRate = 8000000;
                video.bitRateMode = VideoBitrateMode.High;
                video.includeAlpha = false;
                if (File.Exists(actual.archivoVideo)) File.Delete(actual.archivoVideo);
                codificador = new MediaEncoder(actual.archivoVideo, video);
            }
            catch (Exception e)
            {
                actual.notas += "sin codificador de video: " + e.Message + "\n";
                codificador = null;
                actual.archivoVideo = "";
            }

            Time.captureFramerate = fps;   // cada cuadro avanza exactamente 1/fps de tiempo de juego
            ultimoCuadro = -1;
            siguienteAccion = 0;
            capturando = true;
            actual.cuadros = 0;
            actual.fase = 2;
            Guardar();
        }

        private static void CerrarCaptura()
        {
            capturando = false;
            try { if (codificador != null) codificador.Dispose(); } catch (Exception) { }
            codificador = null;
            try { if (cuadrosMjpeg != null) cuadrosMjpeg.Dispose(); } catch (Exception) { }
            cuadrosMjpeg = null;
            if (rt != null) { rt.Release(); UnityEngine.Object.DestroyImmediate(rt); rt = null; }
            if (tex != null) { UnityEngine.Object.DestroyImmediate(tex); tex = null; }
            camara = null;
        }
    }
}
