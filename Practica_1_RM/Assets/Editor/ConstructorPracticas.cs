using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TecEmergentes
{
    // Construye las seis prácticas siguiendo el paso a paso de las presentaciones de la clase.
    // Cada método deja en comentarios el paso de la presentación que está ejecutando.
    //
    // Sistema de coordenadas de todas las escenas (realidad mixta, origen en el piso):
    //   el usuario arranca parado en (0, 0, 0) viendo hacia +Z; el contenido queda a ~2 m al frente.
    public static class ConstructorPracticas
    {
        private static readonly Color Rojo = new Color(0.86f, 0.22f, 0.20f);
        private static readonly Color Naranja = new Color(0.95f, 0.55f, 0.15f);
        private static readonly Color Amarillo = new Color(0.96f, 0.80f, 0.20f);
        private static readonly Color Verde = new Color(0.25f, 0.70f, 0.35f);
        private static readonly Color Azul = new Color(0.20f, 0.50f, 0.90f);
        private static readonly Color Morado = new Color(0.55f, 0.35f, 0.85f);
        private static readonly Color Gris = new Color(0.55f, 0.57f, 0.60f);
        private static readonly Color GrisOscuro = new Color(0.20f, 0.21f, 0.24f);
        private static readonly Color Blanco = new Color(0.93f, 0.93f, 0.91f);
        private static readonly Color Madera = new Color(0.55f, 0.38f, 0.22f);
        private static readonly Color Pasto = new Color(0.30f, 0.60f, 0.30f);
        private static readonly Color Cian = new Color(0.20f, 0.85f, 0.95f);
        private static readonly Color Texto = new Color(0.95f, 0.96f, 1.00f);
        private static readonly Color TextoSuave = new Color(0.70f, 0.78f, 0.88f);

        // ================================================================== todo

        [MenuItem("Tecnologías Emergentes/1. Construir todas las prácticas", priority = 1)]
        public static void ConstruirTodoDesdeMenu()
        {
            Debug.Log("[TecEmergentes] " + ConstruirTodo());
        }

        public static string ConstruirTodo()
        {
            if (EditorApplication.isPlaying) return "Detén la reproducción (Play) antes de construir.";

            var pasos = new List<KeyValuePair<string, Action>>
            {
                new KeyValuePair<string, Action>("objetos", EscenaObjetos),
                new KeyValuePair<string, Action>("Parque_diversiones", EscenaParque),
                new KeyValuePair<string, Action>("Proyecto_final", EscenaProyectoFinal),
                new KeyValuePair<string, Action>("Canon_Autonomo", EscenaCanon),
                new KeyValuePair<string, Action>("Fisica_Sesion1", EscenaFisica1),
                new KeyValuePair<string, Action>("Fisica_Sesion2", EscenaFisica2),
                new KeyValuePair<string, Action>("Jarvis_RM", EscenaJarvis)
            };

            string resumen = "";
            int bien = 0;
            foreach (var paso in pasos)
            {
                try
                {
                    paso.Value();
                    resumen += paso.Key + ": ok\n";
                    bien++;
                }
                catch (Exception e)
                {
                    resumen += paso.Key + ": ERROR " + e.Message + "\n";
                    Debug.LogError("[TecEmergentes] Falló la escena " + paso.Key + ": " + e);
                }
            }

            AssetDatabase.SaveAssets();
            ActualizarBuildSettings();
            AssetDatabase.Refresh();

            // Deja abierta la primera práctica
            string primera = TE.RutaEscena(TE.Escenas[0]);
            if (System.IO.File.Exists(primera)) EditorSceneManager.OpenScene(primera, OpenSceneMode.Single);

            return bien + "/" + pasos.Count + " escenas construidas (Meta XR SDK " + (TE.PrefabRigMeta() != null ? "presente" : "AUSENTE") + ")\n" + resumen;
        }

        public static string Construir(string nombre)
        {
            if (EditorApplication.isPlaying) return "Detén la reproducción (Play) antes de construir.";
            switch (nombre)
            {
                case "objetos": EscenaObjetos(); break;
                case "Parque_diversiones": EscenaParque(); break;
                case "Proyecto_final": EscenaProyectoFinal(); break;
                case "Canon_Autonomo": EscenaCanon(); break;
                case "Fisica_Sesion1": EscenaFisica1(); break;
                case "Fisica_Sesion2": EscenaFisica2(); break;
                case "Jarvis_RM": EscenaJarvis(); break;
                default: return "No existe la práctica " + nombre;
            }
            AssetDatabase.SaveAssets();
            ActualizarBuildSettings();
            return nombre + ": ok";
        }

        public static void ActualizarBuildSettings()
        {
            EditorBuildSettings.scenes = TE.Escenas
                .Select(n => TE.RutaEscena(n))
                .Where(r => System.IO.File.Exists(r))
                .Select(r => new EditorBuildSettingsScene(r, true))
                .ToArray();
        }

        // ================================================================== Práctica 1: Game Object
        // Unity_o2026.pptx, diapositiva 9:
        //   "Crea una carpeta llamada Scene y crea una escena nueva llamada objetos."
        //   "Identifica y crea todas las figuras geométricas que observas en 3D Object y modifica su Componente Transform."

        private static void EscenaObjetos()
        {
            Scene escena = TE.NuevaEscena();
            GameObject raiz = TE.Vacio("Practica_1_GameObjects", null, new Vector3(0f, 0f, 1.9f));

            // Las seis figuras del menú GameObject > 3D Object, cada una con posición, rotación y escala distintas
            Figura(raiz, PrimitiveType.Cube, "Cube", new Vector3(-0.8f, 1.6f, 0f), new Vector3(20f, 45f, 0f), new Vector3(0.3f, 0.3f, 0.3f), TE.Mat("Rojo", Rojo));
            Figura(raiz, PrimitiveType.Sphere, "Sphere", new Vector3(0f, 1.6f, 0f), Vector3.zero, new Vector3(0.36f, 0.36f, 0.36f), TE.Mat("Naranja", Naranja));
            Figura(raiz, PrimitiveType.Capsule, "Capsule", new Vector3(0.8f, 1.6f, 0f), new Vector3(0f, 0f, 25f), new Vector3(0.22f, 0.24f, 0.22f), TE.Mat("Amarillo", Amarillo));
            Figura(raiz, PrimitiveType.Cylinder, "Cylinder", new Vector3(-0.8f, 0.9f, 0f), new Vector3(35f, 0f, 0f), new Vector3(0.28f, 0.16f, 0.28f), TE.Mat("Verde", Verde));
            Figura(raiz, PrimitiveType.Plane, "Plane", new Vector3(0f, 0.9f, 0f), new Vector3(-55f, 0f, 0f), new Vector3(0.04f, 1f, 0.04f), TE.Mat("Azul", Azul));
            Figura(raiz, PrimitiveType.Quad, "Quad", new Vector3(0.8f, 0.9f, 0f), new Vector3(0f, -25f, 0f), new Vector3(0.42f, 0.3f, 1f), TE.Mat("Morado", Morado));

            TE.Etiqueta("Las 6 figuras de GameObject > 3D Object\ncada una con su Transform modificado", raiz.transform, new Vector3(0f, 0.2f, 0f), 0.04f, TextoSuave);

            TE.InfraestructuraRM(0, new Vector3(0f, 1.15f, 1.9f), 1.9f, 1.4f, 22f);   // cerca, para que se lean los valores del Transform
            TE.GuardarEscena(escena, "objetos");
        }

        private static void Figura(GameObject raiz, PrimitiveType tipo, string nombre, Vector3 pos, Vector3 rot, Vector3 escala, Material mat)
        {
            TE.Prim(tipo, nombre, raiz.transform, pos, rot, escala, mat);
            string texto = "<b>" + nombre + "</b>\nPos " + TE.V(pos) + "\nRot " + TE.V(rot) + "\nEsc " + TE.V(escala);
            TE.Etiqueta(texto, raiz.transform, pos + new Vector3(0f, -0.36f, -0.02f), 0.032f, Texto, TextAnchor.MiddleCenter, "Etiqueta_" + nombre);
        }

        // ================================================================== Práctica 1: Parque de diversiones
        // Unity_o2026.pptx, diapositiva 10:
        //   "Crea una nueva Escena y guárdala en la carpeta Scene, coloca el nombre de Parque_diversiones."
        //   "Crea un parque de diversiones donde priorices jerarquías, ya que en el siguiente paso tendrá movimiento."
        // Es una maqueta de 2.7 m de diámetro sobre el piso real.

        private static void EscenaParque()
        {
            Scene escena = TE.NuevaEscena();

            Material rojo = TE.Mat("Rojo", Rojo), amarillo = TE.Mat("Amarillo", Amarillo), azul = TE.Mat("Azul", Azul);
            Material gris = TE.Mat("Gris", Gris), blanco = TE.Mat("Blanco", Blanco), verde = TE.Mat("Verde", Verde);
            Material naranja = TE.Mat("Naranja", Naranja), morado = TE.Mat("Morado", Morado), madera = TE.Mat("Madera", Madera);
            Material pasto = TE.Mat("Pasto", Pasto, 0.05f), camino = TE.Mat("Camino", new Color(0.78f, 0.74f, 0.66f), 0.05f);
            Material luz = TE.MatEmisivo("Luz_Farol", new Color(1f, 0.95f, 0.7f), new Color(1.2f, 1.0f, 0.5f));

            // ---------- PREFABS: el plano original guardado en archivos; en la escena solo hay instancias
            GameObject prefabCabina = PrefabCabina(azul, blanco, gris);
            GameObject prefabCaballo = PrefabCaballo(blanco, amarillo, madera);
            GameObject prefabSilla = PrefabSilla(gris, rojo);
            GameObject prefabArbol = PrefabArbol(madera, verde);
            GameObject prefabFarol = PrefabFarol(GrisOscuroMat(), luz);
            GameObject prefabPuesto = PrefabPuesto(madera, rojo, blanco);

            // ---------- JERARQUÍA: un padre por atracción; al mover o girar al padre, los hijos lo siguen
            GameObject parque = TE.Vacio("Parque_diversiones", null, new Vector3(0f, 0f, 2.2f));
            Transform p = parque.transform;

            TE.Adorno(PrimitiveType.Cylinder, "Terreno", p, new Vector3(0f, 0.01f, 0f), Vector3.zero, new Vector3(2.7f, 0.01f, 2.7f), pasto);
            TE.Adorno(PrimitiveType.Cube, "Camino", p, new Vector3(0f, 0.023f, -0.72f), Vector3.zero, new Vector3(0.32f, 0.006f, 1.1f), camino);
            TE.Adorno(PrimitiveType.Cylinder, "Plaza", p, new Vector3(0f, 0.023f, -0.05f), Vector3.zero, new Vector3(0.75f, 0.003f, 0.75f), camino);

            // Entrada
            GameObject entrada = TE.Vacio("Entrada", p, new Vector3(0f, 0f, -1.22f));
            TE.Adorno(PrimitiveType.Cylinder, "Poste_izquierdo", entrada.transform, new Vector3(-0.32f, 0.24f, 0f), Vector3.zero, new Vector3(0.05f, 0.24f, 0.05f), rojo);
            TE.Adorno(PrimitiveType.Cylinder, "Poste_derecho", entrada.transform, new Vector3(0.32f, 0.24f, 0f), Vector3.zero, new Vector3(0.05f, 0.24f, 0.05f), rojo);
            TE.Adorno(PrimitiveType.Cube, "Letrero", entrada.transform, new Vector3(0f, 0.52f, 0f), Vector3.zero, new Vector3(0.78f, 0.13f, 0.03f), amarillo);
            TE.Etiqueta("PARQUE RM", entrada.transform, new Vector3(0f, 0.52f, -0.02f), 0.085f, new Color(0.15f, 0.1f, 0.1f), TextAnchor.MiddleCenter, "Texto_letrero");

            // Rueda de la fortuna
            GameObject rueda = TE.Vacio("Rueda_de_la_fortuna", p, new Vector3(-0.72f, 0f, 0.45f));
            const float alturaEje = 0.70f, radio = 0.5f;
            GameObject baseRueda = TE.Vacio("Base", rueda.transform, Vector3.zero);
            foreach (float z in new[] { -0.11f, 0.11f })
            {
                string lado = z < 0f ? "frontal" : "trasera";
                TE.Adorno(PrimitiveType.Cube, "Pata_" + lado + "_izquierda", baseRueda.transform, new Vector3(-0.175f, alturaEje / 2f, z), new Vector3(0f, 0f, -26.6f), new Vector3(0.035f, 0.79f, 0.035f), gris);
                TE.Adorno(PrimitiveType.Cube, "Pata_" + lado + "_derecha", baseRueda.transform, new Vector3(0.175f, alturaEje / 2f, z), new Vector3(0f, 0f, 26.6f), new Vector3(0.035f, 0.79f, 0.035f), gris);
            }
            TE.Adorno(PrimitiveType.Cylinder, "Eje", baseRueda.transform, new Vector3(0f, alturaEje, 0f), new Vector3(90f, 0f, 0f), new Vector3(0.05f, 0.14f, 0.05f), GrisOscuroMat());

            GameObject giratoria = TE.Vacio("Rueda_giratoria", rueda.transform, new Vector3(0f, alturaEje, 0f));
            Girar giroRueda = giratoria.AddComponent<Girar>();
            giroRueda.eje = Vector3.forward;
            giroRueda.gradosPorSegundo = 18f;
            Aro(giratoria.transform, "Aro_frontal", radio, -0.075f, rojo, blanco);
            Aro(giratoria.transform, "Aro_trasero", radio, 0.075f, rojo, blanco);
            GameObject cabinas = TE.Vacio("Cabinas", giratoria.transform, Vector3.zero);
            Material[] coloresCabina = { azul, naranja, morado, verde };
            for (int i = 0; i < 8; i++)
            {
                float a = i * 45f * Mathf.Deg2Rad;
                GameObject cabina = TE.Instancia(prefabCabina, "Cabina_" + (i + 1).ToString("00"), cabinas.transform, new Vector3(Mathf.Cos(a) * radio, Mathf.Sin(a) * radio, 0f), Vector3.zero);
                // Sobrescritura (override) de la instancia: mismo prefab, distinto color
                Transform cuerpo = cabina.transform.Find("Cuerpo");
                if (cuerpo != null)
                {
                    MeshRenderer mr = cuerpo.GetComponent<MeshRenderer>();
                    mr.sharedMaterial = coloresCabina[i % coloresCabina.Length];
                    TE.RegistrarOverride(mr);
                }
            }

            // Carrusel
            GameObject carrusel = TE.Vacio("Carrusel", p, new Vector3(0.72f, 0f, 0.42f));
            TE.Adorno(PrimitiveType.Cylinder, "Plataforma_fija", carrusel.transform, new Vector3(0f, 0.025f, 0f), Vector3.zero, new Vector3(0.82f, 0.025f, 0.82f), gris);
            GameObject giratorio = TE.Vacio("Giratorio", carrusel.transform, new Vector3(0f, 0.05f, 0f));
            Girar giroCarrusel = giratorio.AddComponent<Girar>();
            giroCarrusel.eje = Vector3.up;
            giroCarrusel.gradosPorSegundo = 35f;
            TE.Adorno(PrimitiveType.Cylinder, "Piso_giratorio", giratorio.transform, new Vector3(0f, 0.01f, 0f), Vector3.zero, new Vector3(0.74f, 0.01f, 0.74f), rojo);
            TE.Adorno(PrimitiveType.Cylinder, "Poste_central", giratorio.transform, new Vector3(0f, 0.27f, 0f), Vector3.zero, new Vector3(0.07f, 0.27f, 0.07f), amarillo);
            TE.Adorno(PrimitiveType.Cylinder, "Techo", giratorio.transform, new Vector3(0f, 0.55f, 0f), Vector3.zero, new Vector3(0.86f, 0.02f, 0.86f), rojo);
            TE.Adorno(PrimitiveType.Sphere, "Cupula", giratorio.transform, new Vector3(0f, 0.57f, 0f), Vector3.zero, new Vector3(0.5f, 0.18f, 0.5f), amarillo);
            GameObject caballos = TE.Vacio("Caballos", giratorio.transform, Vector3.zero);
            for (int i = 0; i < 6; i++)
            {
                float grados = i * 60f;
                float a = grados * Mathf.Deg2Rad;
                TE.Instancia(prefabCaballo, "Caballo_" + (i + 1).ToString("00"), caballos.transform, new Vector3(Mathf.Cos(a) * 0.27f, 0.02f, Mathf.Sin(a) * 0.27f), new Vector3(0f, -grados, 0f));
            }

            // Sillas voladoras
            GameObject sillas = TE.Vacio("Sillas_voladoras", p, new Vector3(0.05f, 0f, -0.42f));
            TE.Adorno(PrimitiveType.Cylinder, "Base", sillas.transform, new Vector3(0f, 0.02f, 0f), Vector3.zero, new Vector3(0.32f, 0.02f, 0.32f), gris);
            TE.Adorno(PrimitiveType.Cylinder, "Torre", sillas.transform, new Vector3(0f, 0.40f, 0f), Vector3.zero, new Vector3(0.07f, 0.40f, 0.07f), azul);
            GameObject corona = TE.Vacio("Corona_giratoria", sillas.transform, new Vector3(0f, 0.82f, 0f));
            Girar giroSillas = corona.AddComponent<Girar>();
            giroSillas.eje = Vector3.up;
            giroSillas.gradosPorSegundo = 55f;
            TE.Adorno(PrimitiveType.Cylinder, "Disco", corona.transform, Vector3.zero, Vector3.zero, new Vector3(0.56f, 0.015f, 0.56f), amarillo);
            TE.Adorno(PrimitiveType.Sphere, "Remate", corona.transform, new Vector3(0f, 0.04f, 0f), Vector3.zero, new Vector3(0.16f, 0.1f, 0.16f), rojo);
            for (int i = 0; i < 8; i++)
            {
                float grados = i * 45f;
                float a = grados * Mathf.Deg2Rad;
                TE.Instancia(prefabSilla, "Silla_" + (i + 1).ToString("00"), corona.transform, new Vector3(Mathf.Cos(a) * 0.25f, 0f, Mathf.Sin(a) * 0.25f), new Vector3(0f, -grados, 24f));
            }

            // Puestos, árboles, faroles y bancas (instancias de prefab repartidas por el parque)
            GameObject puestos = TE.Vacio("Puestos", p, Vector3.zero);
            TE.Instancia(prefabPuesto, "Puesto_01", puestos.transform, new Vector3(-0.55f, 0.02f, -0.75f), new Vector3(0f, 35f, 0f));
            TE.Instancia(prefabPuesto, "Puesto_02", puestos.transform, new Vector3(0.62f, 0.02f, -0.72f), new Vector3(0f, -35f, 0f));
            TE.Instancia(prefabPuesto, "Puesto_03", puestos.transform, new Vector3(0f, 0.02f, 1.05f), new Vector3(0f, 180f, 0f));

            GameObject arboles = TE.Vacio("Arboles", p, Vector3.zero);
            float[] angulosArbol = { 20f, 62f, 118f, 160f, 205f, 238f, 302f, 335f };
            for (int i = 0; i < angulosArbol.Length; i++)
            {
                float a = angulosArbol[i] * Mathf.Deg2Rad;
                GameObject arbol = TE.Instancia(prefabArbol, "Arbol_" + (i + 1).ToString("00"), arboles.transform, new Vector3(Mathf.Cos(a) * 1.2f, 0.02f, Mathf.Sin(a) * 1.2f), new Vector3(0f, i * 40f, 0f));
                float s = 0.85f + 0.1f * (i % 3);
                arbol.transform.localScale = new Vector3(s, s, s);
                TE.RegistrarOverride(arbol.transform);
            }

            GameObject faroles = TE.Vacio("Faroles", p, Vector3.zero);
            Vector3[] posFarol = { new Vector3(-0.24f, 0.02f, -0.95f), new Vector3(0.24f, 0.02f, -0.95f), new Vector3(-0.42f, 0.02f, -0.1f), new Vector3(0.42f, 0.02f, -0.1f) };
            for (int i = 0; i < posFarol.Length; i++)
            {
                TE.Instancia(prefabFarol, "Farol_" + (i + 1).ToString("00"), faroles.transform, posFarol[i], Vector3.zero);
            }

            TE.Etiqueta("Maqueta: cada atracción es un padre con hijos.\nLas piezas repetidas son instancias de un prefab.", p, new Vector3(0f, 1.35f, 0.9f), 0.045f, TextoSuave);

            TE.InfraestructuraRM(1, new Vector3(0f, 0.42f, 2.2f), 2.6f, 1.9f, 40f);
            TE.GuardarEscena(escena, "Parque_diversiones");
        }

        private static Material GrisOscuroMat()
        {
            return TE.Mat("Gris_Oscuro", GrisOscuro);
        }

        // Aro de la rueda: 16 tramos en círculo + 8 rayos, todos hijos de un mismo objeto vacío
        private static void Aro(Transform padre, string nombre, float radio, float z, Material matAro, Material matRayo)
        {
            GameObject aro = TE.Vacio(nombre, padre, new Vector3(0f, 0f, z));
            const int tramos = 16;
            float largo = 2f * Mathf.PI * radio / tramos * 1.04f;
            for (int i = 0; i < tramos; i++)
            {
                float grados = i * 360f / tramos;
                float a = grados * Mathf.Deg2Rad;
                TE.Adorno(PrimitiveType.Cube, "Tramo_" + (i + 1).ToString("00"), aro.transform, new Vector3(Mathf.Cos(a) * radio, Mathf.Sin(a) * radio, 0f), new Vector3(0f, 0f, grados + 90f), new Vector3(largo, 0.022f, 0.02f), matAro);
            }
            for (int i = 0; i < 8; i++)
            {
                float grados = i * 45f;
                float a = grados * Mathf.Deg2Rad;
                TE.Adorno(PrimitiveType.Cube, "Rayo_" + (i + 1).ToString("00"), aro.transform, new Vector3(Mathf.Cos(a) * radio / 2f, Mathf.Sin(a) * radio / 2f, 0f), new Vector3(0f, 0f, grados), new Vector3(radio, 0.012f, 0.012f), matRayo);
            }
        }

        private static GameObject PrefabCabina(Material cuerpo, Material techo, Material colgante)
        {
            GameObject raiz = new GameObject("Cabina");
            raiz.AddComponent<MantenerVertical>();
            TE.Adorno(PrimitiveType.Cylinder, "Colgante", raiz.transform, new Vector3(0f, -0.035f, 0f), Vector3.zero, new Vector3(0.01f, 0.035f, 0.01f), colgante);
            TE.Adorno(PrimitiveType.Cube, "Techo", raiz.transform, new Vector3(0f, -0.072f, 0f), Vector3.zero, new Vector3(0.13f, 0.012f, 0.105f), techo);
            TE.Adorno(PrimitiveType.Cube, "Cuerpo", raiz.transform, new Vector3(0f, -0.125f, 0f), Vector3.zero, new Vector3(0.115f, 0.09f, 0.09f), cuerpo);
            return TE.GuardarComoPrefab(raiz, "Cabina");
        }

        private static GameObject PrefabCaballo(Material cuerpo, Material barra, Material patas)
        {
            GameObject raiz = new GameObject("Caballo");
            TE.Adorno(PrimitiveType.Cylinder, "Barra", raiz.transform, new Vector3(0f, 0.25f, 0f), Vector3.zero, new Vector3(0.012f, 0.25f, 0.012f), barra);
            TE.Adorno(PrimitiveType.Capsule, "Cuerpo", raiz.transform, new Vector3(0f, 0.16f, 0f), new Vector3(90f, 0f, 0f), new Vector3(0.06f, 0.07f, 0.06f), cuerpo);
            TE.Adorno(PrimitiveType.Sphere, "Cabeza", raiz.transform, new Vector3(0f, 0.215f, 0.075f), Vector3.zero, new Vector3(0.05f, 0.06f, 0.06f), cuerpo);
            TE.Adorno(PrimitiveType.Cube, "Patas_delanteras", raiz.transform, new Vector3(0f, 0.105f, 0.04f), Vector3.zero, new Vector3(0.04f, 0.07f, 0.012f), patas);
            TE.Adorno(PrimitiveType.Cube, "Patas_traseras", raiz.transform, new Vector3(0f, 0.105f, -0.04f), Vector3.zero, new Vector3(0.04f, 0.07f, 0.012f), patas);
            return TE.GuardarComoPrefab(raiz, "Caballo");
        }

        private static GameObject PrefabSilla(Material cadena, Material asiento)
        {
            GameObject raiz = new GameObject("Silla");
            TE.Adorno(PrimitiveType.Cylinder, "Cadena", raiz.transform, new Vector3(0f, -0.14f, 0f), Vector3.zero, new Vector3(0.006f, 0.14f, 0.006f), cadena);
            TE.Adorno(PrimitiveType.Cube, "Asiento", raiz.transform, new Vector3(0f, -0.29f, 0f), Vector3.zero, new Vector3(0.065f, 0.02f, 0.065f), asiento);
            TE.Adorno(PrimitiveType.Cube, "Respaldo", raiz.transform, new Vector3(-0.03f, -0.255f, 0f), Vector3.zero, new Vector3(0.01f, 0.06f, 0.065f), asiento);
            return TE.GuardarComoPrefab(raiz, "Silla");
        }

        private static GameObject PrefabArbol(Material tronco, Material copa)
        {
            GameObject raiz = new GameObject("Arbol");
            TE.Adorno(PrimitiveType.Cylinder, "Tronco", raiz.transform, new Vector3(0f, 0.09f, 0f), Vector3.zero, new Vector3(0.04f, 0.09f, 0.04f), tronco);
            TE.Adorno(PrimitiveType.Sphere, "Copa", raiz.transform, new Vector3(0f, 0.27f, 0f), Vector3.zero, new Vector3(0.24f, 0.26f, 0.24f), copa);
            return TE.GuardarComoPrefab(raiz, "Arbol");
        }

        private static GameObject PrefabFarol(Material poste, Material lampara)
        {
            GameObject raiz = new GameObject("Farol");
            TE.Adorno(PrimitiveType.Cylinder, "Poste", raiz.transform, new Vector3(0f, 0.15f, 0f), Vector3.zero, new Vector3(0.012f, 0.15f, 0.012f), poste);
            TE.Adorno(PrimitiveType.Sphere, "Lampara", raiz.transform, new Vector3(0f, 0.32f, 0f), Vector3.zero, new Vector3(0.05f, 0.05f, 0.05f), lampara);
            return TE.GuardarComoPrefab(raiz, "Farol");
        }

        private static GameObject PrefabPuesto(Material mostrador, Material toldo, Material poste)
        {
            GameObject raiz = new GameObject("Puesto");
            TE.Adorno(PrimitiveType.Cube, "Mostrador", raiz.transform, new Vector3(0f, 0.08f, 0f), Vector3.zero, new Vector3(0.3f, 0.16f, 0.18f), mostrador);
            TE.Adorno(PrimitiveType.Cylinder, "Poste_izquierdo", raiz.transform, new Vector3(-0.14f, 0.16f, -0.08f), Vector3.zero, new Vector3(0.012f, 0.16f, 0.012f), poste);
            TE.Adorno(PrimitiveType.Cylinder, "Poste_derecho", raiz.transform, new Vector3(0.14f, 0.16f, -0.08f), Vector3.zero, new Vector3(0.012f, 0.16f, 0.012f), poste);
            TE.Adorno(PrimitiveType.Cube, "Toldo", raiz.transform, new Vector3(0f, 0.33f, -0.03f), new Vector3(-14f, 0f, 0f), new Vector3(0.36f, 0.015f, 0.3f), toldo);
            return TE.GuardarComoPrefab(raiz, "Puesto");
        }

        // ================================================================== Práctica 1: Propuesta de proyecto final
        // Unity_o2026.pptx, diapositiva 11: "Proponer ejemplo de proyecto final: GameObject, Jerarquía, Prefab."
        // Propuesta: JARVIS, un asistente cuyo cerebro corre en la laptop y cuya interfaz se ve en realidad mixta.

        private static void EscenaProyectoFinal()
        {
            Scene escena = TE.NuevaEscena();

            Material nucleoMat = TE.MatEmisivo("Jarvis_Nucleo", Cian, new Color(0.15f, 1.3f, 1.6f));
            Material anilloMat = TE.MatEmisivo("Jarvis_Anillo", new Color(0.9f, 0.95f, 1f), new Color(0.5f, 0.8f, 1.0f));
            Material sateliteMat = TE.MatEmisivo("Jarvis_Satelite", Naranja, new Color(1.2f, 0.6f, 0.1f));
            Material panelMat = TE.MatTransparente("Jarvis_Panel", new Color(0.1f, 0.55f, 0.75f, 0.32f));
            Material marcoMat = TE.MatEmisivo("Jarvis_Marco", Cian, new Color(0.1f, 0.9f, 1.1f));
            Material mesaMat = TE.Mat("Gris_Oscuro", GrisOscuro);

            // ---------- PREFABS
            GameObject prefabPanel = PrefabPanel(panelMat, marcoMat);
            GameObject prefabSatelite = PrefabSatelite(sateliteMat);

            // ---------- JERARQUÍA
            GameObject raiz = TE.Vacio("Propuesta_Proyecto_Final", null, new Vector3(0f, 0f, 1.9f));

            // GameObject principal: el núcleo de Jarvis, con anillos y satélites como hijos que giran con él
            GameObject jarvis = TE.Vacio("Jarvis", raiz.transform, new Vector3(0f, 1.3f, 0f));
            TE.Adorno(PrimitiveType.Sphere, "Nucleo", jarvis.transform, Vector3.zero, Vector3.zero, Vector3.one * 0.22f, nucleoMat);

            GameObject anilloA = TE.Vacio("Anillo_A", jarvis.transform, Vector3.zero, new Vector3(18f, 0f, 0f));
            AnilloDeCubos(anilloA.transform, 0.21f, 28, 0.018f, anilloMat);
            Girar ga = anilloA.AddComponent<Girar>();
            ga.eje = Vector3.up;
            ga.gradosPorSegundo = 50f;

            GameObject anilloB = TE.Vacio("Anillo_B", jarvis.transform, Vector3.zero, new Vector3(-62f, 0f, 30f));
            AnilloDeCubos(anilloB.transform, 0.27f, 34, 0.014f, anilloMat);
            Girar gb = anilloB.AddComponent<Girar>();
            gb.eje = Vector3.up;
            gb.gradosPorSegundo = -32f;

            GameObject orbita = TE.Vacio("Satelites", jarvis.transform, Vector3.zero, new Vector3(0f, 0f, 12f));
            Girar giroOrbita = orbita.AddComponent<Girar>();
            giroOrbita.eje = Vector3.up;
            giroOrbita.gradosPorSegundo = 22f;
            for (int i = 0; i < 3; i++)
            {
                float a = i * 120f * Mathf.Deg2Rad;
                TE.Instancia(prefabSatelite, "Satelite_" + (i + 1).ToString("00"), orbita.transform, new Vector3(Mathf.Cos(a) * 0.42f, 0f, Mathf.Sin(a) * 0.42f), Vector3.zero);
            }

            // Tres instancias del mismo prefab de panel; solo cambia el texto (override de la instancia)
            GameObject paneles = TE.Vacio("Paneles", raiz.transform, Vector3.zero);
            Panel(prefabPanel, paneles.transform, "Panel_Cerebro", new Vector3(-0.95f, 1.35f, 0.15f), -24f,
                "CEREBRO", "Claude corre en la laptop:\nentiende la orden, decide\ny manda acciones a Unity.");
            Panel(prefabPanel, paneles.transform, "Panel_Vision", new Vector3(0f, 2.0f, 0.3f), 0f,
                "VISIÓN", "Unity + Meta Quest 3:\nJarvis aparece sobre el\nmundo real (passthrough).");
            Panel(prefabPanel, paneles.transform, "Panel_Protocolo", new Vector3(0.95f, 1.35f, 0.15f), 24f,
                "PROTOCOLO", "Laptop y visor hablan por\nWi-Fi en la red local;\nse mide la latencia.");

            // Mesa con los objetos que Jarvis podrá crear a petición (vienen de las prácticas)
            GameObject mesa = TE.Vacio("Mesa_de_objetos", raiz.transform, new Vector3(0f, 0f, 0.1f));
            TE.Prim(PrimitiveType.Cylinder, "Mesa", mesa.transform, new Vector3(0f, 0.72f, 0f), Vector3.zero, new Vector3(0.9f, 0.02f, 0.5f), mesaMat);
            TE.Prim(PrimitiveType.Cylinder, "Pata", mesa.transform, new Vector3(0f, 0.35f, 0f), Vector3.zero, new Vector3(0.08f, 0.35f, 0.08f), mesaMat);
            ObjetoFisico(PrimitiveType.Cube, "Objeto_Cubo", mesa.transform, new Vector3(-0.25f, 0.81f, 0f), Vector3.one * 0.13f, TE.Mat("Rojo", Rojo));
            ObjetoFisico(PrimitiveType.Sphere, "Objeto_Esfera", mesa.transform, new Vector3(0f, 0.82f, 0f), Vector3.one * 0.15f, TE.Mat("Amarillo", Amarillo));
            ObjetoFisico(PrimitiveType.Capsule, "Objeto_Capsula", mesa.transform, new Vector3(0.25f, 0.87f, 0f), new Vector3(0.11f, 0.12f, 0.11f), TE.Mat("Verde", Verde));

            TE.Etiqueta("<b>Proyecto final: JARVIS en realidad mixta</b>\nGameObject (núcleo)  ·  Jerarquía (anillos y satélites)  ·  Prefab (paneles)", raiz.transform, new Vector3(0f, 0.38f, -0.25f), 0.042f, Texto);

            GameObject rm = TE.InfraestructuraRM(2, new Vector3(0f, 1.25f, 1.9f), 3.0f, 1.5f, 30f);
            // El letrero del menú (y = 2.3) quedaba encimado con el panel VISIÓN, también visto desde el visor
            rm.transform.Find("Menu de practicas").localPosition = new Vector3(0f, 2.75f, 3.0f);
            TE.GuardarEscena(escena, "Proyecto_final");
        }

        private static void AnilloDeCubos(Transform padre, float radio, int cantidad, float grosor, Material mat)
        {
            float largo = 2f * Mathf.PI * radio / cantidad * 0.7f;
            for (int i = 0; i < cantidad; i++)
            {
                float grados = i * 360f / cantidad;
                float a = grados * Mathf.Deg2Rad;
                TE.Adorno(PrimitiveType.Cube, "Segmento_" + (i + 1).ToString("00"), padre, new Vector3(Mathf.Cos(a) * radio, 0f, Mathf.Sin(a) * radio), new Vector3(0f, -grados, 0f), new Vector3(grosor, grosor, largo), mat);
            }
        }

        private static GameObject PrefabSatelite(Material mat)
        {
            GameObject raiz = new GameObject("Satelite");
            TE.Adorno(PrimitiveType.Sphere, "Cuerpo", raiz.transform, Vector3.zero, Vector3.zero, Vector3.one * 0.05f, mat);
            TE.Adorno(PrimitiveType.Cube, "Antena", raiz.transform, new Vector3(0f, 0.04f, 0f), Vector3.zero, new Vector3(0.006f, 0.05f, 0.006f), mat);
            return TE.GuardarComoPrefab(raiz, "Satelite");
        }

        private static GameObject PrefabPanel(Material fondo, Material marco)
        {
            GameObject raiz = new GameObject("Panel_Holografico");
            TE.Adorno(PrimitiveType.Quad, "Fondo", raiz.transform, Vector3.zero, Vector3.zero, new Vector3(0.62f, 0.36f, 1f), fondo);
            TE.Adorno(PrimitiveType.Cube, "Marco_superior", raiz.transform, new Vector3(0f, 0.18f, 0f), Vector3.zero, new Vector3(0.63f, 0.008f, 0.008f), marco);
            TE.Adorno(PrimitiveType.Cube, "Marco_inferior", raiz.transform, new Vector3(0f, -0.18f, 0f), Vector3.zero, new Vector3(0.63f, 0.008f, 0.008f), marco);
            TE.Adorno(PrimitiveType.Cube, "Marco_izquierdo", raiz.transform, new Vector3(-0.31f, 0f, 0f), Vector3.zero, new Vector3(0.008f, 0.36f, 0.008f), marco);
            TE.Adorno(PrimitiveType.Cube, "Marco_derecho", raiz.transform, new Vector3(0.31f, 0f, 0f), Vector3.zero, new Vector3(0.008f, 0.36f, 0.008f), marco);
            TE.Etiqueta("TÍTULO", raiz.transform, new Vector3(0f, 0.115f, -0.01f), 0.055f, new Color(0.6f, 0.97f, 1f), TextAnchor.MiddleCenter, "Titulo");
            TE.Etiqueta("Texto del panel", raiz.transform, new Vector3(0f, -0.045f, -0.01f), 0.036f, Texto, TextAnchor.MiddleCenter, "Texto");
            return TE.GuardarComoPrefab(raiz, "Panel_Holografico");
        }

        private static void Panel(GameObject prefab, Transform padre, string nombre, Vector3 pos, float giroY, string titulo, string texto)
        {
            GameObject panel = TE.Instancia(prefab, nombre, padre, pos, new Vector3(0f, giroY, 0f));
            PonerTexto(panel.transform.Find("Titulo"), titulo);
            PonerTexto(panel.transform.Find("Texto"), texto);
        }

        private static void PonerTexto(Transform t, string texto)
        {
            if (t == null) return;
            TextMesh tm = t.GetComponent<TextMesh>();
            tm.text = texto;
            TE.RegistrarOverride(tm);
        }

        private static GameObject ObjetoFisico(PrimitiveType tipo, string nombre, Transform padre, Vector3 pos, Vector3 escala, Material mat)
        {
            GameObject go = TE.Prim(tipo, nombre, padre, pos, Vector3.zero, escala, mat);
            go.AddComponent<Rigidbody>();
            return go;
        }

        // ================================================================== Proyecto final: Jarvis en realidad mixta
        // La escena que se conecta al cerebro (Claude en la laptop). Parte de la propuesta de la Práctica 1
        // (núcleo + anillos + panel de prefab) y le agrega el script JarvisRM, que ejecuta las órdenes.

        private static void EscenaJarvis()
        {
            Scene escena = TE.NuevaEscena();

            Material nucleoMat = TE.MatEmisivo("Jarvis_Nucleo", Cian, new Color(0.15f, 1.3f, 1.6f));
            Material anilloMat = TE.MatEmisivo("Jarvis_Anillo", new Color(0.9f, 0.95f, 1f), new Color(0.5f, 0.8f, 1.0f));
            Material sateliteMat = TE.MatEmisivo("Jarvis_Satelite", Naranja, new Color(1.2f, 0.6f, 0.1f));
            Material panelMat = TE.MatTransparente("Jarvis_Panel", new Color(0.1f, 0.55f, 0.75f, 0.32f));
            Material marcoMat = TE.MatEmisivo("Jarvis_Marco", Cian, new Color(0.1f, 0.9f, 1.1f));
            // Mismos prefabs de la propuesta de la Práctica 1 (se crean solo si todavía no existen)
            GameObject prefabPanel = TE.PrefabExistente("Panel_Holografico") ?? PrefabPanel(panelMat, marcoMat);
            GameObject prefabSatelite = TE.PrefabExistente("Satelite") ?? PrefabSatelite(sateliteMat);

            GameObject raiz = TE.Vacio("Jarvis_RM", null, new Vector3(0f, 0f, 1.8f));

            // Núcleo con anillos y satélites (misma jerarquía que la propuesta de la Práctica 1)
            GameObject jarvis = TE.Vacio("Jarvis", raiz.transform, new Vector3(0f, 1.45f, 0.35f));
            GameObject nucleo = TE.Adorno(PrimitiveType.Sphere, "Nucleo", jarvis.transform, Vector3.zero, Vector3.zero, Vector3.one * 0.2f, nucleoMat);
            GameObject anilloA = TE.Vacio("Anillo_A", jarvis.transform, Vector3.zero, new Vector3(18f, 0f, 0f));
            AnilloDeCubos(anilloA.transform, 0.19f, 28, 0.016f, anilloMat);
            Girar ga = anilloA.AddComponent<Girar>();
            ga.eje = Vector3.up;
            ga.gradosPorSegundo = 50f;
            GameObject anilloB = TE.Vacio("Anillo_B", jarvis.transform, Vector3.zero, new Vector3(-62f, 0f, 30f));
            AnilloDeCubos(anilloB.transform, 0.25f, 34, 0.012f, anilloMat);
            Girar gb = anilloB.AddComponent<Girar>();
            gb.eje = Vector3.up;
            gb.gradosPorSegundo = -32f;
            GameObject orbita = TE.Vacio("Satelites", jarvis.transform, Vector3.zero, new Vector3(0f, 0f, 12f));
            Girar giroOrbita = orbita.AddComponent<Girar>();
            giroOrbita.eje = Vector3.up;
            giroOrbita.gradosPorSegundo = 22f;
            for (int i = 0; i < 3; i++)
            {
                float a = i * 120f * Mathf.Deg2Rad;
                TE.Instancia(prefabSatelite, "Satelite_" + (i + 1).ToString("00"), orbita.transform, new Vector3(Mathf.Cos(a) * 0.38f, 0f, Mathf.Sin(a) * 0.38f), Vector3.zero);
            }

            // Panel donde Jarvis responde (instancia del prefab de la Práctica 1, más grande)
            GameObject panel = TE.Instancia(prefabPanel, "Panel_Respuesta", raiz.transform, new Vector3(0.95f, 1.4f, 0.2f), new Vector3(0f, 22f, 0f));
            panel.transform.localScale = Vector3.one * 1.6f;
            TE.RegistrarOverride(panel.transform);
            PonerTexto(panel.transform.Find("Titulo"), "JARVIS");
            PonerTexto(panel.transform.Find("Texto"), "Di «Hey Jarvis»\no aprieta el botón A");

            // Lo que entendió y el estado del enlace (conexión y latencias)
            TextMesh usuario = TE.Etiqueta("Tú: ...", raiz.transform, new Vector3(0f, 0.98f, 0.2f), 0.045f, new Color(1f, 0.93f, 0.6f), TextAnchor.MiddleCenter, "Texto_usuario");
            GameObject tablero = TE.Vacio("Tablero_de_estado", raiz.transform, new Vector3(-0.95f, 1.62f, 0.2f), new Vector3(0f, -22f, 0f));
            TE.Adorno(PrimitiveType.Quad, "Fondo", tablero.transform, new Vector3(0.44f, -0.13f, 0.01f), Vector3.zero, new Vector3(1.0f, 0.42f, 1f), TE.MatTransparente("Consola_Fondo", new Color(0.05f, 0.07f, 0.1f, 0.6f)));
            TextMesh estado = TE.Etiqueta("Buscando el cerebro de Jarvis...", tablero.transform, new Vector3(-0.03f, 0.05f, 0f), 0.034f, new Color(0.75f, 1f, 0.8f), TextAnchor.UpperLeft, "Texto_estado");

            // Piso invisible a la altura del piso real: los objetos que crea Jarvis caen sobre él
            GameObject piso = TE.Vacio("Piso_invisible", raiz.transform, new Vector3(0f, -0.05f, 0f));
            BoxCollider caja = piso.AddComponent<BoxCollider>();
            caja.size = new Vector3(14f, 0.1f, 14f);

            GameObject zona = TE.Vacio("Zona_de_objetos", raiz.transform, new Vector3(0f, 1.3f, -1.5f));

            JarvisRM rm = raiz.AddComponent<JarvisRM>();
            rm.nucleo = nucleo.transform;
            rm.nucleoRenderer = nucleo.GetComponent<MeshRenderer>();
            rm.textoEstado = estado;
            rm.textoUsuario = usuario;
            rm.tituloPanel = panel.transform.Find("Titulo").GetComponent<TextMesh>();
            rm.textoPanel = panel.transform.Find("Texto").GetComponent<TextMesh>();
            rm.zonaDeObjetos = zona.transform;
            rm.materiales = new[]
            {
                TE.Mat("Rojo", Rojo), TE.Mat("Naranja", Naranja), TE.Mat("Amarillo", Amarillo), TE.Mat("Verde", Verde),
                TE.Mat("Azul", Azul), TE.Mat("Morado", Morado), TE.Mat("Blanco", Blanco), TE.Mat("Gris", Gris)
            };

            TE.InfraestructuraRM(6, new Vector3(0f, 0.85f, 1.8f), 3.0f, 1.5f, 26f);   // más baja: los objetos que crea Jarvis caen al piso
            TE.GuardarEscena(escena, "Jarvis_RM");
        }

        // ================================================================== Práctica 3: El Cañón Autónomo
        // Objetos_Script.pptx, diapositivas 11 a 15.

        private static void EscenaCanon()
        {
            Scene escena = TE.NuevaEscena();
            Material rojo = TE.Mat("Rojo", Rojo);
            Material metal = TE.Mat("Metal_Canon", new Color(0.25f, 0.27f, 0.30f), 0.55f, 0.6f);
            Material madera = TE.Mat("Madera", Madera);
            Material piso = TE.MatTransparente("Piso_RM", new Color(0.35f, 0.45f, 0.6f, 0.28f));

            // ---- Paso 1: Preparar el Prefab de la Bala
            //   "Crea una Esfera pequeña (Escala 0.2). Añádele un Rigidbody.
            //    Crea un material nuevo (ej. Negro o Rojo) y pónselo.
            //    Borra la esfera de la escena. (Ahora solo existe en tus archivos)."
            GameObject esfera = TE.Prim(PrimitiveType.Sphere, "Bala", null, Vector3.zero, Vector3.zero, Vector3.one * 0.2f, rojo);
            esfera.AddComponent<Rigidbody>();
            // ---- Paso 2: "Abre el prefab de la bala y añádele este script" (Bala: se destruye a los 3 segundos)
            esfera.AddComponent<Bala>();
            GameObject prefabBala = TE.GuardarComoPrefab(esfera, "Bala");

            GameObject raiz = TE.Vacio("Practica_3_Canon", null, Vector3.zero);
            TE.Prim(PrimitiveType.Plane, "Piso", raiz.transform, new Vector3(0f, 0f, 4.2f), Vector3.zero, new Vector3(0.4f, 1f, 0.95f), piso);

            // Montaje: objeto vacío en la culata; al girarlo (ApuntarRM) se mueven el cañón y su PuntoDisparo
            GameObject montaje = TE.Vacio("Montaje_Canon", raiz.transform, new Vector3(0f, 0.78f, 0.6f));
            montaje.AddComponent<ApuntarRM>();

            // ---- Paso 3: Crear el Cañón
            //   "Crea un Cilindro en la escena. Rótalo (90 grados en X) para que parezca un cañón apuntando al frente. Llámalo Cañon."
            GameObject canon = TE.Prim(PrimitiveType.Cylinder, "Cañon", montaje.transform, new Vector3(0f, 0f, 1f), new Vector3(90f, 0f, 0f), Vector3.one, metal);
            //   "Crea un Empty GameObject hijo del cilindro. Ponlo justo en la punta. Llámalo PuntoDisparo.
            //    (Esto es para que la bala no nazca dentro del cañón y explote)."
            // Al rotar el cilindro 90° en X su eje Y local queda apuntando al frente y su eje Z local hacia abajo;
            // por eso el PuntoDisparo va en Y local = 1.15 (fuera de la punta) y con rotación local (-90, 0, 0),
            // para que su flecha azul (forward) apunte hacia donde debe salir la bala.
            GameObject punto = TE.Vacio("PuntoDisparo", canon.transform, new Vector3(0f, 1.15f, 0f), new Vector3(-90f, 0f, 0f));

            //   "Crea un script ControlCañon." + Paso 4: programar el disparo (Assets/Scripts/ControlCañon.cs)
            // ---- Paso 5: Conexión
            //   "Selecciona tu objeto Cañon. Arrastra el script ControlCañon."
            ControlCañon control = canon.AddComponent<ControlCañon>();
            //   "Prefab Bala: arrastra tu Prefab de la esfera desde la carpeta Project."
            control.prefabBala = prefabBala;
            //   "Punto Salida: arrastra el objeto hijo PuntoDisparo desde la Jerarquía."
            control.puntoSalida = punto.transform;
            //   "Fuerza Disparo: prueba a poner 1000."
            control.fuerzaDisparo = 1000f;

            // Realidad mixta: el gatillo derecho del Quest 3 dispara igual que el clic del ratón
            GatilloRM gatillo = canon.AddComponent<GatilloRM>();
            gatillo.mensaje = "Disparar";

            // Cureña (adorno) y blancos para ver el efecto físico de las balas
            GameObject curena = TE.Vacio("Curena", raiz.transform, new Vector3(0f, 0f, 1.35f));
            TE.Adorno(PrimitiveType.Cube, "Soporte", curena.transform, new Vector3(0f, 0.13f, 0f), Vector3.zero, new Vector3(1.25f, 0.26f, 1.5f), madera);
            TE.Adorno(PrimitiveType.Cylinder, "Rueda_izquierda", curena.transform, new Vector3(-0.68f, 0.42f, -0.1f), new Vector3(0f, 0f, 90f), new Vector3(0.84f, 0.05f, 0.84f), madera);
            TE.Adorno(PrimitiveType.Cylinder, "Rueda_derecha", curena.transform, new Vector3(0.68f, 0.42f, -0.1f), new Vector3(0f, 0f, 90f), new Vector3(0.84f, 0.05f, 0.84f), madera);

            GameObject blancos = TE.Vacio("Blancos", raiz.transform, new Vector3(0f, 0f, 6.6f));
            Material[] colores = { TE.Mat("Azul", Azul), TE.Mat("Amarillo", Amarillo), TE.Mat("Verde", Verde) };
            int numero = 0;
            for (int fila = 0; fila < 3; fila++)
            {
                int enFila = 3 - fila;
                for (int i = 0; i < enFila; i++)
                {
                    numero++;
                    float x = (i - (enFila - 1) / 2f) * 0.42f;
                    GameObject caja = TE.Prim(PrimitiveType.Cube, "Blanco_" + numero.ToString("00"), blancos.transform, new Vector3(x, 0.2f + fila * 0.4f, 0f), Vector3.zero, Vector3.one * 0.4f, colores[fila]);
                    caja.AddComponent<Rigidbody>().mass = 0.5f;
                }
            }

            TE.Etiqueta("Gatillo derecho (o clic) = Disparar()\nInstantiate crea la bala · AddForce la empuja · Destroy la borra a los 3 s\nPalanca derecha (o flechas) = apuntar", raiz.transform, new Vector3(0f, 1.9f, 3.2f), 0.065f, TextoSuave);

            // Cámara de evidencia alta y en 3/4 desde atrás a la izquierda: desde atrás el tubo tapaba la trayectoria y los blancos
            TE.InfraestructuraRM(3, new Vector3(0f, 0.8f, 3.8f), 6.2f, 3.4f, 25f, 205f);
            TE.GuardarEscena(escena, "Canon_Autonomo");
        }

        // ================================================================== Físicas S1: Rigidbody y gravedad
        // Fisicas_1_Rigidbody_y_Gravedad.pptx, diapositivas 3 a 7. "Guardar la escena como: Fisica_Sesion1."

        private static void EscenaFisica1()
        {
            Scene escena = TE.NuevaEscena();
            Material piso = TE.MatTransparente("Piso_RM", new Color(0.35f, 0.45f, 0.6f, 0.28f));
            Material azul = TE.Mat("Azul", Azul), naranja = TE.Mat("Naranja", Naranja), verde = TE.Mat("Verde", Verde);
            Material rojo = TE.Mat("Rojo", Rojo), amarillo = TE.Mat("Amarillo", Amarillo), morado = TE.Mat("Morado", Morado), gris = TE.Mat("Gris", Gris);

            GameObject raiz = TE.Vacio("Fisica_Sesion1", null, Vector3.zero);

            // ---- Práctica 1, Paso 1: "crear un plano con Collider" (el Plane trae su Mesh Collider)
            TE.Prim(PrimitiveType.Plane, "Piso", raiz.transform, new Vector3(0f, 0f, 2.8f), Vector3.zero, new Vector3(0.52f, 1f, 0.62f), piso);

            // ---- Diapositiva 3: "Crear un cubo sin Rigidbody y preguntar por qué no cae"
            GameObject sinRb = TE.Prim(PrimitiveType.Cube, "Cubo_sin_Rigidbody", raiz.transform, new Vector3(0f, 1.7f, 3.9f), new Vector3(0f, 30f, 0f), Vector3.one * 0.3f, gris);
            TE.Etiqueta("Sin Rigidbody\nla física no lo mueve", raiz.transform, sinRb.transform.localPosition + new Vector3(0f, 0.36f, 0f), 0.045f, TextoSuave);

            // ---- Diapositiva 5: Experimento de masa y gravedad
            //   "1. Crear tres cubos iguales. 2. Colocarlos a la misma altura. 3. Asignar masas 1, 10 y 100.
            //    4. Añadir Rigidbody a cada cubo. 5. Ejecutar la escena y comparar."
            GameObject experimento = TE.Vacio("Experimento_masa_y_gravedad", raiz.transform, new Vector3(-1.15f, 0f, 2.4f));
            experimento.AddComponent<ReinicioFisica>();
            experimento.AddComponent<EmpujeDePrueba>();
            float[] masas = { 1f, 10f, 100f };
            Material[] coloresMasa = { azul, naranja, rojo };
            for (int i = 0; i < 3; i++)
            {
                Vector3 pos = new Vector3((i - 1) * 0.5f, 2.1f, 0f);
                GameObject cubo = TE.Prim(PrimitiveType.Cube, "Cubo_masa_" + masas[i].ToString("0"), experimento.transform, pos, Vector3.zero, Vector3.one * 0.3f, coloresMasa[i]);
                cubo.AddComponent<Rigidbody>().mass = masas[i];
                TE.Etiqueta("Masa " + masas[i].ToString("0"), experimento.transform, pos + new Vector3(0f, 0.3f, 0f), 0.055f, Texto, TextAnchor.MiddleCenter, "Etiqueta_masa_" + masas[i].ToString("0"));
            }
            TE.Etiqueta("<b>Experimento: masa y gravedad</b>\n3 cubos iguales, misma altura\nCaen al mismo tiempo.\n(B) mismo empuje a los tres\n(A) repetir", experimento.transform, new Vector3(0f, 2.95f, 0f), 0.045f, TextoSuave);

            // ---- Práctica 1: escena de caída
            //   "Paso 2: añadir tres objetos a diferentes alturas. Paso 3: configurar Rigidbody y gravedad.
            //    Paso 4: modificar masa, Drag e Is Kinematic. Paso 5: registrar lo observado."
            GameObject caida = TE.Vacio("Practica_caida", raiz.transform, new Vector3(1.15f, 0f, 2.4f));
            caida.AddComponent<ReinicioFisica>();

            GameObject a = TE.Prim(PrimitiveType.Sphere, "Objeto_A_Esfera", caida.transform, new Vector3(-0.5f, 1.2f, 0f), Vector3.zero, Vector3.one * 0.3f, verde);
            Rigidbody rbA = a.AddComponent<Rigidbody>();   // valores por defecto: masa 1, Drag 0, Use Gravity activado
            rbA.mass = 1f;
            TE.Etiqueta("A · altura 1.2\nMasa 1 · Drag 0", caida.transform, new Vector3(-0.5f, 1.55f, 0f), 0.04f, Texto, TextAnchor.MiddleCenter, "Etiqueta_A");

            GameObject b = TE.Prim(PrimitiveType.Cube, "Objeto_B_Cubo", caida.transform, new Vector3(0f, 1.8f, 0f), Vector3.zero, Vector3.one * 0.3f, amarillo);
            Rigidbody rbB = b.AddComponent<Rigidbody>();
            rbB.mass = 5f;
            rbB.drag = 5f;                                 // resistencia al movimiento: cae más lento
            TE.Etiqueta("B · altura 1.8\nMasa 5 · Drag 5", caida.transform, new Vector3(0f, 2.15f, 0f), 0.04f, Texto, TextAnchor.MiddleCenter, "Etiqueta_B");

            GameObject c = TE.Prim(PrimitiveType.Capsule, "Objeto_C_Capsula", caida.transform, new Vector3(0.5f, 2.4f, 0f), Vector3.zero, new Vector3(0.24f, 0.2f, 0.24f), morado);
            Rigidbody rbC = c.AddComponent<Rigidbody>();
            rbC.isKinematic = true;                        // ni la gravedad ni las fuerzas lo controlan: se queda flotando
            TE.Etiqueta("C · altura 2.4\nIs Kinematic", caida.transform, new Vector3(0.5f, 2.72f, 0f), 0.04f, Texto, TextAnchor.MiddleCenter, "Etiqueta_C");

            TE.Etiqueta("<b>Práctica 1: escena de caída</b>\n3 objetos a diferentes alturas\ncambiando masa, Drag e Is Kinematic", caida.transform, new Vector3(0f, 3.15f, 0f), 0.045f, TextoSuave);

            TE.InfraestructuraRM(4, new Vector3(0f, 1.5f, 2.6f), 3.6f, 1.8f, 26f);
            TE.GuardarEscena(escena, "Fisica_Sesion1");
        }

        // ================================================================== Físicas S2: Colliders y colisiones
        // Fisicas_2_Colliders_y_Colisiones.pptx, diapositivas 3 a 7.

        private static void EscenaFisica2()
        {
            Scene escena = TE.NuevaEscena();
            Material pista = TE.Mat("Pista", new Color(0.32f, 0.35f, 0.40f), 0.1f);
            Material rampaMat = TE.Mat("Rampa", new Color(0.85f, 0.62f, 0.25f), 0.1f);
            Material pared = TE.MatTransparente("Pared_Pista", new Color(0.55f, 0.8f, 1f, 0.35f));
            Material rojo = TE.Mat("Rojo", Rojo), verde = TE.Mat("Verde", Verde), morado = TE.Mat("Morado", Morado), amarillo = TE.Mat("Amarillo", Amarillo);
            Material blanco = TE.Mat("Blanco", Blanco);

            GameObject raiz = TE.Vacio("Fisica_Sesion2", null, Vector3.zero);
            raiz.AddComponent<ReinicioFisica>();   // (A) regresa las pelotas al inicio

            // ---- Diapositiva 4: Construcción de la pista
            GameObject pistaGo = TE.Vacio("Pista", raiz.transform, Vector3.zero);

            //   "1. Crear un piso."  (Box Collider)
            GameObject piso = TE.Prim(PrimitiveType.Cube, "Piso", pistaGo.transform, new Vector3(0f, -0.025f, 2.9f), Vector3.zero, new Vector3(1.3f, 0.05f, 5.0f), pista);
            //   Prueba: "Desactivar temporalmente el Collider del piso y observar el resultado" (gatillo izquierdo / tecla C)
            AlternarCollider alternar = piso.AddComponent<AlternarCollider>();
            alternar.objetivo = piso.GetComponent<Collider>();

            //   "2. Añadir una rampa inclinada."  Padre vacío inclinado 25°; la superficie y los rieles son sus hijos
            GameObject rampa = TE.Vacio("Rampa", pistaGo.transform, new Vector3(0f, 0.41f, 1.45f), new Vector3(25f, 0f, 0f));
            TE.Prim(PrimitiveType.Cube, "Superficie", rampa.transform, Vector3.zero, Vector3.zero, new Vector3(1.2f, 0.05f, 1.7f), rampaMat);
            TE.Prim(PrimitiveType.Cube, "Riel_izquierdo", rampa.transform, new Vector3(-0.58f, 0.085f, 0f), Vector3.zero, new Vector3(0.04f, 0.12f, 1.7f), pared);
            TE.Prim(PrimitiveType.Cube, "Riel_derecho", rampa.transform, new Vector3(0.58f, 0.085f, 0f), Vector3.zero, new Vector3(0.04f, 0.12f, 1.7f), pared);

            //   "3. Construir dos paredes laterales."
            TE.Prim(PrimitiveType.Cube, "Pared_izquierda", pistaGo.transform, new Vector3(-0.675f, 0.2f, 2.9f), Vector3.zero, new Vector3(0.05f, 0.4f, 5.0f), pared);
            TE.Prim(PrimitiveType.Cube, "Pared_derecha", pistaGo.transform, new Vector3(0.675f, 0.2f, 2.9f), Vector3.zero, new Vector3(0.05f, 0.4f, 5.0f), pared);
            TE.Prim(PrimitiveType.Cube, "Tope_final", pistaGo.transform, new Vector3(0f, 0.2f, 5.425f), Vector3.zero, new Vector3(1.4f, 0.4f, 0.05f), pared);

            //   "4. Añadir tres obstáculos."  Uno con cada tipo de Collider de la diapositiva 3
            GameObject obstaculos = TE.Vacio("Obstaculos", pistaGo.transform, Vector3.zero);
            TE.Prim(PrimitiveType.Cube, "Obstaculo_1_BoxCollider", obstaculos.transform, new Vector3(-0.28f, 0.15f, 2.95f), new Vector3(0f, 35f, 0f), Vector3.one * 0.3f, rojo);
            GameObject cilindro = TE.Prim(PrimitiveType.Cylinder, "Obstaculo_2_MeshCollider", obstaculos.transform, new Vector3(0.3f, 0.15f, 3.65f), Vector3.zero, new Vector3(0.28f, 0.15f, 0.28f), morado);
            UnityEngine.Object.DestroyImmediate(cilindro.GetComponent<Collider>());
            MeshCollider malla = cilindro.AddComponent<MeshCollider>();   // sigue la geometría exacta: requiere más procesamiento
            malla.sharedMesh = cilindro.GetComponent<MeshFilter>().sharedMesh;
            malla.convex = true;
            TE.Prim(PrimitiveType.Capsule, "Obstaculo_3_CapsuleCollider", obstaculos.transform, new Vector3(-0.15f, 0.13f, 4.4f), new Vector3(0f, 25f, 90f), new Vector3(0.26f, 0.3f, 0.26f), verde);

            //   "5. Colocar una pelota con Sphere Collider y Rigidbody."
            Vector3 arriba = rampa.transform.position + rampa.transform.rotation * new Vector3(0f, 0.025f + 0.11f + 0.01f, -0.68f);
            GameObject pelota = TE.Prim(PrimitiveType.Sphere, "Pelota", raiz.transform, arriba, Vector3.zero, Vector3.one * 0.22f, amarillo);
            pelota.AddComponent<Rigidbody>();
            // ---- Diapositiva 6: "Crear el script, añadirlo a la pelota y revisar la consola" (DetectorColision)
            pelota.AddComponent<DetectorColision>();

            // ---- Diapositiva 5: Detección de colisiones rápidas (Discrete vs Continuous)
            //   "Aumentar la velocidad de la pelota y comparar Discrete con Continuous."
            GameObject carril = TE.Vacio("Experimento_Discrete_vs_Continuous", raiz.transform, new Vector3(1.75f, 0f, 0f));
            TE.Adorno(PrimitiveType.Cube, "Carril", carril.transform, new Vector3(0f, -0.01f, 3.9f), Vector3.zero, new Vector3(1.0f, 0.02f, 7.8f), pista);
            TE.Prim(PrimitiveType.Cube, "Pared_delgada", carril.transform, new Vector3(0f, 0.36f, 3.4f), Vector3.zero, new Vector3(1.0f, 0.7f, 0.02f), blanco);
            TE.Prim(PrimitiveType.Cube, "Tope_lejano", carril.transform, new Vector3(0f, 0.36f, 7.05f), Vector3.zero, new Vector3(1.0f, 0.7f, 1.5f), pared);

            PelotaRapida("Pelota_Discrete", carril.transform, new Vector3(-0.22f, 0.36f, 0.4f), CollisionDetectionMode.Discrete, rojo, TE.MatEstela("Estela_Roja", new Color(1f, 0.3f, 0.25f, 0.9f)));
            PelotaRapida("Pelota_Continuous", carril.transform, new Vector3(0.22f, 0.36f, 0.4f), CollisionDetectionMode.Continuous, verde, TE.MatEstela("Estela_Verde", new Color(0.3f, 1f, 0.45f, 0.9f)));

            TE.Etiqueta("<b>Discrete</b> (roja)\natraviesa la pared", carril.transform, new Vector3(-0.3f, 1.0f, 3.4f), 0.045f, new Color(1f, 0.55f, 0.5f), TextAnchor.MiddleCenter, "Etiqueta_Discrete");
            TE.Etiqueta("<b>Continuous</b> (verde)\nsí choca", carril.transform, new Vector3(0.3f, 1.0f, 3.4f), 0.045f, new Color(0.55f, 1f, 0.65f), TextAnchor.MiddleCenter, "Etiqueta_Continuous");

            // Consola flotante: los Debug.Log de DetectorColision se ven dentro del visor
            GameObject consola = TE.Vacio("Consola_RM", raiz.transform, new Vector3(-1.55f, 1.45f, 3.1f), new Vector3(0f, -28f, 0f));
            TE.Adorno(PrimitiveType.Quad, "Fondo", consola.transform, new Vector3(0f, -0.22f, 0.01f), Vector3.zero, new Vector3(1.15f, 0.62f, 1f), TE.MatTransparente("Consola_Fondo", new Color(0.05f, 0.07f, 0.1f, 0.6f)));
            ConsolaRM cr = consola.AddComponent<ConsolaRM>();
            cr.titulo = "Consola (Debug.Log)";
            cr.texto = TE.Etiqueta("Consola (Debug.Log)", consola.transform, new Vector3(-0.53f, 0.05f, 0f), 0.042f, new Color(0.75f, 1f, 0.8f), TextAnchor.UpperLeft, "Texto");

            TE.Etiqueta("(A) repetir · gatillo izquierdo: quitar el Collider del piso", raiz.transform, new Vector3(0f, 1.25f, 5.5f), 0.05f, TextoSuave);

            TE.InfraestructuraRM(5, new Vector3(0.5f, 0.45f, 3.0f), 5.2f, 2.6f, 32f);
            TE.GuardarEscena(escena, "Fisica_Sesion2");
        }

        private static void PelotaRapida(string nombre, Transform padre, Vector3 pos, CollisionDetectionMode modo, Material mat, Material estela)
        {
            GameObject pelota = TE.Prim(PrimitiveType.Sphere, nombre, padre, pos, Vector3.zero, Vector3.one * 0.1f, mat);
            Rigidbody rb = pelota.AddComponent<Rigidbody>();
            rb.useGravity = false;                 // trayectoria recta, para comparar solo el modo de detección
            rb.collisionDetectionMode = modo;
            LanzadorRapido lanzador = pelota.AddComponent<LanzadorRapido>();
            lanzador.velocidad = 60f;
            pelota.AddComponent<DetectorColision>();

            TrailRenderer tr = pelota.AddComponent<TrailRenderer>();
            tr.sharedMaterial = estela;
            tr.time = 1.5f;
            tr.startWidth = 0.07f;
            tr.endWidth = 0.01f;
            tr.minVertexDistance = 0.02f;
            tr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }
    }
}
