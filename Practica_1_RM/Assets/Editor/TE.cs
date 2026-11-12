using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace TecEmergentes
{
    // Utilidades con las que se arman las prácticas: son los mismos pasos que se hacen a mano
    // en el editor (GameObject > 3D Object, Create > Material, arrastrar al Project para hacer
    // un Prefab...), pero escritos en código para que el proyecto se construya solo.
    public static class TE
    {
        public const string CarpetaEscenas = "Assets/Scene";       // "Crea una carpeta llamada Scene"
        public const string CarpetaPrefabs = "Assets/Prefabs";
        public const string CarpetaMateriales = "Assets/Materials";

        public static readonly string[] Escenas =
        {
            "objetos", "Parque_diversiones", "Proyecto_final", "Canon_Autonomo", "Fisica_Sesion1", "Fisica_Sesion2", "Jarvis_RM"
        };

        public static readonly string[] Titulos =
        {
            "Práctica 1 · Game Objects",
            "Práctica 1 · Parque de diversiones",
            "Práctica 1 · Propuesta de proyecto final",
            "Práctica 3 · El Cañón Autónomo",
            "Físicas S1 · Rigidbody y gravedad",
            "Físicas S2 · Colliders y colisiones",
            "Proyecto final · Jarvis en realidad mixta"
        };

        public static string RutaEscena(string nombre)
        {
            return CarpetaEscenas + "/" + nombre + ".unity";
        }

        // ------------------------------------------------------------------ carpetas

        public static void AsegurarCarpeta(string ruta)
        {
            if (AssetDatabase.IsValidFolder(ruta)) return;
            string padre = Path.GetDirectoryName(ruta).Replace('\\', '/');
            AsegurarCarpeta(padre);
            AssetDatabase.CreateFolder(padre, Path.GetFileName(ruta));
        }

        // ------------------------------------------------------------------ materiales

        public static Material Mat(string nombre, Color color, float suavidad = 0.25f, float metalico = 0f)
        {
            return MatConShader(nombre, "Standard", m =>
            {
                m.color = color;
                m.SetFloat("_Glossiness", suavidad);
                m.SetFloat("_Metallic", metalico);
            });
        }

        // Material que deja ver el mundo real a través de él (pisos de las prácticas en realidad mixta)
        public static Material MatTransparente(string nombre, Color color)
        {
            return MatConShader(nombre, "Standard", m =>
            {
                m.color = color;
                m.SetFloat("_Glossiness", 0.1f);
                m.SetFloat("_Metallic", 0f);
                m.SetFloat("_Mode", 2f); // Fade
                m.SetOverrideTag("RenderType", "Transparent");
                m.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
                m.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
                m.SetInt("_ZWrite", 0);
                m.DisableKeyword("_ALPHATEST_ON");
                m.EnableKeyword("_ALPHABLEND_ON");
                m.DisableKeyword("_ALPHAPREMULTIPLY_ON");
                m.renderQueue = (int)RenderQueue.Transparent;
            });
        }

        public static Material MatEmisivo(string nombre, Color color, Color emision)
        {
            return MatConShader(nombre, "Standard", m =>
            {
                m.color = color;
                m.SetFloat("_Glossiness", 0.4f);
                m.SetFloat("_Metallic", 0f);
                m.EnableKeyword("_EMISSION");
                m.SetColor("_EmissionColor", emision);
                m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            });
        }

        // Para estelas (TrailRenderer)
        public static Material MatEstela(string nombre, Color color)
        {
            return MatConShader(nombre, "Sprites/Default", m => { m.color = color; });
        }

        private static Material MatConShader(string nombre, string shader, Action<Material> configurar)
        {
            AsegurarCarpeta(CarpetaMateriales);
            string ruta = CarpetaMateriales + "/" + nombre + ".mat";
            Shader s = Shader.Find(shader);
            if (s == null) throw new Exception("No encontré el shader " + shader);
            Material m = AssetDatabase.LoadAssetAtPath<Material>(ruta);
            if (m == null)
            {
                m = new Material(s);
                AssetDatabase.CreateAsset(m, ruta);
            }
            else
            {
                m.shader = s;
            }
            configurar(m);
            EditorUtility.SetDirty(m);
            return m;
        }

        // ------------------------------------------------------------------ objetos

        // Equivale a GameObject > 3D Object > (Cube, Sphere, ...) y luego editar su Transform.
        public static GameObject Prim(PrimitiveType tipo, string nombre, Transform padre, Vector3 pos, Vector3 rot, Vector3 escala, Material mat)
        {
            GameObject go = GameObject.CreatePrimitive(tipo);
            go.name = nombre;
            if (padre != null) go.transform.SetParent(padre, false);
            go.transform.localPosition = pos;
            go.transform.localEulerAngles = rot;
            go.transform.localScale = escala;
            if (mat != null) go.GetComponent<MeshRenderer>().sharedMaterial = mat;
            return go;
        }

        // Igual que Prim, pero sin Collider: para piezas de adorno que no participan en la física.
        public static GameObject Adorno(PrimitiveType tipo, string nombre, Transform padre, Vector3 pos, Vector3 rot, Vector3 escala, Material mat)
        {
            GameObject go = Prim(tipo, nombre, padre, pos, rot, escala, mat);
            Collider c = go.GetComponent<Collider>();
            if (c != null) UnityEngine.Object.DestroyImmediate(c);
            return go;
        }

        // Equivale a GameObject > Create Empty.
        public static GameObject Vacio(string nombre, Transform padre, Vector3 pos)
        {
            return Vacio(nombre, padre, pos, Vector3.zero);
        }

        public static GameObject Vacio(string nombre, Transform padre, Vector3 pos, Vector3 rot)
        {
            GameObject go = new GameObject(nombre);
            if (padre != null) go.transform.SetParent(padre, false);
            go.transform.localPosition = pos;
            go.transform.localEulerAngles = rot;
            return go;
        }

        // ------------------------------------------------------------------ texto 3D

        private static Font fuente;
        private static Material materialTexto;

        public static TextMesh Etiqueta(string texto, Transform padre, Vector3 pos, float altoDeLinea, Color color, TextAnchor ancla = TextAnchor.MiddleCenter, string nombre = "Etiqueta")
        {
            GameObject go = new GameObject(nombre);
            if (padre != null) go.transform.SetParent(padre, false);
            go.transform.localPosition = pos;

            TextMesh tm = go.AddComponent<TextMesh>();
            MeshRenderer mr = go.GetComponent<MeshRenderer>();
            if (mr == null) mr = go.AddComponent<MeshRenderer>();

            if (fuente == null) fuente = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (fuente != null)
            {
                tm.font = fuente;
                mr.sharedMaterial = MaterialDeTexto();
            }
            else
            {
                Debug.LogWarning("[TecEmergentes] No encontré la fuente integrada LegacyRuntime.ttf; el texto usa la fuente por defecto.");
            }
            mr.shadowCastingMode = ShadowCastingMode.Off;
            mr.receiveShadows = false;

            tm.text = texto;
            tm.richText = true;
            tm.fontSize = 80;                       // se rasteriza grande para que se lea nítido
            tm.characterSize = altoDeLinea / 9.2f;  // y se reduce hasta el alto de línea pedido (en metros)
            tm.anchor = ancla;
            tm.alignment = ancla == TextAnchor.UpperLeft || ancla == TextAnchor.MiddleLeft || ancla == TextAnchor.LowerLeft
                ? TextAlignment.Left
                : TextAlignment.Center;
            tm.color = color;
            return tm;
        }

        private static Material MaterialDeTexto()
        {
            if (materialTexto != null) return materialTexto;
            Shader s = Shader.Find("TecEmergentes/Texto3D");
            if (s == null)
            {
                Debug.LogWarning("[TecEmergentes] No está el shader TecEmergentes/Texto3D; se usa el material de fuente por defecto.");
                materialTexto = fuente.material;
                return materialTexto;
            }
            AsegurarCarpeta(CarpetaMateriales);
            string ruta = CarpetaMateriales + "/Texto3D.mat";
            Material m = AssetDatabase.LoadAssetAtPath<Material>(ruta);
            if (m == null)
            {
                m = new Material(s);
                AssetDatabase.CreateAsset(m, ruta);
            }
            m.shader = s;
            m.mainTexture = fuente.material.mainTexture;
            m.color = Color.white;
            EditorUtility.SetDirty(m);
            materialTexto = m;
            return materialTexto;
        }

        // ------------------------------------------------------------------ prefabs

        // Equivale a arrastrar el objeto de la Jerarquía a la carpeta Prefabs y borrarlo de la escena:
        // "Ahora solo existe en tus archivos".
        public static GameObject GuardarComoPrefab(GameObject temporal, string nombre)
        {
            AsegurarCarpeta(CarpetaPrefabs);
            string ruta = CarpetaPrefabs + "/" + nombre + ".prefab";
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(temporal, ruta);
            UnityEngine.Object.DestroyImmediate(temporal);
            if (prefab == null) throw new Exception("No se pudo guardar el prefab " + ruta);
            return prefab;
        }

        public static GameObject PrefabExistente(string nombre)
        {
            return AssetDatabase.LoadAssetAtPath<GameObject>(CarpetaPrefabs + "/" + nombre + ".prefab");
        }

        // Equivale a arrastrar el Prefab del Project a la escena: crea una INSTANCIA conectada al original.
        public static GameObject Instancia(GameObject prefab, string nombre, Transform padre, Vector3 pos, Vector3 rot)
        {
            GameObject go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            go.name = nombre;
            if (padre != null) go.transform.SetParent(padre, false);
            go.transform.localPosition = pos;
            go.transform.localEulerAngles = rot;
            RegistrarOverride(go);
            RegistrarOverride(go.transform);
            return go;
        }

        // Cuando se cambia por código una propiedad de una INSTANCIA de prefab hay que avisarle a Unity
        // para que la guarde como "override" (lo que en el Inspector se ve en negritas).
        public static void RegistrarOverride(UnityEngine.Object objeto)
        {
            if (objeto != null && PrefabUtility.IsPartOfPrefabInstance(objeto))
                PrefabUtility.RecordPrefabInstancePropertyModifications(objeto);
        }

        // ------------------------------------------------------------------ escenas

        public static Scene NuevaEscena()
        {
            Scene escena = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // Iluminación sencilla que no necesita "bake": luz direccional + ambiente de tres tonos
            RenderSettings.skybox = null;
            RenderSettings.fog = false;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.66f, 0.70f, 0.76f);
            RenderSettings.ambientEquatorColor = new Color(0.46f, 0.47f, 0.50f);
            RenderSettings.ambientGroundColor = new Color(0.26f, 0.25f, 0.24f);
            RenderSettings.defaultReflectionMode = DefaultReflectionMode.Custom;
            RenderSettings.reflectionIntensity = 0f;

            GameObject luz = new GameObject("Directional Light");
            Light l = luz.AddComponent<Light>();
            l.type = LightType.Directional;
            l.color = new Color(1f, 0.97f, 0.92f);
            l.intensity = 1.05f;
            l.shadows = LightShadows.Soft;
            l.shadowStrength = 0.7f;
            luz.transform.rotation = Quaternion.Euler(52f, -32f, 0f);
            return escena;
        }

        public static void GuardarEscena(Scene escena, string nombre)
        {
            AsegurarCarpeta(CarpetaEscenas);
            EditorSceneManager.MarkSceneDirty(escena);
            if (!EditorSceneManager.SaveScene(escena, RutaEscena(nombre)))
                throw new Exception("No se pudo guardar la escena " + nombre);
        }

        // ------------------------------------------------------------------ realidad mixta (Meta XR SDK)

        public const string GuidOVRCameraRig = "126d619cf4daa52469682f85c1378b4a";

        public static GameObject PrefabRigMeta()
        {
            string ruta = AssetDatabase.GUIDToAssetPath(GuidOVRCameraRig);
            if (string.IsNullOrEmpty(ruta))
            {
                foreach (string guid in AssetDatabase.FindAssets("OVRCameraRig t:Prefab", new[] { "Packages" }))
                {
                    string p = AssetDatabase.GUIDToAssetPath(guid);
                    if (p.Contains("com.meta.xr.sdk.core") && Path.GetFileNameWithoutExtension(p) == "OVRCameraRig")
                    {
                        ruta = p;
                        break;
                    }
                }
            }
            return string.IsNullOrEmpty(ruta) ? null : AssetDatabase.LoadAssetAtPath<GameObject>(ruta);
        }

        private static Type TipoPorNombre(string nombre)
        {
            return TypeCache.GetTypesDerivedFrom<MonoBehaviour>().FirstOrDefault(t => t.Name == nombre && string.IsNullOrEmpty(t.Namespace));
        }

        private static bool Poner(SerializedObject so, string propiedad, Action<SerializedProperty> asignar)
        {
            SerializedProperty p = so.FindProperty(propiedad);
            if (p == null)
            {
                Debug.LogWarning("[TecEmergentes] " + so.targetObject.GetType().Name + " no tiene la propiedad " + propiedad);
                return false;
            }
            asignar(p);
            return true;
        }

        // Todo lo que una práctica necesita para verse en el Quest 3:
        //   - OVRCameraRig de Meta con passthrough (equivale a los Building Blocks "Camera Rig" + "Passthrough")
        //   - manos que agarran y lanzan objetos
        //   - letrero con el nombre de la práctica y el cambio de escena con los botones X / Y
        //   - cámara de simulación para ver y grabar la práctica en el editor (no entra al visor)
        public static GameObject InfraestructuraRM(int indiceEscena, Vector3 objetivoCamara, float distanciaCamara, float alturaCamara, float amplitud = 35f, float anguloCentral = 180f)
        {
            GameObject raiz = new GameObject("RM (Realidad Mixta)");
            ArranqueRM arranque = raiz.AddComponent<ArranqueRM>();

            // --- Rig de Meta
            GameObject prefab = PrefabRigMeta();
            if (prefab != null)
            {
                GameObject rig = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                PrefabUtility.UnpackPrefabInstance(rig, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
                rig.name = "OVRCameraRig";
                rig.transform.SetParent(raiz.transform, false);
                ConfigurarPassthrough(rig);
                arranque.rig = rig;

                Transform manoDerecha = rig.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == "RightHandAnchor");
                Transform manoIzquierda = rig.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == "LeftHandAnchor");
                AgregarMano(manoDerecha, true);
                AgregarMano(manoIzquierda, false);

                // El rig se guarda APAGADO y ArranqueRM lo enciende al iniciar cuando sí hay visor.
                // Así el código de Meta no corre dentro del editor de Linux, que no tiene su librería nativa.
                rig.SetActive(false);
            }
            else
            {
                Debug.LogWarning("[TecEmergentes] No está instalado el Meta XR Core SDK: la escena se crea sin OVRCameraRig. " +
                                 "Cuando el paquete termine de instalarse, vuelve a construir las prácticas.");
            }

            // --- Letrero y menú de prácticas
            GameObject menu = Vacio("Menu de practicas", raiz.transform, new Vector3(0f, 2.3f, 3.0f));
            MenuPracticasRM m = menu.AddComponent<MenuPracticasRM>();
            m.escenas = Escenas;
            m.titulos = Titulos;
            m.letrero = Etiqueta(Titulos[indiceEscena] + "\n(Y) siguiente    (X) anterior", menu.transform, Vector3.zero, 0.075f, new Color(0.75f, 0.93f, 1f), TextAnchor.MiddleCenter, "Letrero");

            // --- Cámara de simulación / evidencia (solo en el editor)
            GameObject camGo = new GameObject("CamaraEvidencia");
            camGo.tag = "EditorOnly";
            Camera cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.075f, 0.085f, 0.11f, 1f);
            cam.fieldOfView = 55f;
            cam.nearClipPlane = 0.05f;
            cam.farClipPlane = 200f;
            cam.depth = 10f;
            cam.stereoTargetEye = StereoTargetEyeMask.None;
            CamaraEvidencia ce = camGo.AddComponent<CamaraEvidencia>();
            ce.objetivo = objetivoCamara;
            ce.distancia = distanciaCamara;
            ce.altura = alturaCamara;
            ce.amplitud = amplitud;
            ce.anguloCentral = anguloCentral;
            ce.Colocar(0f);
            arranque.camaraSimulacion = camGo;

            return raiz;
        }

        private static void ConfigurarPassthrough(GameObject rig)
        {
            // OVRManager: activar Passthrough y origen de seguimiento en el piso (y = 0 es el piso real)
            Type tipoManager = TipoPorNombre("OVRManager");
            Component manager = tipoManager != null ? rig.GetComponentInChildren(tipoManager, true) : null;
            if (manager == null && tipoManager != null) manager = rig.AddComponent(tipoManager);
            if (manager != null)
            {
                SerializedObject so = new SerializedObject(manager);
                Poner(so, "isInsightPassthroughEnabled", p => p.boolValue = true);
                Poner(so, "_trackingOriginType", p => p.intValue = 1); // FloorLevel
                so.ApplyModifiedPropertiesWithoutUndo();
            }
            else
            {
                Debug.LogWarning("[TecEmergentes] No encontré OVRManager en el rig.");
            }

            // OVRPassthroughLayer en modo Underlay: el video del mundo real se dibuja DETRÁS de lo virtual
            Type tipoCapa = TipoPorNombre("OVRPassthroughLayer");
            if (tipoCapa != null)
            {
                Component capa = rig.GetComponentInChildren(tipoCapa, true);
                if (capa == null) capa = rig.AddComponent(tipoCapa);
                SerializedObject so = new SerializedObject(capa);
                Poner(so, "overlayType", p => p.intValue = 1); // Underlay
                so.ApplyModifiedPropertiesWithoutUndo();
            }
            else
            {
                Debug.LogWarning("[TecEmergentes] No encontré el tipo OVRPassthroughLayer.");
            }

            // Cámaras del rig: fondo transparente (alfa 0) para que se vea el passthrough
            foreach (Camera cam in rig.GetComponentsInChildren<Camera>(true))
            {
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = new Color(0f, 0f, 0f, 0f);
                cam.nearClipPlane = 0.05f;
            }
        }

        private static void AgregarMano(Transform ancla, bool derecha)
        {
            if (ancla == null) return;
            ManoRM mano = ancla.gameObject.AddComponent<ManoRM>();
            mano.derecha = derecha;

            Material mat = MatEmisivo("Mano_RM", new Color(0.2f, 0.9f, 1f), new Color(0.1f, 0.7f, 0.9f));
            Adorno(PrimitiveType.Sphere, derecha ? "Puntero_derecho" : "Puntero_izquierdo", ancla, Vector3.zero, Vector3.zero, Vector3.one * 0.03f, mat);

            // Rayo del agarre a distancia (ManoRM lo enciende solo cuando apunta a algo que se puede agarrar)
            GameObject rayoGo = new GameObject(derecha ? "Rayo_derecho" : "Rayo_izquierdo");
            rayoGo.transform.SetParent(ancla, false);
            LineRenderer linea = rayoGo.AddComponent<LineRenderer>();
            linea.sharedMaterial = MatEstela("Rayo_RM", new Color(0.3f, 0.9f, 1f, 0.7f));
            linea.positionCount = 2;
            linea.useWorldSpace = true;
            linea.widthMultiplier = 0.006f;
            linea.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            linea.receiveShadows = false;
            linea.enabled = false;
            mano.rayo = linea;
        }

        public static string V(Vector3 v)
        {
            System.Globalization.CultureInfo c = System.Globalization.CultureInfo.InvariantCulture;
            return "(" + v.x.ToString("0.##", c) + ", " + v.y.ToString("0.##", c) + ", " + v.z.ToString("0.##", c) + ")";
        }
    }
}
