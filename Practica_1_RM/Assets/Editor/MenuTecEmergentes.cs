using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace TecEmergentes
{
    // Menú "Tecnologías Emergentes" y configuración automática al abrir el proyecto.
    [InitializeOnLoad]
    public static class MenuTecEmergentes
    {
        private const string ClaveIntentoAndroid = "TE.intentoAndroid";
        private const string ClaveConstruido = "TE.construidoEnEstaSesion";

        static MenuTecEmergentes()
        {
            EditorApplication.delayCall += PrimerArranque;
        }

        // Al abrir el proyecto por primera vez: ajusta XR para este editor, cambia a Android
        // (si el módulo está instalado) y construye las prácticas si todavía no existen.
        private static void PrimerArranque()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode)
            {
                EditorApplication.delayCall += PrimerArranque;
                return;
            }

            try
            {
                AjustarXRParaEsteEditor();

                bool faltanEscenas = !File.Exists(TE.RutaEscena(TE.Escenas[0]));
                if (!faltanEscenas) return;

                bool androidDisponible = BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Android, BuildTarget.Android);
                if (androidDisponible && EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android && !SessionState.GetBool(ClaveIntentoAndroid, false))
                {
                    SessionState.SetBool(ClaveIntentoAndroid, true);
                    Debug.Log("[TecEmergentes] Cambiando la plataforma a Android (Meta Quest 3)...");
                    EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Android, BuildTarget.Android);
                    EditorApplication.delayCall += PrimerArranque;
                    return;
                }

                if (SessionState.GetBool(ClaveConstruido, false)) return;
                SessionState.SetBool(ClaveConstruido, true);
                Debug.Log("[TecEmergentes] Primera apertura: construyendo las prácticas...");
                Debug.Log("[TecEmergentes] " + ConstructorPracticas.ConstruirTodo());
            }
            catch (Exception e)
            {
                Debug.LogError("[TecEmergentes] Configuración automática: " + e);
            }
        }

        // El editor de Linux no tiene runtime de OpenXR: se apaga "Initialize XR on Startup" para PC
        // (así Play no marca error) y se vuelve a encender en Windows, donde sí existe Quest Link.
        public static string AjustarXRParaEsteEditor()
        {
            const string ruta = "Assets/XR/XRGeneralSettings.asset";
            bool deseado = Application.platform != RuntimePlatform.LinuxEditor;
            foreach (UnityEngine.Object o in AssetDatabase.LoadAllAssetsAtPath(ruta))
            {
                if (o == null || o.name != "Standalone Settings") continue;
                SerializedObject so = new SerializedObject(o);
                SerializedProperty p = so.FindProperty("m_InitManagerOnStart");
                if (p == null) return "no encontré m_InitManagerOnStart";
                if (p.boolValue == deseado) return "XR en PC ya estaba en " + deseado;
                p.boolValue = deseado;
                so.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(o);
                AssetDatabase.SaveAssets();
                return "XR en PC (Initialize XR on Startup) = " + deseado;
            }
            return "no encontré 'Standalone Settings' en " + ruta;
        }

        [MenuItem("Tecnologías Emergentes/2. Preparar para Quest 3 (cambiar a Android)", priority = 2)]
        public static void PrepararQuest()
        {
            if (!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Android, BuildTarget.Android))
            {
                EditorUtility.DisplayDialog("Falta Android Build Support",
                    "Este editor no tiene instalado el módulo de Android.\n\nUnity Hub > Installs > (engrane de 2022.3.58f1) > Add modules > Android Build Support, con OpenJDK y Android SDK & NDK Tools.", "Entendido");
                return;
            }
            EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Android, BuildTarget.Android);
            ConstructorPracticas.ActualizarBuildSettings();
            Debug.Log("[TecEmergentes] Plataforma activa: " + EditorUserBuildSettings.activeBuildTarget);
        }

        [MenuItem("Tecnologías Emergentes/3. Compilar e instalar en el Quest 3 (cable USB)", priority = 3)]
        public static void CompilarEInstalarDesdeMenu()
        {
            Debug.Log("[TecEmergentes] " + Compilar(true));
        }

        // Genera Builds/Practica_1_RM.apk con las seis prácticas; con instalar = true lo manda al visor conectado.
        public static string Compilar(bool instalar)
        {
            if (EditorApplication.isPlaying) return "Detén la reproducción (Play) antes de compilar.";
            if (!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Android, BuildTarget.Android)) return "Falta el módulo Android Build Support.";
            ConstructorPracticas.ActualizarBuildSettings();
            string[] escenas = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
            if (escenas.Length == 0) return "No hay escenas: usa primero 'Construir todas las prácticas'.";

            Directory.CreateDirectory("Builds");
            BuildPlayerOptions opciones = new BuildPlayerOptions
            {
                scenes = escenas,
                locationPathName = "Builds/Practica_1_RM.apk",
                target = BuildTarget.Android,
                targetGroup = BuildTargetGroup.Android,
                options = instalar ? BuildOptions.AutoRunPlayer : BuildOptions.None
            };
            BuildReport reporte = BuildPipeline.BuildPlayer(opciones);
            BuildSummary s = reporte.summary;
            return "Compilación " + s.result + " · " + (s.totalSize / 1048576) + " MB · " + s.totalTime + " · errores: " + s.totalErrors;
        }

        [MenuItem("Tecnologías Emergentes/Abrir práctica/1 · Game Objects (objetos)", priority = 20)]
        private static void Abrir0() { Abrir(0); }

        [MenuItem("Tecnologías Emergentes/Abrir práctica/1 · Parque de diversiones", priority = 21)]
        private static void Abrir1() { Abrir(1); }

        [MenuItem("Tecnologías Emergentes/Abrir práctica/1 · Propuesta de proyecto final", priority = 22)]
        private static void Abrir2() { Abrir(2); }

        [MenuItem("Tecnologías Emergentes/Abrir práctica/3 · El Cañón Autónomo", priority = 23)]
        private static void Abrir3() { Abrir(3); }

        [MenuItem("Tecnologías Emergentes/Abrir práctica/Físicas S1 · Rigidbody y gravedad", priority = 24)]
        private static void Abrir4() { Abrir(4); }

        [MenuItem("Tecnologías Emergentes/Abrir práctica/Físicas S2 · Colliders y colisiones", priority = 25)]
        private static void Abrir5() { Abrir(5); }

        [MenuItem("Tecnologías Emergentes/Abrir práctica/Conexión de escenas · Inicio", priority = 26)]
        private static void Abrir7() { Abrir(7); }

        [MenuItem("Tecnologías Emergentes/Abrir práctica/Conectividad IoT · MQTT", priority = 27)]
        private static void Abrir10() { Abrir(10); }

        [MenuItem("Tecnologías Emergentes/Abrir práctica/Proyecto final · Jarvis en realidad mixta", priority = 40)]
        private static void Abrir6() { Abrir(6); }

        private static void Abrir(int indice)
        {
            if (EditorApplication.isPlaying) return;
            string ruta = TE.RutaEscena(TE.Escenas[indice]);
            if (!File.Exists(ruta))
            {
                Debug.LogWarning("[TecEmergentes] Aún no existe " + ruta + ". Usa 'Construir todas las prácticas'.");
                return;
            }
            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) EditorSceneManager.OpenScene(ruta, OpenSceneMode.Single);
        }
    }
}
