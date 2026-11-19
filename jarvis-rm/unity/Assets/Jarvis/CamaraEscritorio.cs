using UnityEngine;

// Cámara para usar Jarvis sin visor (ejecutable de Linux): botón derecho del ratón para mirar,
// WASD / Q / E para moverse. No hay passthrough, así que el fondo es oscuro.
public class CamaraEscritorio : MonoBehaviour
{
    public float velocidad = 1.2f;
    public float sensibilidad = 2.5f;
    private float yaw, pitch = 8f;

    public static Camera Crear()
    {
        GameObject go = new GameObject("CamaraEscritorio");
        go.tag = "MainCamera";
        go.transform.position = new Vector3(0f, 1.55f, -0.25f);
        Camera cam = go.AddComponent<Camera>();
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.03f, 0.05f, 0.07f);
        cam.nearClipPlane = 0.05f;
        cam.fieldOfView = 70f;
        go.AddComponent<AudioListener>();
        go.AddComponent<CamaraEscritorio>();
        return cam;
    }

    void Update()
    {
        if (Input.GetMouseButton(1))
        {
            yaw += Input.GetAxis("Mouse X") * sensibilidad;
            pitch = Mathf.Clamp(pitch - Input.GetAxis("Mouse Y") * sensibilidad, -80f, 80f);
        }
        transform.rotation = Quaternion.Euler(pitch, yaw, 0f);
        Vector3 mover = new Vector3(
            (Input.GetKey(KeyCode.D) ? 1 : 0) - (Input.GetKey(KeyCode.A) ? 1 : 0),
            (Input.GetKey(KeyCode.E) ? 1 : 0) - (Input.GetKey(KeyCode.Q) ? 1 : 0),
            (Input.GetKey(KeyCode.W) ? 1 : 0) - (Input.GetKey(KeyCode.S) ? 1 : 0));
        transform.position += transform.rotation * mover * velocidad * Time.deltaTime;
    }
}
