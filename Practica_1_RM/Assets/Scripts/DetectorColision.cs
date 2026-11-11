using UnityEngine;

public class DetectorColision : MonoBehaviour
{
    private void OnCollisionEnter(Collision collision)
    {
        Debug.Log("Colisión con: " +
                  collision.gameObject.name);
    }
}
