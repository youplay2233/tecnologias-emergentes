using UnityEngine;

// Sin visor (editor o ejecutable de Linux): arrastrar con el botón izquierdo del ratón lo mismo que se
// agarra con la mano: cubos, pelotas, el robot armado o sus piezas despiezadas. La rueda acerca o aleja.
public class ArrastreRaton : MonoBehaviour
{
    private Rigidbody agarrado;
    private bool eraCinematico;
    private float distancia;
    private Vector3 desfase;
    private Vector3 velocidad;

    void Update()
    {
        Camera cam = CamaraActiva();
        if (cam == null) return;
        Ray rayo = cam.ScreenPointToRay(Input.mousePosition);

        if (Input.GetMouseButtonDown(0) && agarrado == null)
        {
            Vector3 punto;
            if (PunteroRM.Apuntado(rayo, 20f, out punto) != null) return;   // los botones los pulsa JarvisHUD
            RaycastHit golpe;
            if (Physics.Raycast(rayo, out golpe, 20f) && golpe.rigidbody != null && !ManoRM.Agarrados.Contains(golpe.rigidbody))
            {
                agarrado = golpe.rigidbody;
                ManoRM.Agarrados.Add(agarrado);
                eraCinematico = agarrado.isKinematic;
                agarrado.isKinematic = true;
                distancia = golpe.distance;
                desfase = agarrado.position - golpe.point;
                velocidad = Vector3.zero;
            }
        }
        if (agarrado == null) return;

        distancia = Mathf.Clamp(distancia + Input.mouseScrollDelta.y * 0.1f, 0.2f, 15f);
        Vector3 objetivo = rayo.GetPoint(distancia) + desfase;
        velocidad = Vector3.Lerp(velocidad, (objetivo - agarrado.position) / Mathf.Max(Time.deltaTime, 0.0001f), 0.5f);
        agarrado.transform.position = objetivo;

        bool alBote = Input.GetKeyDown(KeyCode.B) && BoteBasura.Instancia != null;
        if (Input.GetMouseButtonUp(0) || alBote)
        {
            Rigidbody rb = agarrado;
            agarrado = null;
            ManoRM.Agarrados.Remove(rb);
            rb.isKinematic = eraCinematico;
            Vector3 v = alBote ? BoteBasura.Instancia.VelocidadHacia(rb.position, 0.8f) : velocidad * 0.6f;
            PiezaRM pieza = rb.GetComponent<PiezaRM>();
            bool tirable = pieza != null || !eraCinematico;
            if (tirable && BoteBasura.Instancia != null && (alBote || v.magnitude > ManoRM.VelocidadDeLanzamiento))
            {
                if (pieza != null && pieza.modelo != null) pieza.modelo.Lanzar(pieza, v);
                else Proyectil.Lanzar(rb, v, encesto => Destroy(rb.gameObject));
            }
            else if (!eraCinematico) rb.velocity = v;
        }
    }

    private static Camera CamaraActiva()
    {
        foreach (Camera c in Camera.allCameras)
        {
            if (c.isActiveAndEnabled) return c;
        }
        return null;
    }
}
