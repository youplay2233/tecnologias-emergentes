using UnityEngine;
using UnityEngine.Rendering;

// Piezas sueltas del estilo holográfico: colores, materiales, texto 3D, líneas y sonidos.
// Los materiales los crea el constructor de la escena en Assets/Resources/JarvisRM, así entran a la build.
public static class Holo
{
    public static readonly Color Cian = new Color(0.35f, 0.92f, 1f, 1f);
    public static readonly Color CianTenue = new Color(0.35f, 0.92f, 1f, 0.35f);
    public static readonly Color Ambar = new Color(1f, 0.72f, 0.25f, 1f);
    public static readonly Color Verde = new Color(0.35f, 1f, 0.6f, 1f);
    public static readonly Color Rojo = new Color(1f, 0.32f, 0.28f, 1f);
    public static readonly Color Blanco = new Color(0.9f, 0.97f, 1f, 1f);

    private static Material linea, texto, panel, piezaBase, piezaResalte;
    private static Font fuente;

    public static Material MatLinea { get { return linea != null ? linea : (linea = Cargar("Holo_Linea")); } }
    public static Material MatPanel { get { return panel != null ? panel : (panel = Cargar("Holo_Panel")); } }
    public static Material MatPiezaBase { get { return piezaBase != null ? piezaBase : (piezaBase = Cargar("Pieza_Base")); } }
    public static Material MatPiezaResalte { get { return piezaResalte != null ? piezaResalte : (piezaResalte = Cargar("Pieza_Resalte")); } }

    private static Material Cargar(string nombre)
    {
        Material m = Resources.Load<Material>("JarvisRM/" + nombre);
        if (m == null) Debug.LogWarning("[Jarvis] Falta el material JarvisRM/" + nombre + ": vuelve a construir la escena (menú Jarvis).");
        return m;
    }

    // ------------------------------------------------------------------ texto

    public static TextMesh Texto(string contenido, Transform padre, Vector3 pos, float altoDeLinea, Color color,
                                 TextAnchor ancla = TextAnchor.MiddleCenter, string nombre = "Texto")
    {
        GameObject go = new GameObject(nombre);
        go.transform.SetParent(padre, false);
        go.transform.localPosition = pos;
        TextMesh tm = go.AddComponent<TextMesh>();
        MeshRenderer mr = go.GetComponent<MeshRenderer>();
        if (fuente == null) fuente = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (texto == null) texto = Cargar("Texto3D");
        tm.font = fuente;
        if (texto != null) mr.sharedMaterial = texto;
        else if (fuente != null) mr.sharedMaterial = fuente.material;
        mr.shadowCastingMode = ShadowCastingMode.Off;
        mr.receiveShadows = false;
        tm.richText = true;
        tm.fontSize = 80;
        tm.characterSize = altoDeLinea / 9.2f;
        tm.anchor = ancla;
        tm.alignment = ancla == TextAnchor.UpperLeft || ancla == TextAnchor.MiddleLeft || ancla == TextAnchor.LowerLeft
            ? TextAlignment.Left : TextAlignment.Center;
        tm.color = color;
        tm.text = contenido;
        return tm;
    }

    // TextMesh no parte renglones: se parte el texto por palabras
    public static string Envolver(string t, int ancho)
    {
        if (string.IsNullOrEmpty(t)) return "";
        System.Text.StringBuilder sb = new System.Text.StringBuilder();
        foreach (string parrafo in t.Replace("\r", "").Split('\n'))
        {
            int enLinea = 0;
            foreach (string palabra in parrafo.Split(' '))
            {
                if (palabra.Length == 0) continue;
                if (enLinea > 0 && enLinea + 1 + palabra.Length > ancho) { sb.Append('\n'); enLinea = 0; }
                if (enLinea > 0) { sb.Append(' '); enLinea++; }
                sb.Append(palabra);
                enLinea += palabra.Length;
            }
            sb.Append('\n');
        }
        return sb.ToString().TrimEnd('\n');
    }

    // ------------------------------------------------------------------ líneas

    public static LineRenderer Linea(Transform padre, string nombre, Color color, float ancho, bool espacioMundo, int puntos)
    {
        GameObject go = new GameObject(nombre);
        go.transform.SetParent(padre, false);
        LineRenderer lr = go.AddComponent<LineRenderer>();
        lr.sharedMaterial = MatLinea;
        lr.useWorldSpace = espacioMundo;
        lr.positionCount = puntos;
        lr.widthMultiplier = ancho;
        lr.startColor = color;
        lr.endColor = color;
        lr.numCapVertices = 2;
        lr.shadowCastingMode = ShadowCastingMode.Off;
        lr.receiveShadows = false;
        return lr;
    }

    // Arco o círculo en el plano XY local (de frente al usuario) o XZ (horizontal)
    public static LineRenderer Arco(Transform padre, string nombre, float radio, float desdeGrados, float grados, Color color,
                                    float ancho, bool horizontal = false, int segmentos = 64)
    {
        int n = Mathf.Max(2, Mathf.CeilToInt(segmentos * grados / 360f) + 1);
        LineRenderer lr = Linea(padre, nombre, color, ancho, false, n);
        for (int i = 0; i < n; i++)
        {
            float a = (desdeGrados + grados * i / (n - 1)) * Mathf.Deg2Rad;
            Vector3 p = horizontal ? new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) : new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f);
            lr.SetPosition(i, p * radio);
        }
        lr.loop = grados >= 359.9f;
        return lr;
    }

    public static void Rectangulo(LineRenderer lr, float ancho, float alto)
    {
        float x = ancho / 2f, y = alto / 2f;
        lr.positionCount = 4;
        lr.loop = true;
        lr.SetPosition(0, new Vector3(-x, y, 0f));
        lr.SetPosition(1, new Vector3(x, y, 0f));
        lr.SetPosition(2, new Vector3(x, -y, 0f));
        lr.SetPosition(3, new Vector3(-x, -y, 0f));
    }

    // Esquinas tipo "visor de mira" alrededor de un rectángulo
    public static void Esquinas(Transform padre, float ancho, float alto, float largo, Color color, float grosor)
    {
        float x = ancho / 2f, y = alto / 2f;
        Vector3[] c = { new Vector3(-x, y), new Vector3(x, y), new Vector3(x, -y), new Vector3(-x, -y) };
        Vector3[] h = { Vector3.right, Vector3.left, Vector3.left, Vector3.right };
        Vector3[] v = { Vector3.down, Vector3.down, Vector3.up, Vector3.up };
        for (int i = 0; i < 4; i++)
        {
            LineRenderer lr = Linea(padre, "Esquina_" + i, color, grosor, false, 3);
            lr.SetPosition(0, c[i] + h[i] * largo);
            lr.SetPosition(1, c[i]);
            lr.SetPosition(2, c[i] + v[i] * largo);
        }
    }

    public static GameObject Placa(Transform padre, string nombre, Vector3 pos, Vector2 tam, Color color)
    {
        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Quad);
        go.name = nombre;
        Object.Destroy(go.GetComponent<Collider>());
        go.transform.SetParent(padre, false);
        go.transform.localPosition = pos;
        go.transform.localScale = new Vector3(tam.x, tam.y, 1f);
        Renderer r = go.GetComponent<Renderer>();
        r.sharedMaterial = MatPanel;
        r.material.color = color;
        r.shadowCastingMode = ShadowCastingMode.Off;
        r.receiveShadows = false;
        return go;
    }

    // ------------------------------------------------------------------ sonidos (sintetizados, sin archivos)

    private static AudioClip blip, confirmar, abrir, cerrar;

    public static void Sonido(string cual, Vector3 donde, float volumen = 0.35f)
    {
        AudioClip clip;
        switch (cual)
        {
            case "confirmar": clip = confirmar != null ? confirmar : (confirmar = Tono("confirmar", 0.16f, 880f, 1320f)); break;
            case "abrir": clip = abrir != null ? abrir : (abrir = Tono("abrir", 0.28f, 420f, 1200f)); break;
            case "cerrar": clip = cerrar != null ? cerrar : (cerrar = Tono("cerrar", 0.22f, 1100f, 380f)); break;
            default: clip = blip != null ? blip : (blip = Tono("blip", 0.06f, 1500f, 1500f)); break;
        }
        AudioSource.PlayClipAtPoint(clip, donde, volumen);
    }

    // Barrido de frecuencia con un armónico y envolvente suave: suena "electrónico" sin ser estridente
    private static AudioClip Tono(string nombre, float segundos, float desdeHz, float hastaHz)
    {
        const int sr = 44100;
        int n = Mathf.CeilToInt(segundos * sr);
        float[] datos = new float[n];
        float fase = 0f;
        for (int i = 0; i < n; i++)
        {
            float t = (float)i / n;
            float hz = Mathf.Lerp(desdeHz, hastaHz, t * t);
            fase += 2f * Mathf.PI * hz / sr;
            float env = Mathf.Sin(Mathf.PI * Mathf.Min(1f, t * 6f) / 2f) * (1f - t) * (1f - t);
            datos[i] = (Mathf.Sin(fase) * 0.8f + Mathf.Sin(fase * 2f) * 0.2f) * env * 0.6f;
        }
        AudioClip clip = AudioClip.Create(nombre, n, 1, sr, false);
        clip.SetData(datos, 0);
        return clip;
    }
}
