using UnityEngine;

// Va en LeftHandAnchor / RightHandAnchor del OVRCameraRig.
// Con el botón de agarre (grip) toma el Rigidbody más cercano a la mano y al soltarlo
// lo lanza con la velocidad que llevaba la mano. Sirve para "tirar" objetos en realidad mixta.
// Agarre a distancia: si no hay nada junto a la mano, toma el objeto al que apunta el control (se ve un rayo)
// y lo trae a la mano. Así se alcanzan, por ejemplo, los objetos que crea Jarvis y que caen al piso.
public class ManoRM : MonoBehaviour
{
    public bool derecha = true;
    public float radioDeAgarre = 0.10f;
    public float alcanceLejano = 5f;
    public float multiplicadorLanzamiento = 1.3f;
    public LineRenderer rayo;   // se enciende solo cuando el control apunta a algo que se puede agarrar

    private Rigidbody agarrado;
    private bool eraCinematico;
    private bool agarrePrevio;
    private Vector3 posLocal;
    private Quaternion rotLocal;
    private Vector3 posAnterior;
    private Vector3 velocidadMano;

    void OnEnable()
    {
        posAnterior = transform.position;
        velocidadMano = Vector3.zero;
    }

    void Update()
    {
        // Velocidad de la mano, suavizada, para el lanzamiento
        float dt = Mathf.Max(Time.deltaTime, 0.0001f);
        Vector3 v = (transform.position - posAnterior) / dt;
        velocidadMano = Vector3.Lerp(velocidadMano, v, 0.5f);
        posAnterior = transform.position;

        bool agarre = derecha ? EntradaRM.AgarreDerecho() : EntradaRM.AgarreIzquierdo();
        if (agarre && !agarrePrevio && agarrado == null) Tomar();
        if (!agarre && agarrado != null) Soltar();
        agarrePrevio = agarre;
        MostrarRayo();
    }

    void FixedUpdate()
    {
        if (agarrado == null) return;
        agarrado.MovePosition(transform.TransformPoint(posLocal));
        agarrado.MoveRotation(transform.rotation * rotLocal);
    }

    private void Tomar()
    {
        Rigidbody mejor = MasCercano();
        bool aDistancia = false;
        if (mejor == null)
        {
            Vector3 punto;
            mejor = Apuntado(out punto);
            aDistancia = mejor != null;
        }
        if (mejor == null) return;

        agarrado = mejor;
        eraCinematico = agarrado.isKinematic;
        agarrado.isKinematic = true;
        // A distancia el objeto llega a la mano: queda unos centímetros enfrente del control
        posLocal = aDistancia ? Vector3.forward * 0.1f : transform.InverseTransformPoint(agarrado.position);
        rotLocal = Quaternion.Inverse(transform.rotation) * agarrado.rotation;
    }

    private Rigidbody MasCercano()
    {
        Collider[] cerca = Physics.OverlapSphere(transform.position, radioDeAgarre);
        Rigidbody mejor = null;
        float mejorDistancia = float.MaxValue;
        foreach (Collider c in cerca)
        {
            Rigidbody rb = c.attachedRigidbody;
            if (rb == null) continue;
            float d = (c.ClosestPoint(transform.position) - transform.position).sqrMagnitude;
            if (d < mejorDistancia)
            {
                mejorDistancia = d;
                mejor = rb;
            }
        }
        return mejor;
    }

    // El Rigidbody al que apunta el control (los cinemáticos no se traen a distancia)
    private Rigidbody Apuntado(out Vector3 punto)
    {
        punto = Vector3.zero;
        RaycastHit golpe;
        if (!Physics.SphereCast(transform.position, 0.04f, transform.forward, out golpe, alcanceLejano, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)) return null;
        if (golpe.rigidbody == null || golpe.rigidbody.isKinematic) return null;
        punto = golpe.point;
        return golpe.rigidbody;
    }

    private void MostrarRayo()
    {
        if (rayo == null) return;
        Vector3 punto = Vector3.zero;
        bool ver = agarrado == null && Apuntado(out punto) != null;
        rayo.enabled = ver;
        if (!ver) return;
        rayo.SetPosition(0, transform.position);
        rayo.SetPosition(1, punto);
    }

    private void Soltar()
    {
        agarrado.isKinematic = eraCinematico;
        if (!agarrado.isKinematic)
        {
            agarrado.velocity = velocidadMano * multiplicadorLanzamiento;
        }
        agarrado = null;
    }

    void OnDisable()
    {
        if (agarrado != null) Soltar();
    }
}
