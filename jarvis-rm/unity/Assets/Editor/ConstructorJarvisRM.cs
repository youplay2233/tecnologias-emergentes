using System.Linq;
using UnityEditor;
using UnityEngine;

namespace JarvisRMEditor
{
    // Arma la escena Assets/Escenas/Jarvis.unity desde cero. Casi todo el HUD lo construyen los scripts
    // al iniciar (JarvisHUD); aquí solo va lo que necesita el editor: materiales, rig de Meta y piso.
    //   Menú: Jarvis > Construir escena
    //   Terminal: Unity -batchmode -projectPath ~/jarvis-rm -executeMethod JarvisRMEditor.ConstructorJarvisRM.Construir -quit
    public static class ConstructorJarvisRM
    {
        public const string Escena = "Jarvis";

        [MenuItem("Jarvis/Construir escena", priority = 1)]
        public static void Construir()
        {
            var escena = TE.NuevaEscena();

            // Materiales en Assets/Resources/JarvisRM: los scripts los cargan con Resources.Load,
            // y por estar ahí entran a la build con sus variantes de shader (Fade, Emission).
            TE.MatEstela("Holo_Linea", Color.white);
            TE.MatTransparente("Holo_Panel", new Color(0.02f, 0.1f, 0.14f, 0.55f));
            TE.Mat("Pieza_Base", new Color(0.8f, 0.82f, 0.85f), 0.45f, 0.3f);
            TE.MatEmisivo("Pieza_Resalte", new Color(0.3f, 0.85f, 1f), new Color(0.15f, 0.8f, 1.2f));
            Object.DestroyImmediate(TE.Etiqueta("x", null, Vector3.zero, 0.05f, Color.white).gameObject);   // crea Texto3D.mat

            // Rig de Meta con passthrough y manos que agarran (ManoRM), más la cámara de simulación del editor
            GameObject rm = TE.InfraestructuraRM(new Vector3(0f, 1.15f, 1.1f), 2.4f, 1.55f, 30f, 180f);
            Transform derecha = rm.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == "RightHandAnchor");
            if (derecha != null) derecha.gameObject.AddComponent<PunteroRM>();
            else Debug.LogWarning("[JarvisRM] No encontré RightHandAnchor: la tableta solo se podrá usar con el ratón.");
#if TEC_META_XR
            // Seguimiento de manos: pinza para agarrar y punta del índice para tocar botones
            AgregarManoSeguida(rm, "LeftHandAnchor", false);
            AgregarManoSeguida(rm, "RightHandAnchor", true);
            // Manos y controles a la vez: con un control en una mano, la otra se sigue como mano (la mano espejo
            // sigue funcionando mientras el otro control mueve el modelo)
            OVRManager manager = rm.GetComponentsInChildren<OVRManager>(true).FirstOrDefault();
            if (manager != null) manager.launchSimultaneousHandsControllersOnStartup = true;
            else Debug.LogWarning("[JarvisRM] No encontré OVRManager para activar manos y controles a la vez.");
#endif

            // Piso invisible a la altura del piso real (y = 0): lo que Jarvis crea o lanza cae sobre él
            GameObject piso = TE.Vacio("Piso_invisible", null, new Vector3(0f, -0.05f, 0f));
            piso.AddComponent<BoxCollider>().size = new Vector3(400f, 0.1f, 400f);

            new GameObject("Jarvis").AddComponent<JarvisHUD>();

            TE.GuardarEscena(escena, Escena);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(TE.RutaEscena(Escena), true) };
            AssetDatabase.SaveAssets();
            Debug.Log("[JarvisRM] Escena construida: " + TE.RutaEscena(Escena));
        }

#if TEC_META_XR
        private const string PrefabMano = "Packages/com.meta.xr.sdk.core/Prefabs/OVRHandPrefab.prefab";

        private static void AgregarManoSeguida(GameObject rm, string ancla, bool derecha)
        {
            Transform t = rm.GetComponentsInChildren<Transform>(true).FirstOrDefault(x => x.name == ancla);
            ManoRM manoRM = t != null ? t.GetComponent<ManoRM>() : null;
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabMano);
            if (manoRM == null || prefab == null)
            {
                Debug.LogWarning("[JarvisRM] No pude agregar el seguimiento de manos en " + ancla + ".");
                return;
            }

            GameObject go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, t);
            PrefabUtility.UnpackPrefabInstance(go, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            go.name = derecha ? "Mano_derecha" : "Mano_izquierda";

            OVRHand mano = go.GetComponent<OVRHand>();
            OVRSkeleton esqueleto = go.GetComponent<OVRSkeleton>();
            SerializedObject so = new SerializedObject(mano);
            so.FindProperty("HandType").intValue = (int)(derecha ? OVRHand.Hand.HandRight : OVRHand.Hand.HandLeft);
            so.ApplyModifiedPropertiesWithoutUndo();
            so = new SerializedObject(esqueleto);
            so.FindProperty("_skeletonType").intValue = (int)(derecha ? OVRSkeleton.SkeletonType.XRHandRight : OVRSkeleton.SkeletonType.XRHandLeft);
            so.ApplyModifiedPropertiesWithoutUndo();
            // Todo con el esqueleto OpenXR (OculusRuntimeSettings.handSkeletonVersion = OpenXR); si no coinciden,
            // OVRHand marca los datos como inválidos y el esqueleto no arranca
            OVRMesh malla = go.GetComponent<OVRMesh>();
            if (malla != null)
            {
                so = new SerializedObject(malla);
                so.FindProperty("_meshType").intValue = (int)(derecha ? OVRMesh.MeshType.XRHandRight : OVRMesh.MeshType.XRHandLeft);
                so.ApplyModifiedPropertiesWithoutUndo();
            }

            // Con passthrough ya se ven las manos reales: la malla virtual se apaga
            foreach (Renderer r in go.GetComponentsInChildren<Renderer>(true)) r.enabled = false;
            foreach (Behaviour b in go.GetComponentsInChildren<Behaviour>(true))
            {
                string tipo = b.GetType().Name;
                if (tipo == "OVRMeshRenderer" || tipo == "OVRSkeletonRenderer") b.enabled = false;
            }

            manoRM.mano = mano;
            manoRM.esqueleto = esqueleto;
        }
#endif

        // Ejecutable de escritorio para probar sin visor (cámara con ratón, sin passthrough)
        [MenuItem("Jarvis/Compilar para Linux (prueba sin visor)", priority = 20)]
        public static void CompilarLinux()
        {
            Compilar(BuildTarget.StandaloneLinux64, "Builds/Linux/JarvisRM.x86_64");
        }

        // APK para el Quest 3:  adb install -r Builds/JarvisRM.apk
        [MenuItem("Jarvis/Compilar APK para el Quest", priority = 21)]
        public static void CompilarQuest()
        {
            Compilar(BuildTarget.Android, "Builds/JarvisRM.apk");
        }

        private static void Compilar(BuildTarget destino, string ruta)
        {
            var opciones = new BuildPlayerOptions
            {
                scenes = new[] { TE.RutaEscena(Escena) },
                locationPathName = ruta,
                target = destino,
                options = BuildOptions.None,
            };
            var reporte = BuildPipeline.BuildPlayer(opciones);
            if (reporte.summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded)
                throw new System.Exception("[JarvisRM] La compilación falló: " + reporte.summary.result + " (" + reporte.summary.totalErrors + " errores)");
            Debug.Log("[JarvisRM] Compilado " + ruta + " (" + (reporte.summary.totalSize / 1048576f).ToString("0.0") + " MB)");
        }
    }
}
