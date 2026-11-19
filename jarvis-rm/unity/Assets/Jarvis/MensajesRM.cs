using System;

// Mensajes JSON del enlace con la laptop (ver jarvis/unity/servidor.py en ~/jarvis).
// JsonUtility solo llena los campos que vienen en el mensaje; los demás quedan en su valor por defecto.
[Serializable]
public class MensajeJarvis
{
    public string tipo;
    public int id;            // > 0: la laptop espera un acuse (ack)
    public string texto;
    public string titulo;
    public string valor;      // texto: estado ("escuchando", "pensando"...)
    public string accion;     // tipo "modelo": cargar, despiezar, girar, aislar, ocultar, mostrar, resaltar, mostrar_todo, escala, cerrar
    public string clave;
    public string url;
    public float numero;
    public float numero2;
    public bool activa;
    public string[] piezas;
    public ItemModelo[] modelos;
    public ItemDocumento[] documentos;

    // Objetos con física (crear, lanzar)
    public string forma;
    public int cantidad;
    public string color;
    public float tamano;
    public float fuerza;

    // Métricas del ciclo de voz
    public int escucha_ms;
    public int cerebro_ms;
    public int voz_ms;
}

[Serializable]
public class ItemModelo
{
    public string clave;
    public string nombre;
    public string descripcion;
    public int piezas;
}

[Serializable]
public class ItemDocumento
{
    public string nombre;
    public string carpeta;
    public float mb;
}

[Serializable]
public class AcuseJarvis
{
    public string tipo = "ack";
    public int id;
    public bool ok = true;
    public string detalle;
    public int objetos;
}

[Serializable]
public class SaludoJarvis
{
    public string tipo = "hola";
    public string dispositivo;
    public string escena;
    public string token;
}

[Serializable]
public class EventoJarvis
{
    public string tipo = "evento";
    public string nombre;
    public string detalle;
}
