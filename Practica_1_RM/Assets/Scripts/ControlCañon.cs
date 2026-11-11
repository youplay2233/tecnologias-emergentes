using UnityEngine;

public class ControlCañon : MonoBehaviour
{
    // VARIABLES PÚBLICAS (Se verán en el Inspector)
    public GameObject prefabBala;      // ¿Qué disparamos?
    public Transform puntoSalida;      // ¿Desde dónde sale?
    public float fuerzaDisparo = 500f; // ¿Con qué fuerza?

    void Update()
    {
        // Si pulsamos clic izquierdo (0)
        if (Input.GetMouseButtonDown(0))
        {
            Disparar();
        }
    }

    void Disparar()
    {
        // 1. Crear la bala (Qué, Dónde, Con qué rotación)
        GameObject nuevaBala = Instantiate(prefabBala, puntoSalida.position, puntoSalida.rotation);

        // 2. Obtener el Rigidbody de ESA bala nueva
        Rigidbody rbBala = nuevaBala.GetComponent<Rigidbody>();

        // 3. Empujarla hacia adelante
        rbBala.AddForce(puntoSalida.forward * fuerzaDisparo);
    }
}
