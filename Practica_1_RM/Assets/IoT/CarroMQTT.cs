using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using MQTTnet;
using MQTTnet.Client;
using TMPro;
using UnityEngine;

// Ejercicio 2 de MQTT: recibe por MQTT las órdenes de los botones de la ESP32 y se las pasa al carro.
// Mismo esquema que el MQTTBasico de la clase (cliente de MQTTnet, una cola para los mensajes y Update para
// usarlos), con un topic para cada cosa:
//   <topicBase>/mover        adelante, atras, izquierda, derecha, adelante_izquierda, adelante_derecha,
//                            atras_izquierda, atras_derecha, girar_izquierda, girar_derecha, alto
//   <topicBase>/luces        encender, apagar
//   <topicBase>/direccional  izquierda, derecha, apagar
//   <topicBase>/estado       lo publica este script cada vez que el carro cambia (se ve en MQTT Explorer)
public class CarroMQTT : MonoBehaviour
{
    public CarroOmnidireccional carro;
    public TMP_Text textoEstado;

    // test.mosquitto.org, como en el ejercicio 1, o la IP de la laptop con Mosquitto (diapositivas 4 y 5)
    public string servidor = "test.mosquitto.org";
    public int puerto = 1883;
    public string topicBase = "clase/te/quest3/carro";

    private IMqttClient cliente;
    private readonly ConcurrentQueue<string[]> mensajes = new ConcurrentQueue<string[]>();
    private volatile bool conectado;
    private bool conectando;
    private float siguienteIntento;
    private string ultimoMensaje = "todavía ninguno";
    private string estadoPublicado = "";
    private string textoMostrado = "";

    void Start()
    {
        Conectar();
    }

    private async void Conectar()
    {
        conectando = true;
        try
        {
            if (cliente == null)
            {
                cliente = new MqttFactory().CreateMqttClient();
                cliente.ApplicationMessageReceivedAsync += e =>
                {
                    mensajes.Enqueue(new[] { e.ApplicationMessage.Topic, e.ApplicationMessage.ConvertPayloadToString() });
                    return Task.CompletedTask;
                };
                cliente.DisconnectedAsync += e =>
                {
                    conectado = false;
                    return Task.CompletedTask;
                };
            }

            var opciones = new MqttClientOptionsBuilder()
                .WithTcpServer(servidor, puerto)
                .WithClientId("Unity_carro_" + Guid.NewGuid())
                .Build();
            await cliente.ConnectAsync(opciones);
            if (this == null) return;

            var suscripcion = new MqttClientSubscribeOptionsBuilder()
                .WithTopicFilter(topicBase + "/#")
                .Build();
            await cliente.SubscribeAsync(suscripcion);
            if (this == null) return;

            conectado = true;
            estadoPublicado = "";   // al conectarse vuelve a publicar el estado
            Debug.Log("MQTT conectado a " + servidor + " y suscrito a " + topicBase + "/#");
        }
        catch (Exception error)
        {
            if (this != null) Debug.LogError("Error MQTT: " + error.Message);
        }
        finally
        {
            conectando = false;
            siguienteIntento = Time.time + 5f;   // si se cae la conexión, vuelve a intentar cada 5 s
        }
    }

    public async void Publicar(string subtopic, string texto)
    {
        if (cliente == null || !cliente.IsConnected)
        {
            Debug.Log("Sin conexión MQTT: no se mandó " + subtopic + " = " + texto);
            return;
        }
        try
        {
            var envio = new MqttApplicationMessageBuilder()
                .WithTopic(topicBase + "/" + subtopic)
                .WithPayload(texto)
                .Build();
            await cliente.PublishAsync(envio);
        }
        catch (Exception error)
        {
            Debug.LogError("Error al publicar: " + error.Message);
        }
    }

    void Update()
    {
        if (!conectado && !conectando && Time.time >= siguienteIntento) Conectar();

        while (mensajes.TryDequeue(out string[] m))
        {
            string subtopic = m[0].StartsWith(topicBase + "/") ? m[0].Substring(topicBase.Length + 1) : m[0];
            string orden = m[1].Trim().ToLowerInvariant();
            bool entendida;
            switch (subtopic)
            {
                case "mover": entendida = carro.Mover(orden); break;
                case "luces": entendida = carro.Luces(orden); break;
                case "direccional": entendida = carro.Direccional(orden); break;
                default: continue;   // "estado" lo publica este mismo script
            }
            ultimoMensaje = subtopic + " = " + orden + (entendida ? "" : " (no la entiendo)");
            Debug.Log("MQTT " + ultimoMensaje);
        }

        string estado = "mover=" + carro.Movimiento + " | luces=" + (carro.LucesEncendidas ? "encendidas" : "apagadas")
            + " | direccional=" + carro.DireccionalActiva;
        if (conectado && estado != estadoPublicado)
        {
            estadoPublicado = estado;
            Publicar("estado", estado);
        }

        if (textoEstado == null) return;
        string texto = (conectado ? "<color=#9be15d>Conectado</color>" : "<color=#ff6f61>Sin conexión</color>")
            + " a " + servidor + ":" + puerto
            + "\nÚltimo mensaje: <color=#4cd7ff>" + ultimoMensaje + "</color>"
            + "\nLuces: " + (carro.LucesEncendidas ? "encendidas" : "apagadas") + "    Direccional: " + carro.DireccionalActiva;
        if (texto != textoMostrado)
        {
            textoMostrado = texto;
            textoEstado.text = texto;
        }
    }

    private void OnDestroy()
    {
        // Liberar la conexión al salir de la escena
        cliente?.Dispose();
    }
}
