using UnityEngine;

// Práctica MQTT, ejercicio 1: al tocar la figura cambia de color y manda por MQTT qué se tocó y de qué color quedó.
// Usa el MQTTBasico de la presentación: escribe su campo "mensaje" y llama a Publicar(), lo mismo que hace el botón.
// En el visor se toca con la mano o apuntando con el control y apretando el gatillo (TocadorRM).
public class FiguraTocable : MonoBehaviour, ITocableRM
{
    public string nombre = "Figura";
    public MQTTBasico mqtt;
    public Renderer[] piezas;   // las piezas que cambian de color (si está vacío, todas)
    public int primerColor;     // con qué color del ciclo empieza

    public static readonly string[] NombresColor = { "rojo", "verde", "azul", "amarillo", "morado", "naranja" };
    public static readonly Color[] Colores =
    {
        new Color(0.86f, 0.22f, 0.20f), new Color(0.25f, 0.70f, 0.35f), new Color(0.20f, 0.50f, 0.90f),
        new Color(0.96f, 0.80f, 0.20f), new Color(0.55f, 0.35f, 0.85f), new Color(0.95f, 0.55f, 0.15f)
    };

    private int indice = -1;
    private Vector3 escalaBase;
    private float pulso;

    void Awake()
    {
        if (piezas == null || piezas.Length == 0) piezas = GetComponentsInChildren<Renderer>();
        escalaBase = transform.localScale;
    }

    public void Tocar()
    {
        indice = indice < 0 ? primerColor % Colores.Length : (indice + 1) % Colores.Length;
        foreach (Renderer r in piezas) r.material.color = Colores[indice];
        pulso = 1f;

        string texto = "Tocaron: " + nombre + " | Color: " + NombresColor[indice];
        Debug.Log(texto);
        if (mqtt != null)
        {
            string delBoton = mqtt.mensaje;
            mqtt.mensaje = texto;
            mqtt.Publicar();            // lee "mensaje" antes de su primer await
            mqtt.mensaje = delBoton;    // el botón sigue mandando su propio mensaje ("Hola desde Unity")
        }
    }

    void Update()
    {
        // Un pequeño latido para que se note el toque
        if (pulso <= 0f) return;
        pulso = Mathf.Max(0f, pulso - Time.deltaTime * 4f);
        transform.localScale = escalaBase * (1f + 0.15f * pulso);
    }
}
