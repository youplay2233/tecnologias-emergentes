using System;
using UnityEngine;

// Botón holográfico: se ilumina cuando el rayo del control lo apunta y se pulsa con el gatillo
// (o con clic del ratón en el editor). Ver PunteroRM.
public class BotonHolo : MonoBehaviour
{
    public Action alPulsar;

    private Renderer fondo;
    private TextMesh etiqueta;
    private Color normal = new Color(0.08f, 0.35f, 0.45f, 0.45f);
    private Color encima = new Color(0.2f, 0.75f, 0.95f, 0.75f);
    private Color seleccionado = new Color(0.12f, 0.55f, 0.7f, 0.65f);
    private float hastaEncima;
    private float destello;

    public bool Seleccionado { get; set; }

    public string Texto
    {
        get { return etiqueta.text; }
        set { etiqueta.text = value; }
    }

    public static BotonHolo Crear(Transform padre, string nombre, string texto, Vector3 pos, Vector2 tam, float altoTexto, bool centrado = false)
    {
        GameObject go = new GameObject(nombre);
        go.transform.SetParent(padre, false);
        go.transform.localPosition = pos;
        BotonHolo b = go.AddComponent<BotonHolo>();
        b.fondo = Holo.Placa(go.transform, "Fondo", new Vector3(0f, 0f, 0.001f), tam, b.normal).GetComponent<Renderer>();
        BoxCollider c = go.AddComponent<BoxCollider>();
        c.size = new Vector3(tam.x, tam.y, 0.01f);
        Vector3 posTexto = centrado ? new Vector3(0f, 0f, -0.001f) : new Vector3(-tam.x / 2f + 0.012f, 0f, -0.001f);
        b.etiqueta = Holo.Texto(texto, go.transform, posTexto, altoTexto, Holo.Blanco, centrado ? TextAnchor.MiddleCenter : TextAnchor.MiddleLeft, "Texto");
        return b;
    }

    public void Encima()
    {
        if (Time.time > hastaEncima) Holo.Sonido("blip", transform.position, 0.12f);
        hastaEncima = Time.time + 0.1f;
    }

    public void Pulsar()
    {
        destello = 1f;
        Holo.Sonido("confirmar", transform.position);
        if (alPulsar != null) alPulsar();
    }

    void Update()
    {
        Color c = Time.time < hastaEncima ? encima : (Seleccionado ? seleccionado : normal);
        c = Color.Lerp(c, Color.white, destello);
        destello = Mathf.MoveTowards(destello, 0f, Time.deltaTime * 4f);
        fondo.material.color = Color.Lerp(fondo.material.color, c, 1f - Mathf.Exp(-18f * Time.deltaTime));
    }
}
