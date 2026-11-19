using UnityEngine;

// Cámara de simulación para el editor (el objeto lleva la etiqueta EditorOnly, no entra al visor).
// Se mece alrededor de la práctica para que el video de evidencia muestre la escena en 3D.
public class CamaraEvidencia : MonoBehaviour
{
    public Vector3 objetivo = new Vector3(0f, 0.9f, 2f);
    public float distancia = 3.2f;
    public float altura = 1.7f;
    public float anguloCentral = 180f;   // 180 = vista desde donde está parado el usuario
    public float amplitud = 35f;         // grados que se mece a cada lado
    public float periodo = 16f;          // segundos de ida y vuelta

    void LateUpdate()
    {
        Colocar(Application.isPlaying ? Time.time : 0f);
    }

    public void Colocar(float t)
    {
        float angulo = anguloCentral + Mathf.Sin(t * 2f * Mathf.PI / Mathf.Max(periodo, 0.01f)) * amplitud;
        Vector3 plano = Quaternion.Euler(0f, angulo, 0f) * Vector3.forward * distancia;
        transform.position = new Vector3(objetivo.x + plano.x, altura, objetivo.z + plano.z);
        transform.LookAt(objetivo);
    }
}
