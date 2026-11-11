using UnityEngine;

// Las cabinas de la rueda de la fortuna son hijas de la rueda (giran con ella),
// pero deben colgar siempre derechas: cada cuadro se restaura su rotación global inicial.
public class MantenerVertical : MonoBehaviour
{
    private Quaternion rotacionInicial;

    void Start()
    {
        rotacionInicial = transform.rotation;
    }

    void LateUpdate()
    {
        transform.rotation = rotacionInicial;
    }
}
