using System;
using UnityEngine;

// Tableta holográfica con dos pestañas: los modelos que se pueden abrir y los documentos que Jarvis
// puede mandar por correo. Al pulsar un renglón no se actúa aquí: se le pide a Jarvis con un texto
// (como si lo hubieras dicho), así el cerebro decide y, para los correos, pide confirmación.
public class TabletaJarvis : MonoBehaviour
{
    public Action<string> alPedir;

    private const float Ancho = 0.32f;
    private const float Alto = 0.22f;
    private const int Filas = 5;

    private ItemModelo[] modelos = new ItemModelo[0];
    private ItemDocumento[] documentos = new ItemDocumento[0];
    private int pestana;   // 0 = modelos, 1 = documentos
    private int pagina;

    private BotonHolo tabModelos, tabDocumentos, anterior, siguiente;
    private readonly BotonHolo[] filas = new BotonHolo[Filas];
    private TextMesh pie;

    void Awake()
    {
        // Para tomarla con la mano (botón de agarre) y dejarla donde quieras
        Rigidbody cuerpo = gameObject.AddComponent<Rigidbody>();
        cuerpo.isKinematic = true;
        cuerpo.useGravity = false;
        BoxCollider agarre = gameObject.AddComponent<BoxCollider>();
        agarre.center = new Vector3(0f, 0f, 0.012f);   // detrás de los botones, para no tapar el rayo
        agarre.size = new Vector3(Ancho, Alto, 0.01f);

        Holo.Placa(transform, "Fondo", new Vector3(0f, 0f, 0.004f), new Vector2(Ancho, Alto), new Color(0.01f, 0.06f, 0.09f, 0.7f));
        LineRenderer borde = Holo.Linea(transform, "Borde", Holo.CianTenue, 0.0015f, false, 4);
        Holo.Rectangulo(borde, Ancho, Alto);
        Holo.Esquinas(transform, Ancho + 0.012f, Alto + 0.012f, 0.03f, Holo.Cian, 0.003f);
        Holo.Texto("JARVIS  ·  ARCHIVO", transform, new Vector3(-Ancho / 2f + 0.015f, Alto / 2f - 0.016f, 0f), 0.017f, Holo.Cian, TextAnchor.MiddleLeft, "Encabezado");

        float yTabs = Alto / 2f - 0.042f;
        tabModelos = BotonHolo.Crear(transform, "Tab_modelos", "MODELOS", new Vector3(-0.072f, yTabs, 0f), new Vector2(0.14f, 0.024f), 0.016f, true);
        tabDocumentos = BotonHolo.Crear(transform, "Tab_documentos", "DOCUMENTOS", new Vector3(0.078f, yTabs, 0f), new Vector2(0.14f, 0.024f), 0.016f, true);
        tabModelos.alPulsar = () => Pestana(0);
        tabDocumentos.alPulsar = () => Pestana(1);

        for (int i = 0; i < Filas; i++)
        {
            int indice = i;
            filas[i] = BotonHolo.Crear(transform, "Fila_" + i, "", new Vector3(0f, yTabs - 0.034f - i * 0.026f, 0f), new Vector2(Ancho - 0.03f, 0.022f), 0.0145f);
            filas[i].alPulsar = () => Elegir(indice);
        }

        float yPie = -Alto / 2f + 0.016f;
        anterior = BotonHolo.Crear(transform, "Anterior", "<", new Vector3(-Ancho / 2f + 0.03f, yPie, 0f), new Vector2(0.035f, 0.02f), 0.016f, true);
        siguiente = BotonHolo.Crear(transform, "Siguiente", ">", new Vector3(Ancho / 2f - 0.03f, yPie, 0f), new Vector2(0.035f, 0.02f), 0.016f, true);
        anterior.alPulsar = () => Pagina(-1);
        siguiente.alPulsar = () => Pagina(1);
        pie = Holo.Texto("", transform, new Vector3(0f, yPie, 0f), 0.013f, Holo.CianTenue, TextAnchor.MiddleCenter, "Pie");
        Refrescar();
    }

    public void Catalogo(ItemModelo[] nuevosModelos, ItemDocumento[] nuevosDocumentos)
    {
        if (nuevosModelos != null) modelos = nuevosModelos;
        if (nuevosDocumentos != null) documentos = nuevosDocumentos;
        pagina = 0;
        Refrescar();
    }

    public void Mostrar(bool visible)
    {
        if (gameObject.activeSelf == visible) return;
        gameObject.SetActive(visible);
        Holo.Sonido(visible ? "abrir" : "cerrar", transform.position);
        if (!visible) return;

        // Aparece frente a la cabeza, un poco abajo y a la izquierda, inclinada como una tableta en la mano
        Camera cam = Camera.main;
        if (cam == null) return;
        Vector3 frente = Vector3.ProjectOnPlane(cam.transform.forward, Vector3.up).normalized;
        if (frente.sqrMagnitude < 0.01f) frente = Vector3.forward;
        Vector3 izquierda = Vector3.Cross(frente, Vector3.up);
        transform.position = cam.transform.position + frente * 0.42f + Vector3.down * 0.22f + izquierda * 0.1f;
        transform.rotation = Quaternion.LookRotation(transform.position - cam.transform.position);
    }

    private int Total { get { return pestana == 0 ? modelos.Length : documentos.Length; } }

    private void Pestana(int cual)
    {
        pestana = cual;
        pagina = 0;
        Refrescar();
    }

    private void Pagina(int delta)
    {
        int paginas = Mathf.Max(1, Mathf.CeilToInt(Total / (float)Filas));
        pagina = (pagina + delta + paginas) % paginas;
        Refrescar();
    }

    private void Elegir(int fila)
    {
        int i = pagina * Filas + fila;
        if (alPedir == null || i >= Total) return;
        if (pestana == 0) alPedir("Abre el modelo " + modelos[i].nombre + ".");
        else alPedir("Mándame por correo el documento «" + documentos[i].nombre + "».");
    }

    private void Refrescar()
    {
        if (tabModelos == null) return;
        tabModelos.Seleccionado = pestana == 0;
        tabDocumentos.Seleccionado = pestana == 1;
        for (int f = 0; f < Filas; f++)
        {
            int i = pagina * Filas + f;
            bool hay = i < Total;
            filas[f].gameObject.SetActive(hay);
            if (!hay) continue;
            if (pestana == 0)
            {
                ItemModelo m = modelos[i];
                filas[f].Texto = Recortar(m.nombre, 24) + "  <color=#7fdfff>" + m.piezas + " piezas</color>";
            }
            else
            {
                ItemDocumento d = documentos[i];
                filas[f].Texto = Recortar(d.nombre, 30) + "  <color=#7fdfff>" + d.mb.ToString("0.0") + " MB</color>";
            }
        }
        int paginas = Mathf.Max(1, Mathf.CeilToInt(Total / (float)Filas));
        pie.text = Total == 0 ? (pestana == 0 ? "Sin modelos: uv run jarvis-modelos" : "Sin documentos") : "página " + (pagina + 1) + " de " + paginas;
        anterior.gameObject.SetActive(paginas > 1);
        siguiente.gameObject.SetActive(paginas > 1);
    }

    private static string Recortar(string t, int n)
    {
        if (string.IsNullOrEmpty(t)) return "";
        return t.Length <= n ? t : t.Substring(0, n - 1) + "…";
    }
}
