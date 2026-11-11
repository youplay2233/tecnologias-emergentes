using UnityEngine;

// Movimiento del "siguiente paso" del Parque de diversiones:
// gira el objeto PADRE y todos sus hijos giran con él (jerarquía).
public class Girar : MonoBehaviour
{
    public Vector3 eje = Vector3.up;        // Eje local de giro
    public float gradosPorSegundo = 30f;    // Velocidad

    void Update()
    {
        transform.Rotate(eje, gradosPorSegundo * Time.deltaTime, Space.Self);
    }
}
