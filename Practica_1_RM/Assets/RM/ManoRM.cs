using UnityEngine;

// Va en LeftHandAnchor / RightHandAnchor del OVRCameraRig.
// Con el botón de agarre (grip) toma el Rigidbody más cercano a la mano y al soltarlo
// lo lanza con la velocidad que llevaba la mano. Sirve para "tirar" objetos en realidad mixta.
public class ManoRM : MonoBehaviour
{
    public bool derecha = true;
    public float radioDeAgarre = 0.10f;
    public float multiplicadorLanzamiento = 1.3f;

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
    }

    void FixedUpdate()
    {
        if (agarrado == null) return;
        agarrado.MovePosition(transform.TransformPoint(posLocal));
        agarrado.MoveRotation(transform.rotation * rotLocal);
    }

    private void Tomar()
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
        if (mejor == null) return;

        agarrado = mejor;
        eraCinematico = agarrado.isKinematic;
        agarrado.isKinematic = true;
        posLocal = transform.InverseTransformPoint(agarrado.position);
        rotLocal = Quaternion.Inverse(transform.rotation) * agarrado.rotation;
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
