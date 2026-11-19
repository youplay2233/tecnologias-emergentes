using UnityEngine;

// Bote de basura holográfico para el minijuego: lo que se lanza adentro cuenta como encestado.
// Las piezas tiradas a la basura reaparecen cuando se vuelve a armar o despiezar el robot.
public class BoteBasura : MonoBehaviour
{
    public static BoteBasura Instancia { get; private set; }

    public float radio = 0.17f;
    public float alto = 0.42f;

    private int encestadas, tiros;
    private TextMesh marcador;
    private LineRenderer[] lineas;
    private float destello;
    private Color colorDestello = Holo.Verde;
    private Material materialCristal;

    void Awake()
    {
        Instancia = this;
        Holo.Arco(transform, "Fondo", radio * 0.85f, 0f, 360f, Holo.CianTenue, 0.003f, true);
        Holo.Arco(transform, "Boca", radio, 0f, 360f, Holo.Cian, 0.006f, true).transform.localPosition = Vector3.up * alto;
        for (int i = 0; i < 12; i++)
        {
            float a = i * 30f * Mathf.Deg2Rad;
            Vector3 d = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
            LineRenderer l = Holo.Linea(transform, "Varilla_" + i, Holo.CianTenue, 0.0025f, false, 2);
            l.SetPosition(0, d * radio * 0.85f);
            l.SetPosition(1, d * radio + Vector3.up * alto);
        }
        lineas = GetComponentsInChildren<LineRenderer>();

        // Cuerpo de cristal holográfico: le da volumen para ver bien dónde está y dónde agarrarlo
        GameObject cristal = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        cristal.name = "Cristal";
        Destroy(cristal.GetComponent<Collider>());
        cristal.transform.SetParent(transform, false);
        cristal.transform.localPosition = Vector3.up * alto * 0.5f;
        cristal.transform.localScale = new Vector3(radio * 1.9f, alto * 0.5f, radio * 1.9f);   // el cilindro de Unity mide 2 de alto
        Renderer r = cristal.GetComponent<Renderer>();
        r.sharedMaterial = Holo.MatPanel;
        r.material.color = new Color(0.08f, 0.35f, 0.45f, 0.28f);
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        materialCristal = r.material;
        Holo.Arco(transform, "Anillo_medio", radio * 0.93f, 0f, 360f, Holo.CianTenue, 0.002f, true).transform.localPosition = Vector3.up * alto * 0.5f;

        // Se puede agarrar con la mano o el control (cinemático: no cae ni lo empujan los proyectiles)
        Rigidbody cuerpo = gameObject.AddComponent<Rigidbody>();
        cuerpo.isKinematic = true;
        cuerpo.useGravity = false;

        // Zona de "encestado": dentro del bote, justo debajo de la boca
        BoxCollider zona = gameObject.AddComponent<BoxCollider>();
        zona.isTrigger = true;
        zona.center = Vector3.up * alto * 0.55f;
        zona.size = new Vector3(radio * 1.7f, alto * 0.9f, radio * 1.7f);

        marcador = Holo.Texto("", transform, Vector3.up * (alto + 0.09f), 0.032f, Holo.Cian, TextAnchor.MiddleCenter, "Marcador");
        Pintar();
    }

    void OnDestroy()
    {
        if (Instancia == this) Instancia = null;
    }

    private float piso;
    private Rigidbody cuerpoBote;

    void Start()
    {
        piso = transform.position.y;
        cuerpoBote = GetComponent<Rigidbody>();
    }

    // Al soltarlo se queda flotando donde lo dejaste (como un holograma); ManoRM lo mantiene derecho
    // mientras lo cargas. Con la voz ("pon el bote a mi izquierda") vuelve al piso.
    public bool Agarrado { get { return cuerpoBote != null && ManoRM.Agarrados.Contains(cuerpoBote); } }

    // Con la voz: "izquierda", "derecha", "frente" o "atras", respecto a donde mira el usuario
    public string Mover(string donde)
    {
        Camera cam = Camera.main;
        if (cam == null) return "No sé dónde está el usuario.";
        Vector3 frente = Vector3.ProjectOnPlane(cam.transform.forward, Vector3.up).normalized;
        if (frente.sqrMagnitude < 0.01f) frente = Vector3.forward;
        Vector3 derecha = Vector3.Cross(Vector3.up, frente);
        Vector3 d;
        switch (donde)
        {
            case "izquierda": d = -derecha * 0.7f + frente * 0.3f; break;
            case "derecha": d = derecha * 0.7f + frente * 0.3f; break;
            case "atras": d = -frente * 0.8f; break;
            default: d = frente * 0.9f; donde = "frente"; break;
        }
        Vector3 p = cam.transform.position + d;
        transform.position = new Vector3(p.x, piso, p.z);
        Holo.Sonido("abrir", transform.position, 0.3f);
        return "Bote de basura a la " + (donde == "frente" ? "vista, enfrente" : donde == "atras" ? "espalda del usuario" : donde) + ".";
    }

    public static void Registrar(bool encesto, Vector3 donde)
    {
        if (Instancia == null) return;
        Instancia.tiros++;
        if (encesto) Instancia.encestadas++;
        Instancia.destello = 1f;
        Instancia.colorDestello = encesto ? Holo.Verde : Holo.Rojo;
        Holo.Sonido(encesto ? "confirmar" : "cerrar", donde, 0.4f);
        Instancia.Pintar();
        Debug.Log("[Jarvis] Basura: " + (encesto ? "encestada" : "fallada") + " (" + Instancia.encestadas + " / " + Instancia.tiros + ")");
    }

    // Velocidad para que algo que sale de "desde" caiga en la boca del bote en "segundos"
    public Vector3 VelocidadHacia(Vector3 desde, float segundos)
    {
        Vector3 hacia = transform.position + Vector3.up * alto * 0.6f;
        return (hacia - desde) / segundos - 0.5f * Physics.gravity * segundos;
    }

    private void Pintar()
    {
        marcador.text = "BASURA\n<size=60>" + encestadas + " / " + tiros + "</size>";
    }

    void Update()
    {
        // El marcador mira al usuario y el bote destella verde (encestó) o rojo (falló)
        Camera cam = Camera.main;
        if (cam != null) marcador.transform.rotation = Quaternion.LookRotation(marcador.transform.position - cam.transform.position);
        destello = Mathf.MoveTowards(destello, 0f, Time.deltaTime * 2f);
        if (materialCristal != null)
        {
            Color baseCristal = Agarrado ? new Color(0.15f, 0.55f, 0.7f, 0.4f) : new Color(0.08f, 0.35f, 0.45f, 0.28f);
            materialCristal.color = Color.Lerp(baseCristal, colorDestello * 0.6f + new Color(0, 0, 0, 0.4f), destello);
        }
        foreach (LineRenderer l in lineas)
        {
            Color c = Color.Lerp(Holo.Cian, colorDestello, destello);
            c.a = l.startColor.a;
            l.startColor = l.endColor = c;
        }
    }
}
