using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.Compilation;
using UnityEngine;
using UnityEngine.SceneManagement;

// PUENTE JARVIS
// -------------
// Conecta al "cerebro" (Claude) con el editor de Unity por medio de archivos:
//
//   <proyecto>/PuenteJarvis/entrada/*.json   órdenes que llegan (una por archivo)
//   <proyecto>/PuenteJarvis/salida/*.json    resultado de cada orden
//   <proyecto>/PuenteJarvis/estado.json      latido: qué está haciendo Unity ahora
//   <proyecto>/PuenteJarvis/registro.log     lo que sale en la consola de Unity
//   <proyecto>/PuenteJarvis/compilacion.json errores de compilación de los scripts
//
// Este ensamblado no depende de ningún otro script ni paquete del proyecto: si algo más
// deja de compilar, el puente sigue vivo y reporta el error.
namespace PuenteJarvis
{
    [Serializable]
    public class Accion
    {
        public float t;          // segundo de la grabación en que se ejecuta
        public string objetivo;  // nombre del GameObject
        public string mensaje;   // método a llamar con SendMessage
        public string arg;       // argumento opcional (texto)
    }

    [Serializable]
    public class Orden
    {
        public string id;
        public string cmd;
        public string ruta;
        public string nombre;
        public string tipo;
        public string metodo;
        public string[] args;
        public int ancho;
        public int alto;
        public int fps;
        public float segundos;
        public string salida;
        public float[] pos;
        public float[] mirar;
        public float fov;
        public bool voltear;
        public bool ejecutar;
        public bool sinwebm;     // grabar: solo los cuadros JPEG (ffmpeg arma el MP4); sin el WebM cada cuadro tarda menos
        public Accion[] acciones;
    }

    [Serializable]
    public class Resultado
    {
        public string id;
        public string cmd;
        public bool ok;
        public string mensaje;
        public string datos;
        public string hora;
    }

    [Serializable]
    public class Estado
    {
        public string hora;
        public string evento;
        public string unity;
        public string editor;
        public string plataformaActiva;
        public string escenaActiva;
        public bool compilando;
        public bool importando;
        public bool reproduciendo;
        public bool compilacionFallida;
        public bool androidInstalado;
        public bool metaXR;
        public int recargas;
        public int ordenesProcesadas;
        public int erroresDeCompilacion;
        public string trabajo;
    }

    [Serializable]
    public class MensajeCompilacion
    {
        public string ensamblado;
        public string tipo;
        public string archivo;
        public int linea;
        public int columna;
        public string mensaje;
    }

    [Serializable]
    public class ReporteCompilacion
    {
        public string hora;
        public bool terminada;
        public int errores;
        public int advertencias;
        public List<MensajeCompilacion> mensajes = new List<MensajeCompilacion>();
    }

    [InitializeOnLoad]
    public static class Puente
    {
        public const string GuidRigMeta = "126d619cf4daa52469682f85c1378b4a"; // OVRCameraRig.prefab
        public const string SimboloMeta = "TEC_META_XR";

        public static string Raiz { get; private set; }
        public static string DirEntrada { get; private set; }
        public static string DirSalida { get; private set; }
        public static string DirCapturas { get; private set; }
        public static string DirVideos { get; private set; }

        private static readonly object candado = new object();
        private static readonly List<string> colaRegistro = new List<string>();
        private static readonly Dictionary<string, int> intentos = new Dictionary<string, int>();
        private static ReporteCompilacion compilacion = new ReporteCompilacion();
        private static double proximoTick;
        private static double proximoEstado;
        private static int recargas;
        private static string ultimoEvento = "inicio";

        static Puente()
        {
            try
            {
                Raiz = Path.Combine(Path.GetDirectoryName(Application.dataPath), "PuenteJarvis");
                DirEntrada = Path.Combine(Raiz, "entrada");
                DirSalida = Path.Combine(Raiz, "salida");
                DirCapturas = Path.Combine(Raiz, "capturas");
                DirVideos = Path.Combine(Raiz, "videos");
                Directory.CreateDirectory(DirEntrada);
                Directory.CreateDirectory(DirSalida);
                Directory.CreateDirectory(DirCapturas);
                Directory.CreateDirectory(DirVideos);

                recargas = SessionState.GetInt("PJ.recargas", 0) + 1;
                SessionState.SetInt("PJ.recargas", recargas);

                Application.logMessageReceivedThreaded += AlRegistrar;
                CompilationPipeline.compilationStarted += AlIniciarCompilacion;
                CompilationPipeline.assemblyCompilationFinished += AlCompilarEnsamblado;
                CompilationPipeline.compilationFinished += AlTerminarCompilacion;
                EditorApplication.update += Tick;
                EditorApplication.playModeStateChanged += cambio => { ultimoEvento = "play:" + cambio; EscribirEstado(); };

                Trabajos.Restaurar();
                EscribirEstado();
                EditorApplication.delayCall += AlCargar;
            }
            catch (Exception e)
            {
                Debug.LogError("[PuenteJarvis] No pudo iniciar: " + e);
            }
        }

        // ------------------------------------------------------------------ arranque

        private static void AlCargar()
        {
            try
            {
                GestionarSimboloMeta();

                // Si Unity abrió con errores de compilación, se vuelve a compilar una sola vez
                // para que los errores lleguen a compilacion.json.
                if (EditorUtility.scriptCompilationFailed && !SessionState.GetBool("PJ.recompilado", false))
                {
                    SessionState.SetBool("PJ.recompilado", true);
                    CompilationPipeline.RequestScriptCompilation();
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning("[PuenteJarvis] AlCargar: " + e.Message);
            }
        }

        public static bool HayMetaXR()
        {
            return !string.IsNullOrEmpty(AssetDatabase.GUIDToAssetPath(GuidRigMeta));
        }

        // Los scripts de las prácticas usan OVRInput solo si el símbolo TEC_META_XR está definido.
        // Aquí se mantiene sincronizado con la presencia real del Meta XR Core SDK.
        public static string GestionarSimboloMeta()
        {
            bool hay = HayMetaXR();
            string cambios = "";
            foreach (BuildTargetGroup grupo in new[] { BuildTargetGroup.Android, BuildTargetGroup.Standalone })
            {
                string actual = PlayerSettings.GetScriptingDefineSymbolsForGroup(grupo);
                List<string> lista = actual.Split(';').Select(s => s.Trim()).Where(s => s.Length > 0).ToList();
                bool tiene = lista.Contains(SimboloMeta);
                if (hay && !tiene) lista.Add(SimboloMeta);
                else if (!hay && tiene) lista.Remove(SimboloMeta);
                else continue;
                PlayerSettings.SetScriptingDefineSymbolsForGroup(grupo, string.Join(";", lista.ToArray()));
                cambios += grupo + (hay ? ":+ " : ":- ");
            }
            if (cambios.Length > 0) Debug.Log("[PuenteJarvis] Símbolo " + SimboloMeta + " -> " + cambios + "(Meta XR SDK " + (hay ? "presente" : "ausente") + ")");
            return "metaXR=" + hay + " cambios=" + cambios;
        }

        // ------------------------------------------------------------------ registro de consola

        private static void AlRegistrar(string condicion, string pila, LogType tipo)
        {
            string letra = tipo == LogType.Log ? "L" : (tipo == LogType.Warning ? "W" : "E");
            StringBuilder sb = new StringBuilder();
            sb.Append('[').Append(DateTime.Now.ToString("HH:mm:ss")).Append("] [").Append(letra).Append("] ").Append(condicion);
            if (letra == "E" && !string.IsNullOrEmpty(pila))
            {
                string[] lineas = pila.Split('\n');
                for (int i = 0; i < lineas.Length && i < 8; i++)
                {
                    if (lineas[i].Trim().Length > 0) sb.Append("\n      ").Append(lineas[i].Trim());
                }
            }
            lock (candado)
            {
                if (colaRegistro.Count < 4000) colaRegistro.Add(sb.ToString());
            }
        }

        private static void VaciarRegistro()
        {
            string[] lineas;
            lock (candado)
            {
                if (colaRegistro.Count == 0) return;
                lineas = colaRegistro.ToArray();
                colaRegistro.Clear();
            }
            try
            {
                string archivo = Path.Combine(Raiz, "registro.log");
                FileInfo info = new FileInfo(archivo);
                if (info.Exists && info.Length > 1500000)
                {
                    string anterior = Path.Combine(Raiz, "registro.anterior.log");
                    if (File.Exists(anterior)) File.Delete(anterior);
                    File.Move(archivo, anterior);
                }
                File.AppendAllText(archivo, string.Join("\n", lineas) + "\n", Encoding.UTF8);
            }
            catch (Exception)
            {
                // El registro nunca debe tumbar al puente
            }
        }

        // ------------------------------------------------------------------ compilación

        private static void AlIniciarCompilacion(object contexto)
        {
            compilacion = new ReporteCompilacion();
            ultimoEvento = "compilando";
        }

        private static void AlCompilarEnsamblado(string ensamblado, CompilerMessage[] mensajes)
        {
            foreach (CompilerMessage m in mensajes)
            {
                bool esError = m.type == CompilerMessageType.Error;
                if (esError) compilacion.errores++;
                else compilacion.advertencias++;
                if (!esError && compilacion.mensajes.Count > 60) continue;
                compilacion.mensajes.Add(new MensajeCompilacion
                {
                    ensamblado = Path.GetFileName(ensamblado),
                    tipo = esError ? "error" : "advertencia",
                    archivo = m.file,
                    linea = m.line,
                    columna = m.column,
                    mensaje = m.message
                });
            }
        }

        private static void AlTerminarCompilacion(object contexto)
        {
            compilacion.terminada = true;
            compilacion.hora = Ahora();
            EscribirJson(Path.Combine(Raiz, "compilacion.json"), JsonUtility.ToJson(compilacion, true));
            ultimoEvento = compilacion.errores > 0 ? "compilacion_con_errores" : "compilacion_ok";
            EscribirEstado();
        }

        // ------------------------------------------------------------------ bucle principal

        private static void Tick()
        {
            double ahora = EditorApplication.timeSinceStartup;

            // Las grabaciones necesitan correr en cada cuadro
            try
            {
                Trabajos.CadaCuadro();
            }
            catch (Exception e)
            {
                Trabajos.Fallar("excepción: " + e);
            }

            if (ahora < proximoTick) return;
            proximoTick = ahora + 0.25;

            VaciarRegistro();

            if (ahora >= proximoEstado)
            {
                proximoEstado = ahora + 3.0;
                EscribirEstado();
            }

            if (EditorApplication.isCompiling || EditorApplication.isUpdating) return;

            try
            {
                Trabajos.CadaTick();
                if (!Trabajos.HayTrabajo) ProcesarEntrada();
            }
            catch (Exception e)
            {
                Debug.LogError("[PuenteJarvis] " + e);
            }
        }

        private static void ProcesarEntrada()
        {
            string[] archivos = Directory.GetFiles(DirEntrada, "*.json");
            if (archivos.Length == 0) return;
            Array.Sort(archivos, StringComparer.Ordinal);

            foreach (string archivo in archivos)
            {
                Orden orden = null;
                try
                {
                    orden = JsonUtility.FromJson<Orden>(File.ReadAllText(archivo, Encoding.UTF8));
                }
                catch (Exception)
                {
                    orden = null;
                }

                if (orden == null || string.IsNullOrEmpty(orden.cmd))
                {
                    // Puede estar a medio escribir: se reintenta unos segundos y luego se aparta
                    int n;
                    intentos.TryGetValue(archivo, out n);
                    intentos[archivo] = n + 1;
                    if (n > 40)
                    {
                        string mal = Path.Combine(DirEntrada, "ilegibles");
                        Directory.CreateDirectory(mal);
                        string destino = Path.Combine(mal, Path.GetFileName(archivo));
                        if (File.Exists(destino)) File.Delete(destino);
                        File.Move(archivo, destino);
                        intentos.Remove(archivo);
                    }
                    continue;
                }

                if (string.IsNullOrEmpty(orden.id)) orden.id = Path.GetFileNameWithoutExtension(archivo);
                File.Delete(archivo);
                SessionState.SetInt("PJ.ordenes", SessionState.GetInt("PJ.ordenes", 0) + 1);
                ultimoEvento = "orden:" + orden.cmd;

                Resultado r = new Resultado { id = orden.id, cmd = orden.cmd, ok = true };
                bool diferido = false;
                try
                {
                    diferido = Ordenes.Ejecutar(orden, r);
                }
                catch (Exception e)
                {
                    Exception real = e.InnerException ?? e;
                    r.ok = false;
                    r.mensaje = real.GetType().Name + ": " + real.Message + "\n" + real.StackTrace;
                }
                if (!diferido) EscribirResultado(r);

                // Una orden a la vez cuando puede haber recarga de dominio o trabajo largo
                if (diferido || Trabajos.HayTrabajo || EditorApplication.isCompiling) break;
            }
        }

        // ------------------------------------------------------------------ utilidades

        public static string Ahora()
        {
            return DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        }

        public static void EscribirResultado(Resultado r)
        {
            r.hora = Ahora();
            EscribirJson(Path.Combine(DirSalida, r.id + ".json"), JsonUtility.ToJson(r, true));
        }

        public static void EscribirEstado()
        {
            try
            {
                Estado e = new Estado
                {
                    hora = Ahora(),
                    evento = ultimoEvento,
                    unity = Application.unityVersion,
                    editor = Application.platform.ToString(),
                    plataformaActiva = EditorUserBuildSettings.activeBuildTarget.ToString(),
                    escenaActiva = SceneManager.GetActiveScene().path,
                    compilando = EditorApplication.isCompiling,
                    importando = EditorApplication.isUpdating,
                    reproduciendo = EditorApplication.isPlaying,
                    compilacionFallida = EditorUtility.scriptCompilationFailed,
                    androidInstalado = BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Android, BuildTarget.Android),
                    metaXR = HayMetaXR(),
                    recargas = recargas,
                    ordenesProcesadas = SessionState.GetInt("PJ.ordenes", 0),
                    erroresDeCompilacion = compilacion.errores,
                    trabajo = Trabajos.Descripcion
                };
                EscribirJson(Path.Combine(Raiz, "estado.json"), JsonUtility.ToJson(e, true));
            }
            catch (Exception)
            {
                // sin estado en este tick
            }
        }

        public static void EscribirJson(string destino, string contenido)
        {
            string temporal = destino + ".tmp";
            File.WriteAllText(temporal, contenido, new UTF8Encoding(false));
            if (File.Exists(destino)) File.Delete(destino);
            File.Move(temporal, destino);
        }
    }
}
