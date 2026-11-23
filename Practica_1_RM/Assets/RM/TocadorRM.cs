using UnityEngine;

// Va en LeftHandAnchor / RightHandAnchor del OVRCameraRig, en las prácticas con botones o figuras para tocar.
// De cerca: acercar el control o la mano a un botón o figura lo toca.
// A distancia: apuntar (se ve un rayo) y apretar el gatillo de esa mano.
public class TocadorRM : MonoBehaviour
{
    public bool derecha = true;
    public float radioToque = 0.04f;
    public float alcance = 5f;
    public LineRenderer rayo;

    private ITocableRM tocadoAntes;   // no repite el toque mientras la mano siga dentro

    void Update()
    {
        // De cerca
        ITocableRM cerca = null;
        foreach (Collider c in Physics.OverlapSphere(transform.position, radioToque, ~0, QueryTriggerInteraction.Collide))
        {
            cerca = c.GetComponentInParent<ITocableRM>();
            if (cerca != null) break;
        }
        if (cerca != null && cerca != tocadoAntes) cerca.Tocar();
        tocadoAntes = cerca;

        // A distancia
        ITocableRM apuntado = null;
        RaycastHit golpe;
        if (Physics.Raycast(transform.position, transform.forward, out golpe, alcance, ~0, QueryTriggerInteraction.Collide))
        {
            apuntado = golpe.collider.GetComponentInParent<ITocableRM>();
        }
        if (rayo != null)
        {
            rayo.enabled = apuntado != null;
            if (apuntado != null)
            {
                rayo.SetPosition(0, transform.position);
                rayo.SetPosition(1, golpe.point);
            }
        }
        bool gatillo = derecha ? EntradaRM.GatilloDerechoAbajo() : EntradaRM.GatilloIzquierdoAbajo();
        if (gatillo && apuntado != null) apuntado.Tocar();
    }
}
