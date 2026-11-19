using UnityEngine;

// Va en RightHandAnchor: un rayo sale del control derecho; si toca un BotonHolo lo ilumina
// y el gatillo lo pulsa. En el editor (sin visor) se usa el ratón: ver JarvisHUD.
public class PunteroRM : MonoBehaviour
{
    public float alcance = 3f;
    private LineRenderer rayo;

    void Start()
    {
        rayo = Holo.Linea(transform, "Rayo_botones", Holo.Cian, 0.003f, true, 2);
        rayo.enabled = false;
    }

    void Update()
    {
        Vector3 punto;
        BotonHolo boton = Apuntado(new Ray(transform.position, transform.forward), alcance, out punto);
        rayo.enabled = boton != null;
        if (boton == null) return;
        rayo.SetPosition(0, transform.position);
        rayo.SetPosition(1, punto);
        boton.Encima();
        if (EntradaRM.GatilloDerechoAbajo()) boton.Pulsar();
    }

    // El BotonHolo más cercano en la dirección del rayo (ignora lo que no es botón, como el robot)
    public static BotonHolo Apuntado(Ray rayo, float alcance, out Vector3 punto)
    {
        punto = Vector3.zero;
        BotonHolo mejor = null;
        float mejorDistancia = float.MaxValue;
        foreach (RaycastHit h in Physics.RaycastAll(rayo, alcance, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Collide))
        {
            BotonHolo b = h.collider.GetComponent<BotonHolo>();
            if (b == null || h.distance >= mejorDistancia) continue;
            mejor = b;
            mejorDistancia = h.distance;
            punto = h.point;
        }
        return mejor;
    }
}
