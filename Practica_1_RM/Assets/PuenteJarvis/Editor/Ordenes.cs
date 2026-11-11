using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace PuenteJarvis
{
    // Órdenes que el cerebro puede mandarle a Unity.
    public static class Ordenes
    {
        // Devuelve true cuando la orden queda como trabajo largo (el resultado se escribe al terminar).
        public static bool Ejecutar(Orden o, Resultado r)
        {
            switch (o.cmd)
            {
                case "ping":
                    r.mensaje = "pong";
                    r.datos = "unity=" + Application.unityVersion + " editor=" + Application.platform + " objetivo=" + EditorUserBuildSettings.activeBuildTarget;
                    return false;

                case "refrescar":
                    // Importa archivos nuevos o cambiados; si hay scripts, Unity recompila después.
                    AssetDatabase.Refresh();
                    r.mensaje = "refresco pedido; compilando=" + EditorApplication.isCompiling;
                    return false;

                case "menu":
                    r.ok = EditorApplication.ExecuteMenuItem(o.nombre);
                    r.mensaje = r.ok ? "menú ejecutado: " + o.nombre : "no existe el menú: " + o.nombre;
                    return false;

                case "llamar":
                    r.datos = Llamar(o);
                    r.mensaje = o.tipo + "." + o.metodo + " ejecutado";
                    return false;

                case "simbolo":
                    r.mensaje = Puente.GestionarSimboloMeta();
                    return false;

                case "abrir_escena":
                    RequiereEdicion();
                    Scene escena = EditorSceneManager.OpenScene(o.ruta, OpenSceneMode.Single);
                    r.mensaje = "escena abierta: " + escena.path + " (" + escena.rootCount + " objetos raíz)";
                    return false;

                case "guardar_escena":
                    RequiereEdicion();
                    r.ok = EditorSceneManager.SaveOpenScenes();
                    r.mensaje = "escenas guardadas=" + r.ok;
                    return false;

                case "jerarquia":
                    r.datos = Inspeccion.Jerarquia(o.nombre, o.ejecutar);
                    r.mensaje = "jerarquía de " + SceneManager.GetActiveScene().name;
                    return false;

                case "seleccionar":
                    GameObject objetivo = Inspeccion.Buscar(o.nombre);
                    if (objetivo == null) throw new Exception("no encontré el objeto: " + o.nombre);
                    Selection.activeGameObject = objetivo;
                    EditorGUIUtility.PingObject(objetivo);
                    r.mensaje = "seleccionado: " + Inspeccion.Ruta(objetivo.transform);
                    return false;

                case "captura":
                    r.datos = Capturas.Camara(o);
                    r.mensaje = "captura guardada";
                    return false;

                case "ventana":
                    Trabajos.Iniciar("ventana", o);
                    return true;

                case "grabar":
                    Trabajos.Iniciar("grabar", o);
                    return true;

                case "play":
                    EditorApplication.isPlaying = o.ejecutar;
                    r.mensaje = "play=" + o.ejecutar;
                    return false;

                case "plataforma":
                    r.mensaje = CambiarPlataforma(o.nombre);
                    return false;

                case "paquetes":
                    r.datos = Paquetes();
                    r.mensaje = "paquetes registrados";
                    return false;

                case "compilar_apk":
                    r.mensaje = CompilarApk(o, r);
                    return false;

                default:
                    r.ok = false;
                    r.mensaje = "orden desconocida: " + o.cmd;
                    return false;
            }
        }

        private static void RequiereEdicion()
        {
            if (EditorApplication.isPlaying) throw new Exception("Unity está en Play; detén la reproducción primero.");
        }

        // Llama a un método estático por reflexión: {tipo:"Espacio.Clase", metodo:"Metodo", args:["a","b"]}
        private static string Llamar(Orden o)
        {
            Type tipo = null;
            foreach (Assembly a in AppDomain.CurrentDomain.GetAssemblies())
            {
                tipo = a.GetType(o.tipo, false);
                if (tipo != null) break;
            }
            if (tipo == null) throw new Exception("no existe el tipo " + o.tipo + " (¿falló la compilación de sus scripts?)");

            string[] args = o.args ?? new string[0];
            MethodInfo[] candidatos = tipo.GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
                .Where(m => m.Name == o.metodo).ToArray();
            if (candidatos.Length == 0) throw new Exception("no existe el método " + o.tipo + "." + o.metodo);

            foreach (MethodInfo m in candidatos)
            {
                ParameterInfo[] p = m.GetParameters();
                object[] valores = null;
                if (p.Length == 0 && args.Length == 0) valores = new object[0];
                else if (p.Length == 1 && p[0].ParameterType == typeof(string[])) valores = new object[] { args };
                else if (p.Length == args.Length && p.All(x => x.ParameterType == typeof(string))) valores = args.Cast<object>().ToArray();
                if (valores == null) continue;
                object devuelto = m.Invoke(null, valores);
                return devuelto == null ? "" : devuelto.ToString();
            }
            throw new Exception("ninguna variante de " + o.metodo + " acepta " + args.Length + " argumentos de texto");
        }

        private static string CambiarPlataforma(string nombre)
        {
            RequiereEdicion();
            BuildTargetGroup grupo;
            BuildTarget objetivo;
            if (string.Equals(nombre, "Android", StringComparison.OrdinalIgnoreCase))
            {
                grupo = BuildTargetGroup.Android;
                objetivo = BuildTarget.Android;
            }
            else
            {
                grupo = BuildTargetGroup.Standalone;
                objetivo = Application.platform == RuntimePlatform.LinuxEditor ? BuildTarget.StandaloneLinux64
                    : (Application.platform == RuntimePlatform.OSXEditor ? BuildTarget.StandaloneOSX : BuildTarget.StandaloneWindows64);
            }
            if (!BuildPipeline.IsBuildTargetSupported(grupo, objetivo))
                throw new Exception("el módulo de " + objetivo + " no está instalado en este editor (Unity Hub > Installs > Add modules)");
            if (EditorUserBuildSettings.activeBuildTarget == objetivo) return "ya estaba en " + objetivo;
            bool ok = EditorUserBuildSettings.SwitchActiveBuildTarget(grupo, objetivo);
            return "cambio a " + objetivo + " = " + ok;
        }

        private static string Paquetes()
        {
            StringBuilder sb = new StringBuilder();
            foreach (UnityEditor.PackageManager.PackageInfo p in UnityEditor.PackageManager.PackageInfo.GetAllRegisteredPackages())
            {
                if (p.name.StartsWith("com.unity.modules.")) continue;
                sb.Append(p.name).Append('@').Append(p.version).Append("  [").Append(p.source).Append("]\n");
            }
            return sb.ToString();
        }

        private static string CompilarApk(Orden o, Resultado r)
        {
            RequiereEdicion();
            if (!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Android, BuildTarget.Android))
                throw new Exception("falta el módulo Android Build Support en este editor");

            string[] escenas = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
            if (escenas.Length == 0) throw new Exception("no hay escenas en Build Settings");

            string destino = string.IsNullOrEmpty(o.ruta) ? "Builds/Practica_1_RM.apk" : o.ruta;
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(destino)));

            BuildPlayerOptions opciones = new BuildPlayerOptions
            {
                scenes = escenas,
                locationPathName = destino,
                target = BuildTarget.Android,
                targetGroup = BuildTargetGroup.Android,
                options = o.ejecutar ? BuildOptions.AutoRunPlayer : BuildOptions.None
            };
            BuildReport reporte = BuildPipeline.BuildPlayer(opciones);
            BuildSummary s = reporte.summary;
            r.ok = s.result == BuildResult.Succeeded;

            StringBuilder sb = new StringBuilder();
            foreach (BuildStep paso in reporte.steps)
            {
                foreach (BuildStepMessage m in paso.messages)
                {
                    if (m.type == LogType.Error || m.type == LogType.Exception) sb.Append(paso.name).Append(": ").Append(m.content).Append('\n');
                }
            }
            r.datos = sb.ToString();
            return "resultado=" + s.result + " tamaño=" + (s.totalSize / 1048576) + "MB tiempo=" + s.totalTime + " errores=" + s.totalErrors + " apk=" + destino;
        }
    }

    // Lectura de la escena como texto (para verificar y documentar sin capturas).
    public static class Inspeccion
    {
        public static GameObject Buscar(string nombre)
        {
            if (string.IsNullOrEmpty(nombre)) return null;
            GameObject directo = GameObject.Find(nombre);
            if (directo != null) return directo;
            // GameObject.Find no ve objetos inactivos: se recorre la escena completa
            foreach (GameObject raiz in SceneManager.GetActiveScene().GetRootGameObjects())
            {
                foreach (Transform t in raiz.GetComponentsInChildren<Transform>(true))
                {
                    if (t.name == nombre || Ruta(t) == nombre) return t.gameObject;
                }
            }
            return null;
        }

        public static string Ruta(Transform t)
        {
            string ruta = t.name;
            while (t.parent != null)
            {
                t = t.parent;
                ruta = t.name + "/" + ruta;
            }
            return ruta;
        }

        // nombre vacío = toda la escena; detalle = incluir los valores serializados de cada componente
        public static string Jerarquia(string nombre, bool detalle)
        {
            StringBuilder sb = new StringBuilder();
            if (string.IsNullOrEmpty(nombre))
            {
                foreach (GameObject raiz in SceneManager.GetActiveScene().GetRootGameObjects()) Volcar(raiz.transform, 0, detalle, sb);
            }
            else
            {
                GameObject go = Buscar(nombre);
                if (go == null) throw new Exception("no encontré el objeto: " + nombre);
                Volcar(go.transform, 0, detalle, sb);
            }
            return sb.ToString();
        }

        private static void Volcar(Transform t, int nivel, bool detalle, StringBuilder sb)
        {
            if (sb.Length > 400000) return;
            string sangria = new string(' ', nivel * 2);
            sb.Append(sangria).Append(t.name);
            if (!t.gameObject.activeSelf) sb.Append(" (inactivo)");
            if (t.gameObject.tag != "Untagged") sb.Append(" #").Append(t.gameObject.tag);
            sb.Append("  pos").Append(V(t.localPosition)).Append(" rot").Append(V(t.localEulerAngles)).Append(" esc").Append(V(t.localScale));

            List<string> comps = new List<string>();
            foreach (Component c in t.GetComponents<Component>())
            {
                if (c == null) { comps.Add("(script perdido)"); continue; }
                if (c is Transform) continue;
                comps.Add(detalle ? Detalle(c) : c.GetType().Name);
            }
            if (comps.Count > 0) sb.Append("  {").Append(string.Join(", ", comps.ToArray())).Append('}');
            sb.Append('\n');

            for (int i = 0; i < t.childCount; i++) Volcar(t.GetChild(i), nivel + 1, detalle, sb);
        }

        private static string Detalle(Component c)
        {
            string nombre = c.GetType().Name;
            if (c is MeshFilter || c is MeshRenderer) return nombre;

            StringBuilder sb = new StringBuilder(nombre);
            SerializedObject so = new SerializedObject(c);
            SerializedProperty p = so.GetIterator();
            int cuenta = 0;
            bool entrar = true;
            sb.Append('(');
            while (p.NextVisible(entrar) && cuenta < 24)
            {
                entrar = false;
                if (p.name == "m_Script" || p.name == "m_Material" && c is Collider) continue;
                string valor = Valor(p);
                if (valor == null) continue;
                if (cuenta > 0) sb.Append("; ");
                sb.Append(p.displayName).Append('=').Append(valor);
                cuenta++;
            }
            sb.Append(')');
            return sb.ToString();
        }

        private static string Valor(SerializedProperty p)
        {
            switch (p.propertyType)
            {
                case SerializedPropertyType.Integer: return p.intValue.ToString();
                case SerializedPropertyType.Boolean: return p.boolValue ? "sí" : "no";
                case SerializedPropertyType.Float: return p.floatValue.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture);
                case SerializedPropertyType.String: return "\"" + (p.stringValue.Length > 60 ? p.stringValue.Substring(0, 60) + "…" : p.stringValue).Replace("\n", "\\n") + "\"";
                case SerializedPropertyType.Enum: return p.enumValueIndex >= 0 && p.enumValueIndex < p.enumDisplayNames.Length ? p.enumDisplayNames[p.enumValueIndex] : p.intValue.ToString();
                case SerializedPropertyType.ObjectReference: return p.objectReferenceValue == null ? "(ninguno)" : p.objectReferenceValue.name;
                case SerializedPropertyType.Vector2: return V(p.vector2Value);
                case SerializedPropertyType.Vector3: return V(p.vector3Value);
                case SerializedPropertyType.Color: Color col = p.colorValue; return "rgba(" + col.r.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture) + "," + col.g.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture) + "," + col.b.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture) + "," + col.a.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture) + ")";
                default: return null;
            }
        }

        private static string V(Vector3 v)
        {
            return "(" + v.x.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture) + ", " + v.y.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture) + ", " + v.z.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture) + ")";
        }

        private static string V(Vector2 v)
        {
            return "(" + v.x.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture) + ", " + v.y.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture) + ")";
        }
    }
}
