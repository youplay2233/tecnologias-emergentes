using UnityEngine;

// Carro omnidireccional (ruedas mecanum) del ejercicio 2 de MQTT. No sabe nada de MQTT: CarroMQTT le pasa las
// órdenes que llegan del broker (Mover, Luces, Direccional) y este script las ejecuta cuadro por cuadro.
// Con ruedas mecanum el carro avanza, va de lado o en diagonal sin girar, y también gira sobre su centro.
public class CarroOmnidireccional : MonoBehaviour
{
    [Header("Movimiento")]
    public float velocidad = 0.35f;                          // m/s
    public float velocidadGiro = 75f;                        // grados por segundo
    public Vector3 centroPista;                              // el carro no sale de la pista
    public Vector2 mitadPista = new Vector2(0.95f, 0.95f);   // media pista en X y en Z, en metros

    [Header("Ruedas: pivotes con el eje en su X local")]
    public Transform ruedaDelanteraIzquierda;
    public Transform ruedaDelanteraDerecha;
    public Transform ruedaTraseraIzquierda;
    public Transform ruedaTraseraDerecha;
    public float radioRueda = 0.0675f;                       // en el mundo
    public float mitadEjes = 0.3375f;                        // distancia del centro a la rueda en X más la de Z

    [Header("Luces")]
    public Renderer[] faros;
    public Renderer[] calaveras;
    public Renderer[] direccionalesIzquierdas;
    public Renderer[] direccionalesDerechas;
    public Light luzFaros;
    public Material faroApagado, faroEncendido, calaveraBaja, calaveraAlta, direccionalApagada, direccionalEncendida;

    public string Movimiento { get; private set; } = "alto";
    public bool LucesEncendidas { get; private set; }
    public string DireccionalActiva { get; private set; } = "apagada";

    private Vector3 mando;   // x: de lado (+ derecha), z: de frente (+ adelante), y: giro (+ a la derecha)
    private string direccionalPintada = "";   // qué direccional se ve prendida ahora (o "apagada")

    public bool Mover(string orden)
    {
        Vector3 m;
        switch (orden)
        {
            case "adelante": m = new Vector3(0f, 0f, 1f); break;
            case "atras": m = new Vector3(0f, 0f, -1f); break;
            case "izquierda": m = new Vector3(-1f, 0f, 0f); break;
            case "derecha": m = new Vector3(1f, 0f, 0f); break;
            case "adelante_izquierda": m = new Vector3(-1f, 0f, 1f); break;
            case "adelante_derecha": m = new Vector3(1f, 0f, 1f); break;
            case "atras_izquierda": m = new Vector3(-1f, 0f, -1f); break;
            case "atras_derecha": m = new Vector3(1f, 0f, -1f); break;
            case "girar_izquierda": m = new Vector3(0f, -1f, 0f); break;
            case "girar_derecha": m = new Vector3(0f, 1f, 0f); break;
            case "alto": m = Vector3.zero; break;
            default: return false;
        }
        // En diagonal va a la misma velocidad que en línea recta
        Vector2 plano = new Vector2(m.x, m.z);
        if (plano.sqrMagnitude > 1f)
        {
            plano.Normalize();
            m.x = plano.x;
            m.z = plano.y;
        }
        mando = m;
        Movimiento = orden;
        return true;
    }

    public bool Luces(string orden)
    {
        if (orden == "encender") LucesEncendidas = true;
        else if (orden == "apagar") LucesEncendidas = false;
        else return false;
        PintarLuces();
        return true;
    }

    public bool Direccional(string orden)
    {
        if (orden == "izquierda" || orden == "derecha") DireccionalActiva = orden;
        else if (orden == "apagar") DireccionalActiva = "apagada";
        else return false;
        return true;
    }

    void Start()
    {
        PintarLuces();
        PintarDireccionales(false);
    }

    void Update()
    {
        float dt = Time.deltaTime;

        // Avance en el marco del carro, sin salirse de la pista
        Vector3 local = new Vector3(mando.x, 0f, mando.z) * velocidad;
        transform.Rotate(0f, mando.y * velocidadGiro * dt, 0f, Space.World);
        Vector3 p = transform.position + transform.TransformDirection(local) * dt;
        p.x = Mathf.Clamp(p.x, centroPista.x - mitadPista.x, centroPista.x + mitadPista.x);
        p.z = Mathf.Clamp(p.z, centroPista.z - mitadPista.y, centroPista.z + mitadPista.y);
        transform.position = p;

        // Ruedas mecanum (cinemática inversa): de lado, las de una diagonal giran al revés que las de la otra
        float k = mitadEjes * mando.y * velocidadGiro * Mathf.Deg2Rad;
        Rodar(ruedaDelanteraIzquierda, local.z + local.x + k, dt);
        Rodar(ruedaDelanteraDerecha, local.z - local.x - k, dt);
        Rodar(ruedaTraseraIzquierda, local.z - local.x + k, dt);
        Rodar(ruedaTraseraDerecha, local.z + local.x - k, dt);

        // Direccionales: parpadean una vez y media por segundo
        bool prendida = DireccionalActiva != "apagada" && Mathf.Repeat(Time.time, 0.66f) < 0.33f;
        string pintada = prendida ? DireccionalActiva : "apagada";
        if (pintada != direccionalPintada) PintarDireccionales(prendida);
    }

    private void Rodar(Transform rueda, float velocidadLineal, float dt)
    {
        if (rueda != null) rueda.Rotate(Vector3.right, velocidadLineal / radioRueda * Mathf.Rad2Deg * dt, Space.Self);
    }

    private void PintarLuces()
    {
        Pintar(faros, LucesEncendidas ? faroEncendido : faroApagado);
        Pintar(calaveras, LucesEncendidas ? calaveraAlta : calaveraBaja);
        if (luzFaros != null) luzFaros.enabled = LucesEncendidas;
    }

    private void PintarDireccionales(bool prendida)
    {
        direccionalPintada = prendida ? DireccionalActiva : "apagada";
        Pintar(direccionalesIzquierdas, prendida && DireccionalActiva == "izquierda" ? direccionalEncendida : direccionalApagada);
        Pintar(direccionalesDerechas, prendida && DireccionalActiva == "derecha" ? direccionalEncendida : direccionalApagada);
    }

    private static void Pintar(Renderer[] piezas, Material material)
    {
        if (piezas == null || material == null) return;
        foreach (Renderer r in piezas)
            if (r != null) r.sharedMaterial = material;
    }
}
