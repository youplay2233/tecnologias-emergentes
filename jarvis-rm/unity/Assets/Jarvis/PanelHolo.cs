using UnityEngine;

// Panel holográfico donde aparece lo que Jarvis responde, escrito letra por letra con una línea de barrido.
public class PanelHolo : MonoBehaviour
{
    public float ancho = 0.62f;
    public float alto = 0.44f;   // ~9 renglones: respuestas de Jarvis de hasta unas 50 palabras
    public int caracteresPorLinea = 34;
    public float letrasPorSegundo = 90f;

    private TextMesh titulo, cuerpo;
    private LineRenderer barrido;
    private string objetivo = "";
    private float inicio;

    void Awake()
    {
        Holo.Placa(transform, "Fondo", new Vector3(0f, 0f, 0.003f), new Vector2(ancho, alto), new Color(0.02f, 0.1f, 0.14f, 0.55f));
        LineRenderer borde = Holo.Linea(transform, "Borde", Holo.CianTenue, 0.0015f, false, 4);
        Holo.Rectangulo(borde, ancho, alto);
        Holo.Esquinas(transform, ancho + 0.02f, alto + 0.02f, 0.045f, Holo.Cian, 0.004f);

        LineRenderer separador = Holo.Linea(transform, "Separador", Holo.CianTenue, 0.0012f, false, 2);
        separador.SetPosition(0, new Vector3(-ancho / 2f + 0.03f, alto / 2f - 0.065f, 0f));
        separador.SetPosition(1, new Vector3(ancho / 2f - 0.03f, alto / 2f - 0.065f, 0f));

        titulo = Holo.Texto("J.A.R.V.I.S.", transform, new Vector3(-ancho / 2f + 0.03f, alto / 2f - 0.035f, 0f), 0.038f, Holo.Cian, TextAnchor.MiddleLeft, "Titulo");
        cuerpo = Holo.Texto("", transform, new Vector3(-ancho / 2f + 0.03f, alto / 2f - 0.085f, 0f), 0.03f, Holo.Blanco, TextAnchor.UpperLeft, "Cuerpo");
        barrido = Holo.Linea(transform, "Barrido", new Color(0.35f, 0.92f, 1f, 0.22f), 0.002f, false, 2);
        Mostrar("J.A.R.V.I.S.", "Di «Oye Jarvis».");
    }

    public void Mostrar(string nuevoTitulo, string texto)
    {
        titulo.text = string.IsNullOrEmpty(nuevoTitulo) ? "J.A.R.V.I.S." : nuevoTitulo.ToUpperInvariant();
        objetivo = Holo.Envolver(texto, caracteresPorLinea);
        inicio = Time.time;
    }

    void Update()
    {
        int n = Mathf.Min(objetivo.Length, Mathf.FloorToInt((Time.time - inicio) * letrasPorSegundo));
        string visible = n < objetivo.Length ? objetivo.Substring(0, n) + "_" : objetivo;
        if (cuerpo.text != visible) cuerpo.text = visible;

        float y = alto / 2f - Mathf.Repeat(Time.time * 0.18f, 1f) * alto;
        barrido.SetPosition(0, new Vector3(-ancho / 2f, y, -0.001f));
        barrido.SetPosition(1, new Vector3(ancho / 2f, y, -0.001f));
    }
}
