using UnityEngine;

// Gira el montaje del cañón con la palanca derecha (o las flechas del teclado).
// Se gira el PADRE: el cañón y su PuntoDisparo, que son hijos, se mueven con él.
public class ApuntarRM : MonoBehaviour
{
    public float gradosPorSegundo = 45f;
    public float elevacionMinima = -5f;
    public float elevacionMaxima = 40f;

    private float giro;
    private float elevacion;

    void Update()
    {
        Vector2 palanca = EntradaRM.PalancaDerecha();
        float x = palanca.x + Input.GetAxisRaw("Horizontal");
        float y = palanca.y + Input.GetAxisRaw("Vertical");
        if (Mathf.Abs(x) < 0.15f && Mathf.Abs(y) < 0.15f) return;

        giro += x * gradosPorSegundo * Time.deltaTime;
        elevacion = Mathf.Clamp(elevacion + y * gradosPorSegundo * Time.deltaTime, elevacionMinima, elevacionMaxima);
        transform.localRotation = Quaternion.Euler(-elevacion, giro, 0f);
    }
}
