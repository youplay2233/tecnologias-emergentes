using System;
using UnityEngine;
#if UNITY_ANDROID && !UNITY_EDITOR
using UnityEngine.Android;
#endif

// Micrófono y bocina del visor (ver jarvis/unity/audio_visor.py en la laptop):
//   - El micrófono del Quest se baja a 16 kHz y se manda en trozos de 80 ms (mensaje binario 0x01).
//     Así Jarvis te oye aunque estés lejos de la laptop.
//   - La voz de Jarvis llega como binario 0x02 (frecuencia + PCM) y suena desde el reactor, con sonido
//     espacial. Al terminar se avisa con {"tipo":"voz_fin"}.
// Mientras Jarvis habla no se manda el micrófono, para que no se escuche a sí mismo.
public class AudioVisor : MonoBehaviour
{
    private const byte Microfono = 1;
    private const byte Voz = 2;
    private const int Frecuencia = 16000;
    private const int MuestrasPorTrozo = 1280;   // 80 ms a 16 kHz, lo mismo que espera la laptop

    public Func<JarvisEnlace> enlace;   // JarvisHUD puede reemplazar el enlace al reconectar
    public Transform origenDeLaVoz;     // el reactor

    private AudioSource fuente;
    private string dispositivo;
    private AudioClip grabacion;
    private int frecuenciaMic;
    private int ultimaPosicion;
    private float[] lectura = new float[0];
    private readonly System.Collections.Generic.List<short> pendiente = new System.Collections.Generic.List<short>();
    private float fraccion;   // posición fraccionaria al remuestrear
    private bool hablando;

    public bool Hablando { get { return hablando; } }
    public bool Escuchando { get { return grabacion != null; } }

    void Start()
    {
        fuente = (origenDeLaVoz != null ? origenDeLaVoz.gameObject : gameObject).AddComponent<AudioSource>();
        fuente.playOnAwake = false;
        fuente.spatialBlend = 0.6f;   // se oye desde el reactor, pero sin perderse si volteas
        fuente.minDistance = 0.6f;
        fuente.volume = 1f;
        if (EntradaRM.XRActivo) PedirMicrofono();
    }

    private void PedirMicrofono()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        if (!Permission.HasUserAuthorizedPermission(Permission.Microphone))
        {
            PermissionCallbacks respuesta = new PermissionCallbacks();
            respuesta.PermissionGranted += _ => IniciarMicrofono();
            Permission.RequestUserPermission(Permission.Microphone, respuesta);
            return;
        }
#endif
        IniciarMicrofono();
    }

    private void IniciarMicrofono()
    {
        if (Microphone.devices.Length == 0)
        {
            Debug.LogWarning("[Jarvis] El visor no reporta micrófono.");
            return;
        }
        dispositivo = Microphone.devices[0];
        int minimo, maximo;
        Microphone.GetDeviceCaps(dispositivo, out minimo, out maximo);
        frecuenciaMic = (minimo == 0 && maximo == 0) ? Frecuencia : Mathf.Clamp(Frecuencia, minimo, maximo);
        grabacion = Microphone.Start(dispositivo, true, 2, frecuenciaMic);
        ultimaPosicion = 0;
        Debug.Log("[Jarvis] Micrófono del visor: " + dispositivo + " a " + frecuenciaMic + " Hz.");
    }

    void Update()
    {
        if (hablando && !fuente.isPlaying)
        {
            hablando = false;
            JarvisEnlace e = enlace != null ? enlace() : null;
            if (e != null) e.Enviar("{\"tipo\":\"voz_fin\"}");
        }
        MandarMicrofono();
    }

    // ------------------------------------------------------------------ micrófono -> laptop

    private void MandarMicrofono()
    {
        if (grabacion == null) return;
        int posicion = Microphone.GetPosition(dispositivo);
        int total = grabacion.samples;
        int nuevas = (posicion - ultimaPosicion + total) % total;
        if (nuevas == 0) return;
        if (lectura.Length < nuevas) lectura = new float[nuevas];

        // Lee lo nuevo, contemplando que el búfer circular haya dado la vuelta
        if (ultimaPosicion + nuevas <= total)
        {
            grabacion.GetData(lectura, ultimaPosicion);
        }
        else
        {
            float[] cola = new float[total - ultimaPosicion];
            float[] cabeza = new float[nuevas - cola.Length];
            grabacion.GetData(cola, ultimaPosicion);
            grabacion.GetData(cabeza, 0);
            Array.Copy(cola, lectura, cola.Length);
            Array.Copy(cabeza, 0, lectura, cola.Length, cabeza.Length);
        }
        ultimaPosicion = posicion;

        JarvisEnlace e = enlace != null ? enlace() : null;
        if (e == null || !e.Conectado || hablando)
        {
            pendiente.Clear();   // lo que se oye mientras Jarvis habla es su propia voz
            return;
        }

        if (frecuenciaMic == Frecuencia)
        {
            for (int k = 0; k < nuevas; k++) pendiente.Add((short)Mathf.Clamp(lectura[k] * 32767f, -32768f, 32767f));
        }
        else
        {
            Remuestrear(nuevas);
        }
        Mandar(e);
    }

    // Remuestreo lineal a 16 kHz (cuando el micrófono no la da directo)
    private void Remuestrear(int nuevas)
    {
        float paso = (float)frecuenciaMic / Frecuencia;
        while (fraccion < nuevas - 1)
        {
            int i = (int)fraccion;
            float t = fraccion - i;
            float v = Mathf.Lerp(lectura[i], lectura[i + 1], t);
            pendiente.Add((short)Mathf.Clamp(v * 32767f, -32768f, 32767f));
            fraccion += paso;
        }
        fraccion -= nuevas - 1;
    }

    private void Mandar(JarvisEnlace e)
    {
        while (pendiente.Count >= MuestrasPorTrozo)
        {
            byte[] trozo = new byte[1 + MuestrasPorTrozo * 2];
            trozo[0] = Microfono;
            for (int k = 0; k < MuestrasPorTrozo; k++)
            {
                short m = pendiente[k];
                trozo[1 + 2 * k] = (byte)(m & 0xff);
                trozo[2 + 2 * k] = (byte)((m >> 8) & 0xff);
            }
            pendiente.RemoveRange(0, MuestrasPorTrozo);
            e.EnviarBinario(trozo);
        }
    }

    // ------------------------------------------------------------------ voz de Jarvis <- laptop

    public void Recibir(byte[] datos)
    {
        if (datos == null || datos.Length < 5 || datos[0] != Voz) return;
        int frecuencia = (int)BitConverter.ToUInt32(datos, 1);
        int n = (datos.Length - 5) / 2;
        if (n <= 0 || frecuencia <= 0) return;
        float[] muestras = new float[n];
        for (int k = 0; k < n; k++)
        {
            short m = (short)(datos[5 + 2 * k] | (datos[6 + 2 * k] << 8));
            muestras[k] = m / 32768f;
        }
        AudioClip clip = AudioClip.Create("Voz_Jarvis", n, 1, frecuencia, false);
        clip.SetData(muestras, 0);
        fuente.Stop();
        fuente.clip = clip;
        fuente.Play();
        hablando = true;
    }
}
