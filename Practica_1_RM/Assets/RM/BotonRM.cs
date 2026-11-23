using UnityEngine;
using UnityEngine.UI;

// Hace que un botón de UI (en un Canvas en el espacio del mundo) se pueda pulsar en el visor: le pone un Collider del
// tamaño del botón para que TocadorRM lo encuentre, y Pulsar() hace lo mismo que un clic con el mouse: se ve presionado
// y ejecuta su On Click() (lo que se configuró en el Inspector).
[RequireComponent(typeof(Button))]
public class BotonRM : MonoBehaviour, ITocableRM
{
    private Button boton;

    void Awake()
    {
        boton = GetComponent<Button>();
        RectTransform rt = (RectTransform)transform;
        BoxCollider caja = GetComponent<BoxCollider>();
        if (caja == null) caja = gameObject.AddComponent<BoxCollider>();
        caja.size = new Vector3(rt.rect.width, rt.rect.height, 30f);   // en unidades del Canvas
        caja.isTrigger = true;
    }

    public void Pulsar()
    {
        if (boton == null || !boton.IsActive() || !boton.interactable) return;
        boton.OnSubmit(null);   // efecto de presionado + On Click()
    }

    public void Tocar()
    {
        Pulsar();
    }
}
