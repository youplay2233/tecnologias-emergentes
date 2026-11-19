using UnityEngine;

// El "reactor" de Jarvis: una esfera holográfica tipo giroscopio (como el holograma de la película),
// con meridianos, anillos que giran en distintos ejes, arcos de datos y chispas.
//   azul = reposo · amarillo = escuchando (y pensando, girando rápido) · verde = respondiendo · rojo = sin enlace
public class NucleoJarvis : MonoBehaviour
{
    private const float R = 0.11f;   // radio de la esfera

    private static readonly Color Azul = new Color(0.25f, 0.6f, 1f, 1f);
    private static readonly Color Amarillo = new Color(1f, 0.8f, 0.2f, 1f);
    private static readonly Color Verde = new Color(0.3f, 1f, 0.45f, 1f);
    private static readonly Color Rojo = new Color(1f, 0.25f, 0.2f, 1f);

    private Transform esfera;
    private Transform[] anillos;
    private Vector3[] ejesAnillos;
    private Transform[] arcos;
    private Vector3[] ejesArcos;
    private float[] velArcos;
    private LineRenderer[] lineas;
    private Material materialCentro;
    private Transform centro;
    private ParticleSystem chispas;
    private TextMesh rotulo;
    private Color color = Azul;
    private string estado = "reposo";
    private bool conectado;

    void Awake()
    {
        // 1. Esfera de alambre: meridianos y paralelos, gira despacio
        esfera = Capa("Esfera");
        for (int i = 0; i < 6; i++)
        {
            Holo.Arco(esfera, "Meridiano_" + i, R, 0f, 360f, Tenue(0.3f), 0.0012f).transform.localRotation = Quaternion.Euler(0f, i * 30f, 0f);
        }
        foreach (float h in new[] { -0.6f, -0.3f, 0f, 0.3f, 0.6f })
        {
            float r = R * Mathf.Sqrt(1f - h * h);
            Holo.Arco(esfera, "Paralelo", r, 0f, 360f, Tenue(0.22f), 0.001f, true).transform.localPosition = Vector3.up * (h * R);
        }

        // 2. Anillos giroscópicos alrededor de la esfera, cada uno con su eje
        float[] radios = { R * 1.18f, R * 1.32f, R * 1.48f };
        float[] grados = { 360f, 300f, 220f };
        anillos = new Transform[radios.Length];
        ejesAnillos = new[] { new Vector3(1f, 0.3f, 0f), new Vector3(0.2f, 1f, 0.4f), new Vector3(0.5f, 0f, 1f) };
        for (int i = 0; i < radios.Length; i++)
        {
            anillos[i] = Capa("Anillo_" + i);
            anillos[i].localRotation = Random.rotation;
            Holo.Arco(anillos[i], "Arco", radios[i], 0f, grados[i], Tenue(0.85f), 0.004f - i * 0.0008f);
            for (int k = 0; k < 24; k++)   // marcas como de instrumento
            {
                float a = k * 15f * Mathf.Deg2Rad;
                if (k * 15f > grados[i]) break;
                Vector3 d = new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f);
                LineRenderer m = Holo.Linea(anillos[i], "Marca", Tenue(0.5f), 0.001f, false, 2);
                m.SetPosition(0, d * radios[i]);
                m.SetPosition(1, d * (radios[i] + (k % 3 == 0 ? 0.012f : 0.006f)));
            }
        }

        // 3. Arcos de datos: trazos cortos que corren rápido sobre la esfera
        arcos = new Transform[10];
        ejesArcos = new Vector3[arcos.Length];
        velArcos = new float[arcos.Length];
        for (int i = 0; i < arcos.Length; i++)
        {
            arcos[i] = Capa("Datos_" + i);
            arcos[i].localRotation = Random.rotation;
            ejesArcos[i] = Random.onUnitSphere;
            velArcos[i] = Random.Range(40f, 140f) * (Random.value < 0.5f ? -1f : 1f);
            Holo.Arco(arcos[i], "Trazo", R * Random.Range(0.95f, 1.25f), 0f, Random.Range(15f, 60f), Tenue(1f), 0.0025f, false, 32);
        }

        // 4. Núcleo brillante
        GameObject esf = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        esf.name = "Centro";
        Destroy(esf.GetComponent<Collider>());
        centro = esf.transform;
        centro.SetParent(transform, false);
        centro.localScale = Vector3.one * 0.035f;
        Renderer rc = esf.GetComponent<Renderer>();
        if (Holo.MatPiezaResalte != null) rc.sharedMaterial = materialCentro = new Material(Holo.MatPiezaResalte);
        Holo.Arco(transform, "Anillo_centro", 0.028f, 0f, 360f, Tenue(0.9f), 0.002f);

        // 5. Chispas alrededor
        CrearChispas();

        lineas = GetComponentsInChildren<LineRenderer>();
        rotulo = Holo.Texto("J.A.R.V.I.S.", transform, new Vector3(0f, -R * 1.75f, 0f), 0.026f, Azul, TextAnchor.MiddleCenter, "Rotulo");
    }

    private Transform Capa(string nombre)
    {
        Transform t = new GameObject(nombre).transform;
        t.SetParent(transform, false);
        return t;
    }

    private static Color Tenue(float alfa)
    {
        return new Color(1f, 1f, 1f, alfa);   // el color real se pone cada cuadro según el estado; aquí solo importa el alfa
    }

    private void CrearChispas()
    {
        GameObject go = new GameObject("Chispas");
        go.transform.SetParent(transform, false);
        chispas = go.AddComponent<ParticleSystem>();
        ParticleSystem.MainModule main = chispas.main;
        main.startLifetime = 1.4f;
        main.startSpeed = 0.025f;
        main.startSize = new ParticleSystem.MinMaxCurve(0.002f, 0.006f);
        main.maxParticles = 220;
        main.simulationSpace = ParticleSystemSimulationSpace.Local;
        ParticleSystem.EmissionModule emision = chispas.emission;
        emision.rateOverTime = 45f;
        ParticleSystem.ShapeModule forma = chispas.shape;
        forma.shapeType = ParticleSystemShapeType.Sphere;
        forma.radius = R * 1.15f;
        forma.radiusThickness = 0.2f;
        ParticleSystem.ColorOverLifetimeModule vida = chispas.colorOverLifetime;
        vida.enabled = true;
        Gradient g = new Gradient();
        g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                  new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.25f), new GradientAlphaKey(0f, 1f) });
        vida.color = g;
        ParticleSystemRenderer r = go.GetComponent<ParticleSystemRenderer>();
        r.sharedMaterial = Holo.MatLinea;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        r.receiveShadows = false;
        chispas.Play();
    }

    public void Estado(string nuevo, bool hayEnlace)
    {
        estado = string.IsNullOrEmpty(nuevo) ? "reposo" : nuevo;
        conectado = hayEnlace;
    }

    void Update()
    {
        float vel, pulso, emision;
        Color objetivo;
        string texto;
        if (!conectado) { vel = 15f; pulso = 0.03f; objetivo = Rojo; texto = "SIN ENLACE"; emision = 10f; }
        else
        {
            switch (estado)
            {
                case "escuchando": vel = 50f; pulso = 0.07f; objetivo = Amarillo; texto = "ESCUCHANDO"; emision = 60f; break;
                case "pensando": vel = 200f; pulso = 0.04f; objetivo = Amarillo; texto = "PROCESANDO"; emision = 110f; break;
                case "hablando": vel = 45f; pulso = 0.1f; objetivo = Verde; texto = "J.A.R.V.I.S."; emision = 70f; break;
                default: vel = 18f; pulso = 0.025f; objetivo = Azul; texto = "J.A.R.V.I.S."; emision = 35f; break;
            }
        }
        color = Color.Lerp(color, objetivo, 1f - Mathf.Exp(-6f * Time.deltaTime));

        float dt = Time.deltaTime;
        esfera.Rotate(Vector3.up, vel * 0.25f * dt, Space.Self);
        for (int i = 0; i < anillos.Length; i++) anillos[i].Rotate(ejesAnillos[i], vel * (0.6f + 0.35f * i) * (i % 2 == 0 ? 1f : -1f) * dt, Space.Self);
        for (int i = 0; i < arcos.Length; i++) arcos[i].Rotate(ejesArcos[i], velArcos[i] * (vel / 30f) * dt, Space.Self);

        float latido = 1f + Mathf.Sin(Time.time * (estado == "hablando" ? 9f : 3f)) * pulso;
        centro.localScale = Vector3.one * 0.035f * latido;
        esfera.localScale = Vector3.one * (1f + (latido - 1f) * 0.4f);

        foreach (LineRenderer lr in lineas)
        {
            Color c = color;
            c.a = lr.startColor.a;
            lr.startColor = lr.endColor = c;
        }
        if (materialCentro != null) materialCentro.SetColor("_EmissionColor", color * 1.8f);
        ParticleSystem.MainModule main = chispas.main;
        main.startColor = color;
        ParticleSystem.EmissionModule em = chispas.emission;
        em.rateOverTime = emision;
        rotulo.text = texto;
        rotulo.color = color;
    }
}
