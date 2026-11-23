using System.Collections.Generic;
using UnityEngine;

// Consola flotante: dentro del visor no existe la ventana Console de Unity,
// así que los Debug.Log (por ejemplo "Colisión con: ...") se muestran en un panel de texto.
public class ConsolaRM : MonoBehaviour
{
    public TextMesh texto;
    public int maximoDeLineas = 9;
    public string titulo = "Consola";
    public bool mostrarErrores;   // también los Debug.LogError (por ejemplo, "Error MQTT: ...")

    private readonly Queue<string> lineas = new Queue<string>();
    private int contador;

    void OnEnable()
    {
        Application.logMessageReceived += Recibir;
        Pintar();
    }

    void OnDisable()
    {
        Application.logMessageReceived -= Recibir;
    }

    private void Recibir(string mensaje, string pila, LogType tipo)
    {
        bool error = tipo == LogType.Error || tipo == LogType.Exception;
        if (tipo != LogType.Log && !(mostrarErrores && error)) return;
        contador++;
        lineas.Enqueue(contador + ". " + (error ? "ERROR: " : "") + mensaje);
        while (lineas.Count > maximoDeLineas) lineas.Dequeue();
        Pintar();
    }

    private void Pintar()
    {
        if (texto == null) return;
        texto.text = titulo + "\n" + string.Join("\n", lineas.ToArray());
    }
}
