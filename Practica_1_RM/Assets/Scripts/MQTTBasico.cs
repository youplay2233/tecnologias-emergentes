using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using MQTTnet;
using MQTTnet.Client;
using TMPro;
using UnityEngine;

public class MQTTBasico : MonoBehaviour
{
    // Texto de la escena donde veremos los mensajes.
    public TMP_Text textoRecibido;

    // Todos los participantes deben usar el mismo topic.
    public string topic = "clase/te/quest3/texto";

    // Mensaje que enviará el botón.
    public string mensaje = "Hola desde Unity";

    private IMqttClient cliente;

    // Guarda los mensajes para mostrarlos desde Update.
    private readonly ConcurrentQueue<string> mensajes =
        new ConcurrentQueue<string>();

    private async void Start()
    {
        try
        {
            // 1. Crear el cliente MQTT.
            cliente = new MqttFactory().CreateMqttClient();

            // 2. Definir qué hacer cuando llegue un mensaje.
            cliente.ApplicationMessageReceivedAsync += e =>
            {
                mensajes.Enqueue(
                    e.ApplicationMessage.ConvertPayloadToString());

                return Task.CompletedTask;
            };

            // 3. Configurar el servidor y un identificador único.
            var opciones = new MqttClientOptionsBuilder()
                .WithTcpServer("test.mosquitto.org", 1883)
                .WithClientId("Unity_" + Guid.NewGuid())
                .Build();

            // 4. Conectarse al servidor.
            await cliente.ConnectAsync(opciones);

            if (this == null) return;

            // 5. Suscribirse para recibir mensajes del topic.
            var suscripcion = new MqttClientSubscribeOptionsBuilder()
                .WithTopicFilter(topic)
                .Build();

            await cliente.SubscribeAsync(suscripcion);

            if (this != null)
                Debug.Log("MQTT conectado y suscrito");
        }
        catch (Exception error)
        {
            Debug.LogError("Error MQTT: " + error.Message);
        }
    }

    public async void Publicar()
    {
        // Solo publicar si existe una conexión.
        if (cliente == null || !cliente.IsConnected) return;

        try
        {
            // 6. Preparar y publicar el mensaje.
            var envio = new MqttApplicationMessageBuilder()
                .WithTopic(topic)
                .WithPayload(mensaje)
                .Build();

            await cliente.PublishAsync(envio);
        }
        catch (Exception error)
        {
            Debug.LogError("Error al publicar: " + error.Message);
        }
    }

    private void Update()
    {
        // 7. Mostrar el mensaje recibido en Unity.
        if (mensajes.TryDequeue(out string recibido))
        {
            if (textoRecibido != null)
                textoRecibido.text = recibido;
        }
    }

    private void OnDestroy()
    {
        // Liberar la conexión al salir de la escena.
        cliente?.Dispose();
    }
}
