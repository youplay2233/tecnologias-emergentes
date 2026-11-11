using UnityEngine;

// Puente entre el control del Quest 3 y los scripts de clase:
// al apretar el gatillo derecho manda el mensaje (por ejemplo "Disparar") al objeto.
// Así ControlCañon queda idéntico al de la presentación y además funciona con el visor.
public class GatilloRM : MonoBehaviour
{
    public string mensaje = "Disparar";
    public bool gatilloDerecho = true;

    void Update()
    {
        bool presionado = gatilloDerecho ? EntradaRM.GatilloDerechoAbajo() : EntradaRM.GatilloIzquierdoAbajo();
        if (presionado)
        {
            SendMessage(mensaje, SendMessageOptions.DontRequireReceiver);
        }
    }
}
