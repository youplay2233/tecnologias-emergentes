using UnityEngine;

// Control virtual con los mismos botones que la ESP32 (esp32/carro_mqtt): publica por MQTT exactamente lo que
// publicaría la placa, para probar el carro sin ella. En el visor los botones se pulsan tocándolos; cada uno llama
// a Pulsar("<botón>") desde su On Click(). El carro no sabe quién mandó la orden: solo ve lo que llega del broker.
public class ControlVirtualESP32 : MonoBehaviour
{
    public CarroMQTT mqtt;

    private bool luces;
    private string direccional = "apagar";

    public void Pulsar(string boton)
    {
        if (mqtt == null) return;
        Debug.Log("Botón virtual: " + boton);
        switch (boton)
        {
            case "luces":   // cada pulsación cambia, como el botón de luces de la ESP32
                luces = !luces;
                mqtt.Publicar("luces", luces ? "encender" : "apagar");
                break;
            case "direccional_izquierda":
            case "direccional_derecha":
                string lado = boton.Substring("direccional_".Length);
                direccional = direccional == lado ? "apagar" : lado;
                mqtt.Publicar("direccional", direccional);
                break;
            default:        // adelante, atras, izquierda, derecha, las diagonales, girar_izquierda, girar_derecha y alto
                mqtt.Publicar("mover", boton);
                break;
        }
    }
}
