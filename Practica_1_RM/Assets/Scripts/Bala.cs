using UnityEngine;

public class Bala : MonoBehaviour
{
    // Una variable pública para ajustar el tiempo desde el editor
    public float tiempoDeVida = 3.0f;

    void Start()
    {
        // Destroy tiene un segundo argumento opcional: el tiempo de espera
        Destroy(gameObject, tiempoDeVida);
    }
}
