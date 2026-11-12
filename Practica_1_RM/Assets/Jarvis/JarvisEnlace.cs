using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

// Enlace de red entre el visor y el cerebro de Jarvis (la laptop).
//
//   1. Descubrimiento: pregunta "JARVIS-RM?" por UDP (broadcast) y la laptop contesta con su puerto.
//      UDP sirve aquí porque no importa perder un paquete: se vuelve a preguntar cada segundo.
//   2. Órdenes: abre un WebSocket (sobre TCP) y lo mantiene abierto; las órdenes no se pueden perder
//      ni llegar desordenadas, por eso van por TCP.
//   3. Latencia: cada segundo manda un "ping" con su marca de tiempo y la laptop lo devuelve ("pong");
//      la diferencia es el tiempo de ida y vuelta de la red (RTT) y su variación es el jitter.
//
// Esta clase no usa nada de UnityEngine: corre en hilos de fondo y deja los mensajes en una cola
// que el script JarvisRM vacía en Update().
public sealed class JarvisEnlace : IDisposable
{
    public const string PrefijoBaliza = "JARVIS-RM";
    // Puerto del WebSocket de la laptop cuando nadie contesta el descubrimiento (por ejemplo, por cable con adb reverse)
    public const int PuertoWsPorDefecto = 8765;

    private readonly ConcurrentQueue<string> entrantes = new ConcurrentQueue<string>();
    private readonly ConcurrentQueue<string> salientes = new ConcurrentQueue<string>();
    private readonly SemaphoreSlim hayQueEnviar = new SemaphoreSlim(0);
    private readonly Stopwatch reloj = Stopwatch.StartNew();
    private readonly CancellationTokenSource cancelar = new CancellationTokenSource();

    private string direccionFija;
    private int puertoBaliza;
    private string saludo;
    private volatile string estado = "detenido";
    private volatile string servidor = "";
    private volatile string ultimoError = "";
    private double rttAnterior = -1;
    private int pingsEnviados;

    public string Estado { get { return estado; } }           // detenido, buscando, conectando, conectado, reintentando
    public string Servidor { get { return servidor; } }       // ws://ip:puerto en uso
    public string UltimoError { get { return ultimoError; } }
    public bool Conectado { get { return estado == "conectado"; } }

    // Mediciones de la red (milisegundos)
    public double RttMs { get; private set; }
    public double RttPromedioMs { get; private set; }
    public double RttMinimoMs { get; private set; }
    public double RttMaximoMs { get; private set; }
    public double JitterMs { get; private set; }
    public int Muestras { get; private set; }

    public void Iniciar(string direccion, int puertoDeBaliza, string saludoJson)
    {
        direccionFija = direccion == null ? "" : direccion.Trim();
        puertoBaliza = puertoDeBaliza;
        saludo = saludoJson;
        Task.Run(() => Bucle(cancelar.Token));
    }

    public bool Recibir(out string json)
    {
        return entrantes.TryDequeue(out json);
    }

    public void Enviar(string json)
    {
        salientes.Enqueue(json);
        hayQueEnviar.Release();
    }

    public void Dispose()
    {
        try { cancelar.Cancel(); } catch (Exception) { }
        estado = "detenido";
    }

    // ---------------------------------------------------------------------- bucle de conexión

    private async Task Bucle(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                string url = direccionFija;
                if (string.IsNullOrEmpty(url))
                {
                    estado = "buscando";
                    // Primero por la red. Si nadie contesta en 4 s se intenta por el cable USB: con
                    // `adb reverse tcp:8765 tcp:8765` el localhost del visor llega a la laptop (sirve aunque
                    // la laptop y el visor estén en subredes distintas o el firewall bloquee la red).
                    url = await Descubrir(puertoBaliza, ct, 4).ConfigureAwait(false)
                          ?? "ws://127.0.0.1:" + PuertoWsPorDefecto.ToString(CultureInfo.InvariantCulture);
                }

                estado = "conectando";
                servidor = url;
                using (ClientWebSocket ws = new ClientWebSocket())
                using (CancellationTokenSource limite = CancellationTokenSource.CreateLinkedTokenSource(ct))
                {
                    limite.CancelAfter(5000);
                    await ws.ConnectAsync(new Uri(url), limite.Token).ConfigureAwait(false);

                    estado = "conectado";
                    ultimoError = "";
                    string descartado;
                    while (salientes.TryDequeue(out descartado)) { }   // nada viejo de una conexión anterior
                    await EnviarTexto(ws, saludo, ct).ConfigureAwait(false);

                    Task recibir = BucleRecibir(ws, ct);
                    Task enviar = BucleEnviar(ws, ct);
                    await Task.WhenAny(recibir, enviar).ConfigureAwait(false);
                    try { ws.Abort(); } catch (Exception) { }
                }
            }
            catch (OperationCanceledException)
            {
                if (ct.IsCancellationRequested) break;
                ultimoError = "tiempo de espera agotado";
            }
            catch (Exception e)
            {
                ultimoError = e.Message;
            }

            if (ct.IsCancellationRequested) break;
            estado = "reintentando";
            try { await Task.Delay(2000, ct).ConfigureAwait(false); } catch (OperationCanceledException) { break; }
        }
        estado = "detenido";
    }

    // El visor pregunta "JARVIS-RM?" por broadcast y la laptop le contesta directo con el puerto del WebSocket.
    // Devuelve null si nadie contesta en `segundos`.
    private static async Task<string> Descubrir(int puerto, CancellationToken ct, int segundos)
    {
        using (UdpClient udp = new UdpClient(new IPEndPoint(IPAddress.Any, 0)))
        {
            udp.EnableBroadcast = true;
            byte[] pregunta = Encoding.UTF8.GetBytes(PrefijoBaliza + "?");

            List<IPEndPoint> destinos = new List<IPEndPoint>();
            destinos.Add(new IPEndPoint(IPAddress.Broadcast, puerto));     // 255.255.255.255
            destinos.Add(new IPEndPoint(IPAddress.Loopback, puerto));      // laptop y editor en la misma máquina
            IPAddress local = DifusionDeLaRedLocal();
            if (local != null) destinos.Add(new IPEndPoint(local, puerto)); // x.x.x.255 de la red Wi-Fi

            using (ct.Register(() => { try { udp.Close(); } catch (Exception) { } }))
            {
                Task<UdpReceiveResult> respuesta = udp.ReceiveAsync();
                for (int intento = 0; intento < segundos; intento++)
                {
                    foreach (IPEndPoint destino in destinos)
                    {
                        try { await udp.SendAsync(pregunta, pregunta.Length, destino).ConfigureAwait(false); }
                        catch (Exception) { /* sin ruta hacia ese destino: se intenta con los demás */ }
                    }

                    Task primero = await Task.WhenAny(respuesta, Task.Delay(1000)).ConfigureAwait(false);
                    ct.ThrowIfCancellationRequested();
                    if (primero != respuesta) continue;

                    UdpReceiveResult r = await respuesta.ConfigureAwait(false);
                    respuesta = udp.ReceiveAsync();
                    string texto = Encoding.UTF8.GetString(r.Buffer);
                    if (!texto.StartsWith(PrefijoBaliza + " ", StringComparison.Ordinal)) continue;
                    double puertoWs;
                    if (!ExtraerNumero(texto, "puerto", out puertoWs)) continue;
                    return "ws://" + r.RemoteEndPoint.Address + ":" + ((int)puertoWs).ToString(CultureInfo.InvariantCulture);
                }
                return null;
            }
        }
    }

    // Dirección de broadcast de la red local suponiendo máscara /24 (lo normal en una red de casa)
    private static IPAddress DifusionDeLaRedLocal()
    {
        try
        {
            using (Socket s = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp))
            {
                s.Connect("10.255.255.255", 1);   // no envía nada: solo elige la interfaz de salida
                byte[] b = ((IPEndPoint)s.LocalEndPoint).Address.GetAddressBytes();
                if (b[0] == 127) return null;
                b[3] = 255;
                return new IPAddress(b);
            }
        }
        catch (Exception)
        {
            return null;
        }
    }

    private async Task BucleRecibir(ClientWebSocket ws, CancellationToken ct)
    {
        byte[] bufer = new byte[8192];
        using (MemoryStream mensaje = new MemoryStream())
        {
            while (ws.State == WebSocketState.Open && !ct.IsCancellationRequested)
            {
                WebSocketReceiveResult r = await ws.ReceiveAsync(new ArraySegment<byte>(bufer), ct).ConfigureAwait(false);
                if (r.MessageType == WebSocketMessageType.Close) return;
                mensaje.Write(bufer, 0, r.Count);
                if (!r.EndOfMessage) continue;

                string json = Encoding.UTF8.GetString(mensaje.GetBuffer(), 0, (int)mensaje.Length);
                mensaje.SetLength(0);

                string tipo;
                if (ExtraerTexto(json, "tipo", out tipo) && tipo == "pong") AlPong(json);
                else entrantes.Enqueue(json);
            }
        }
    }

    private async Task BucleEnviar(ClientWebSocket ws, CancellationToken ct)
    {
        double proximoPing = reloj.Elapsed.TotalMilliseconds + 200;
        while (ws.State == WebSocketState.Open && !ct.IsCancellationRequested)
        {
            string json;
            while (salientes.TryDequeue(out json))
            {
                await EnviarTexto(ws, json, ct).ConfigureAwait(false);
            }

            double ahora = reloj.Elapsed.TotalMilliseconds;
            if (ahora >= proximoPing)
            {
                pingsEnviados++;
                string ping = "{\"tipo\":\"ping\",\"t\":" + ahora.ToString("0.###", CultureInfo.InvariantCulture) + ",\"n\":" + pingsEnviados.ToString(CultureInfo.InvariantCulture) + "}";
                await EnviarTexto(ws, ping, ct).ConfigureAwait(false);
                proximoPing = ahora + 1000;
            }

            int espera = (int)Math.Max(1, Math.Min(1000, proximoPing - reloj.Elapsed.TotalMilliseconds));
            await hayQueEnviar.WaitAsync(espera, ct).ConfigureAwait(false);
        }
    }

    private static Task EnviarTexto(ClientWebSocket ws, string json, CancellationToken ct)
    {
        byte[] datos = Encoding.UTF8.GetBytes(json);
        return ws.SendAsync(new ArraySegment<byte>(datos), WebSocketMessageType.Text, true, ct);
    }

    // ---------------------------------------------------------------------- latencia

    private void AlPong(string json)
    {
        double enviado;
        if (!ExtraerNumero(json, "t", out enviado)) return;
        double rtt = reloj.Elapsed.TotalMilliseconds - enviado;
        if (rtt < 0) return;

        Muestras++;
        RttMs = rtt;
        RttPromedioMs = Muestras == 1 ? rtt : RttPromedioMs + (rtt - RttPromedioMs) * 0.2;
        RttMinimoMs = Muestras == 1 ? rtt : Math.Min(RttMinimoMs, rtt);
        RttMaximoMs = Muestras == 1 ? rtt : Math.Max(RttMaximoMs, rtt);
        if (rttAnterior >= 0)
        {
            // Jitter como en RTP (RFC 3550): promedio suavizado de la variación entre muestras seguidas
            JitterMs += (Math.Abs(rtt - rttAnterior) - JitterMs) / 16.0;
        }
        rttAnterior = rtt;

        if (Muestras % 5 == 0)
        {
            Enviar("{\"tipo\":\"latencia\",\"rtt_ms\":" + RttPromedioMs.ToString("0.##", CultureInfo.InvariantCulture) +
                   ",\"jitter_ms\":" + JitterMs.ToString("0.##", CultureInfo.InvariantCulture) + "}");
        }
    }

    // ---------------------------------------------------------------------- lectura mínima de JSON

    // Busca "clave": <número> en un objeto JSON plano.
    public static bool ExtraerNumero(string json, string clave, out double valor)
    {
        valor = 0;
        int i = PosicionDelValor(json, clave);
        if (i < 0) return false;
        int j = i;
        while (j < json.Length && (char.IsDigit(json[j]) || json[j] == '-' || json[j] == '+' || json[j] == '.' || json[j] == 'e' || json[j] == 'E')) j++;
        return j > i && double.TryParse(json.Substring(i, j - i), NumberStyles.Float, CultureInfo.InvariantCulture, out valor);
    }

    // Busca "clave": "texto" en un objeto JSON plano (sin comillas escapadas en el valor).
    public static bool ExtraerTexto(string json, string clave, out string valor)
    {
        valor = "";
        int i = PosicionDelValor(json, clave);
        if (i < 0 || i >= json.Length || json[i] != '"') return false;
        int fin = json.IndexOf('"', i + 1);
        if (fin < 0) return false;
        valor = json.Substring(i + 1, fin - i - 1);
        return true;
    }

    private static int PosicionDelValor(string json, string clave)
    {
        string buscada = "\"" + clave + "\"";
        int i = json.IndexOf(buscada, StringComparison.Ordinal);
        if (i < 0) return -1;
        i += buscada.Length;
        while (i < json.Length && char.IsWhiteSpace(json[i])) i++;
        if (i >= json.Length || json[i] != ':') return -1;
        i++;
        while (i < json.Length && char.IsWhiteSpace(json[i])) i++;
        return i;
    }
}
