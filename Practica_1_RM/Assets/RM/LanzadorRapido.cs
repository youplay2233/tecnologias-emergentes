using UnityEngine;

// Experimento Discrete vs Continuous: lanza la pelota a gran velocidad contra una pared delgada.
// Con Discrete la pelota "salta" de un lado al otro entre dos pasos de física y atraviesa la pared (tunneling).
// Con Continuous el motor revisa la trayectoria completa y la colisión sí se detecta.
[RequireComponent(typeof(Rigidbody))]
public class LanzadorRapido : MonoBehaviour
{
    public float velocidad = 60f;                 // m/s. Súbela o bájala para comparar
    public Vector3 direccion = Vector3.forward;   // dirección local del lanzamiento

    private Rigidbody rb;

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        Lanzar();
    }

    public void Lanzar()
    {
        if (rb == null) rb = GetComponent<Rigidbody>();
        rb.velocity = transform.TransformDirection(direccion.normalized) * velocidad;
    }

    // Lo llama ReinicioFisica al repetir el experimento
    void AlReiniciar()
    {
        TrailRenderer estela = GetComponent<TrailRenderer>();
        if (estela != null) estela.Clear();
        Lanzar();
    }
}
