using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

// Objetos con física que Jarvis crea o lanza (lo mismo que en la práctica Jarvis_RM).
// Se apoyan en el piso invisible de la escena; lo que se sale se borra.
public class ObjetosFisicos : MonoBehaviour
{
    private static readonly string[] Colores = { "rojo", "naranja", "amarillo", "verde", "azul", "morado", "blanco", "gris" };
    private static readonly Color[] Rgb =
    {
        new Color(0.9f, 0.2f, 0.18f), new Color(1f, 0.55f, 0.1f), new Color(1f, 0.85f, 0.15f), new Color(0.25f, 0.8f, 0.35f),
        new Color(0.2f, 0.5f, 1f), new Color(0.6f, 0.3f, 0.9f), new Color(0.95f, 0.95f, 0.95f), new Color(0.5f, 0.52f, 0.55f)
    };
    private const int Maximo = 60;
    private const float AlturaMinima = -3f;
    private const float DistanciaMaxima = 150f;

    private readonly List<GameObject> creados = new List<GameObject>();
    private readonly Dictionary<string, Material> materiales = new Dictionary<string, Material>();

    public int Cantidad { get { return creados.Count; } }

    public string Crear(MensajeJarvis m)
    {
        int cantidad = Mathf.Clamp(m.cantidad, 1, 20);
        float tamano = Mathf.Clamp(m.tamano <= 0f ? 0.2f : m.tamano, 0.05f, 0.6f);
        Vector3 centro, frente;
        Frente(out centro, out frente);
        Vector3 lado = Vector3.Cross(Vector3.up, frente);
        for (int i = 0; i < cantidad; i++)
        {
            float columna = (i % 5) - Mathf.Min(cantidad - 1, 4) / 2f;
            Vector3 pos = centro + frente * (1.3f + (i / 5) * (tamano + 0.1f)) + lado * columna * (tamano + 0.08f) + Vector3.up * (0.25f + 0.12f * i);
            Nuevo(m.forma, m.color, tamano, pos);
        }
        return cantidad + " " + Nombre(m.forma) + (cantidad > 1 ? "s" : "") + " de color " + ColorValido(m.color) + " frente al usuario.";
    }

    public string Lanzar(MensajeJarvis m)
    {
        float tamano = Mathf.Clamp(m.tamano <= 0f ? 0.15f : m.tamano, 0.05f, 0.6f);
        float velocidad = Mathf.Clamp(m.fuerza <= 0f ? 6f : m.fuerza, 1f, 20f);
        Vector3 centro, frente;
        Frente(out centro, out frente);
        GameObject go = Nuevo(m.forma, m.color, tamano, centro + frente * 0.6f + Vector3.down * 0.15f);
        go.GetComponent<Rigidbody>().AddForce((frente + Vector3.up * 0.25f).normalized * velocidad, ForceMode.VelocityChange);
        return Nombre(m.forma) + " lanzado a " + velocidad.ToString("0.#", CultureInfo.InvariantCulture) + " metros por segundo.";
    }

    public string Gravedad(bool activa)
    {
        Physics.gravity = activa ? new Vector3(0f, -9.81f, 0f) : Vector3.zero;
        foreach (GameObject go in creados)
        {
            if (go == null) continue;
            Rigidbody rb = go.GetComponent<Rigidbody>();
            rb.WakeUp();
            if (!activa) rb.AddForce(UnityEngine.Random.insideUnitSphere * 0.3f + Vector3.up * 0.4f, ForceMode.VelocityChange);
        }
        return activa ? "Gravedad encendida." : "Gravedad apagada: los objetos flotan.";
    }

    public string Limpiar()
    {
        int n = 0;
        foreach (GameObject go in creados)
        {
            if (go == null) continue;
            Destroy(go);
            n++;
        }
        creados.Clear();
        return n + " objetos borrados.";
    }

    void Update()
    {
        for (int i = creados.Count - 1; i >= 0; i--)
        {
            GameObject go = creados[i];
            if (go == null) { creados.RemoveAt(i); continue; }
            Vector3 p = go.transform.position;
            if (p.y < AlturaMinima || new Vector2(p.x, p.z).magnitude > DistanciaMaxima)
            {
                Destroy(go);
                creados.RemoveAt(i);
            }
        }
    }

    void OnDestroy()
    {
        Physics.gravity = new Vector3(0f, -9.81f, 0f);
    }

    private GameObject Nuevo(string forma, string color, float tamano, Vector3 pos)
    {
        PrimitiveType tipo;
        switch (forma)
        {
            case "esfera": tipo = PrimitiveType.Sphere; break;
            case "capsula": tipo = PrimitiveType.Capsule; break;
            case "cilindro": tipo = PrimitiveType.Cylinder; break;
            default: tipo = PrimitiveType.Cube; break;
        }
        GameObject go = GameObject.CreatePrimitive(tipo);
        go.name = "Jarvis_" + Nombre(forma) + "_" + (creados.Count + 1);
        go.transform.position = pos;
        go.transform.rotation = UnityEngine.Random.rotation;
        go.transform.localScale = (tipo == PrimitiveType.Capsule || tipo == PrimitiveType.Cylinder)
            ? new Vector3(tamano, tamano * 0.5f, tamano) : Vector3.one * tamano;
        go.GetComponent<Renderer>().sharedMaterial = MaterialDe(ColorValido(color));
        Rigidbody rb = go.AddComponent<Rigidbody>();
        if (tipo != PrimitiveType.Cube) rb.angularDrag = 1.5f;   // PhysX no frena lo que rueda
        creados.Add(go);
        while (creados.Count > Maximo)
        {
            if (creados[0] != null) Destroy(creados[0]);
            creados.RemoveAt(0);
        }
        return go;
    }

    private Material MaterialDe(string color)
    {
        Material m;
        if (!materiales.TryGetValue(color, out m))
        {
            m = MaterialesRobot.Nuevo(Rgb[Array.IndexOf(Colores, color)], 0.1f, 0.5f);
            materiales[color] = m;
        }
        return m;
    }

    private static void Frente(out Vector3 centro, out Vector3 frente)
    {
        Camera cam = Camera.main;
        if (cam != null)
        {
            centro = cam.transform.position;
            frente = Vector3.ProjectOnPlane(cam.transform.forward, Vector3.up).normalized;
            if (frente.sqrMagnitude < 0.01f) frente = Vector3.forward;
        }
        else
        {
            centro = new Vector3(0f, 1.5f, 0f);
            frente = Vector3.forward;
        }
    }

    private static string ColorValido(string color)
    {
        return Array.IndexOf(Colores, color) >= 0 ? color : "azul";
    }

    private static string Nombre(string forma)
    {
        switch (forma)
        {
            case "esfera": return "esfera";
            case "capsula": return "cápsula";
            case "cilindro": return "cilindro";
            default: return "cubo";
        }
    }
}
