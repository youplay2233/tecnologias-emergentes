using System.Collections.Generic;
using UnityEngine;

// Va en LeftHandAnchor / RightHandAnchor del OVRCameraRig.
// Toma el Rigidbody más cercano y al soltarlo lo lanza con la velocidad que llevaba la mano.
//   - Con control: botón de agarre (grip). Si no hay nada cerca, toma lo que apunta el control (rayo)
//     y lo trae a la mano.
//   - Con la mano (seguimiento de manos): pinza de pulgar e índice. El punto de agarre es el que queda
//     entre las dos puntas de los dedos, y con la punta del índice se pulsan los botones holográficos.
// Las piezas de un robot despiezado (PiezaRM) son cinemáticas: al soltarlas se quedan flotando.
public class ManoRM : MonoBehaviour
{
    public bool derecha = true;
    public float radioDeAgarre = 0.10f;      // con control (el ancla está en el control)
    public float radioDePinza = 0.06f;       // con la mano (alrededor del punto de pinza)
    public float alcanceLejano = 5f;
    public float multiplicadorLanzamiento = 1.3f;
    public LineRenderer rayo;   // se enciende solo cuando el control apunta a algo que se puede agarrar

#if TEC_META_XR
    public OVRHand mano;             // los asigna el constructor de la escena
    public OVRSkeleton esqueleto;
#endif

    // Lo que tiene agarrado alguna mano (o el ratón): ModeloRobot no mueve esas piezas mientras tanto
    public static readonly HashSet<Rigidbody> Agarrados = new HashSet<Rigidbody>();
    private static readonly List<ManoRM> Manos = new List<ManoRM>();
    public const float VelocidadDeLanzamiento = 1.3f;   // m/s: soltar más rápido que esto = tirarlo

    private Rigidbody agarrado;
    private bool eraCinematico;
    private bool mantenerDerecho;
    private bool agarrePrevio;
    private bool pinzando;
    private Vector3 posLocal;
    private Quaternion rotLocal;
    private Vector3 posAnterior;
    private Vector3 velocidadMano;
    private float proximoToque;
    private bool conEsqueleto;
    private string diagnostico = "";

#if TEC_META_XR
    // El esqueleto de la mano derecha o izquierda (lo usa la mano espejo)
    public static OVRSkeleton EsqueletoDe(bool derecha)
    {
        foreach (ManoRM m in Manos) if (m.derecha == derecha && m.mano != null && m.mano.IsTracked && m.mano.IsDataValid) return m.esqueleto;
        return null;
    }

    // El ancla de esa mano solo si se está siguiendo la mano real (no el control): su giro es el de la muñeca
    public static Transform AnclaDeManoReal(bool derecha)
    {
        foreach (ManoRM m in Manos) if (m.derecha == derecha && m.mano != null && m.mano.IsTracked && m.mano.IsDataValid) return m.transform;
        return null;
    }
#endif

    // Estado de las dos manos, para la consola del HUD y el registro (adb logcat -s Unity)
    public static string Diagnostico()
    {
        System.Text.StringBuilder sb = new System.Text.StringBuilder();
        foreach (ManoRM m in Manos)
        {
            if (sb.Length > 0) sb.Append("\n");
            sb.Append(m.diagnostico);
        }
        return sb.ToString();
    }

    void OnEnable()
    {
        Manos.Add(this);
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

        Vector3 punta, pinza;
        bool conMano = ManoSeguida(out punta, out pinza);
        bool agarre = conMano ? pinzando : (derecha ? EntradaRM.AgarreDerecho() : EntradaRM.AgarreIzquierdo());

        float radio = conMano ? (conEsqueleto ? radioDePinza : radioDeAgarre) : radioDeAgarre;
        if (agarre && !agarrePrevio && agarrado == null) Tomar(conMano ? pinza : transform.position, radio, !conMano, conMano);
        if (!agarre && agarrado != null) Soltar();
        agarrePrevio = agarre;

        if (conMano && agarrado == null) TocarBotones(punta);
        MostrarRayo(!conMano);
    }

    void FixedUpdate()
    {
        if (agarrado == null) return;
        agarrado.MovePosition(transform.TransformPoint(posLocal));
        Quaternion rotacion = transform.rotation * rotLocal;
        if (mantenerDerecho)
        {
            // El bote de basura solo gira hacia los lados: no se inclina con la muñeca
            Vector3 frente = Vector3.ProjectOnPlane(rotacion * Vector3.forward, Vector3.up);
            if (frente.sqrMagnitude > 0.0001f) rotacion = Quaternion.LookRotation(frente, Vector3.up);
        }
        agarrado.MoveRotation(rotacion);
    }

    // ------------------------------------------------------------------ seguimiento de manos

    // true si la mano (no el control) está siendo seguida; da la punta del índice y el punto de pinza
    private bool ManoSeguida(out Vector3 puntaIndice, out Vector3 puntoPinza)
    {
        puntaIndice = puntoPinza = transform.position;
        diagnostico = derecha ? "der: control" : "izq: control";
#if TEC_META_XR
        if (!EntradaRM.XRActivo || mano == null) return false;
        if (!mano.IsTracked)
        {
            diagnostico = (derecha ? "der" : "izq") + ": sin mano";
            return false;
        }
        if (!mano.IsDataValid)
        {
            // Con manos y controles a la vez, la mano que sostiene un control sigue "rastreada" pero con datos
            // no válidos: esa mano se maneja con el control (botón de agarre), no con la pinza.
            diagnostico = (derecha ? "der" : "izq") + ": control";
            pinzando = false;
            return false;
        }

        // Histéresis: se cierra con fuerza alta y se suelta con fuerza baja, para que no "parpadee"
        float fuerza = mano.GetFingerPinchStrength(OVRHand.HandFinger.Index);
        bool cierra = fuerza > 0.8f || mano.GetFingerIsPinching(OVRHand.HandFinger.Index);
        pinzando = pinzando ? fuerza > 0.45f : cierra;

        Transform pulgar = null, indice = null;
        if (esqueleto != null && esqueleto.IsInitialized && esqueleto.Bones != null)
        {
            foreach (OVRBone hueso in esqueleto.Bones)
            {
                if (hueso.Id == OVRSkeleton.BoneId.XRHand_ThumbTip || hueso.Id == OVRSkeleton.BoneId.Hand_ThumbTip) pulgar = hueso.Transform;
                else if (hueso.Id == OVRSkeleton.BoneId.XRHand_IndexTip || hueso.Id == OVRSkeleton.BoneId.Hand_IndexTip) indice = hueso.Transform;
            }
        }
        conEsqueleto = pulgar != null && indice != null;
        if (conEsqueleto)
        {
            puntaIndice = indice.position;
            puntoPinza = (pulgar.position + indice.position) * 0.5f;
        }
        else if (mano.IsPointerPoseValid && mano.PointerPose != null)
        {
            // Plan B sin esqueleto: la pose de puntero de Meta nace junto a los dedos
            puntaIndice = puntoPinza = mano.PointerPose.position;
        }
        diagnostico = (derecha ? "der" : "izq") + ": mano"
                      + " · pinza " + fuerza.ToString("0.00") + (pinzando ? " CERRADA" : "")
                      + (conEsqueleto ? "" : " · SIN esqueleto");
        return true;
#else
        return false;
#endif
    }

    // Tocar un botón holográfico con la punta del índice
    private void TocarBotones(Vector3 punta)
    {
        if (Time.time < proximoToque) return;
        foreach (Collider c in Physics.OverlapSphere(punta, 0.012f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Collide))
        {
            BotonHolo b = c.GetComponent<BotonHolo>();
            if (b == null) continue;
            b.Pulsar();
            proximoToque = Time.time + 0.6f;   // un toque = una pulsación
            return;
        }
    }

    // ------------------------------------------------------------------ agarrar y soltar

    private void Tomar(Vector3 punto, float radio, bool permitirLejano, bool conMano)
    {
        Rigidbody mejor = MasCercano(punto, radio);
        bool aDistancia = false;
        if (mejor == null && permitirLejano)
        {
            Vector3 apuntado;
            mejor = Apuntado(out apuntado);
            aDistancia = mejor != null;
        }
        if (mejor == null || Agarrados.Contains(mejor)) return;

        agarrado = mejor;
        Agarrados.Add(agarrado);
        mantenerDerecho = agarrado.GetComponent<BoteBasura>() != null;
        eraCinematico = agarrado.isKinematic;
        agarrado.isKinematic = true;
        // A distancia el objeto llega a la mano: queda unos centímetros enfrente del control.
        // Una pieza de robot se centra entre los dedos (o justo delante del control) para verla de cerca.
        if (agarrado.GetComponent<PiezaRM>() != null)
            posLocal = transform.InverseTransformPoint(punto) + (conMano ? Vector3.zero : Vector3.forward * 0.08f);
        else
            posLocal = aDistancia ? Vector3.forward * 0.1f : transform.InverseTransformPoint(agarrado.position);
        rotLocal = Quaternion.Inverse(transform.rotation) * agarrado.rotation;
        Holo.Sonido("blip", agarrado.position, 0.15f);
    }

    // El Rigidbody más cercano. Si el punto queda dentro de varios colliders (una pieza dentro del robot
    // armado, por ejemplo), gana el más pequeño.
    private Rigidbody MasCercano(Vector3 punto, float radio)
    {
        Rigidbody mejor = null;
        float mejorDistancia = float.MaxValue;
        foreach (Collider c in Physics.OverlapSphere(punto, radio))
        {
            Rigidbody rb = c.attachedRigidbody;
            if (rb == null || c.GetComponent<BotonHolo>() != null) continue;
            float d = (c.ClosestPoint(punto) - punto).sqrMagnitude;
            if (d < 0.000001f) d = c.bounds.extents.sqrMagnitude * 0.0001f;
            if (d < mejorDistancia)
            {
                mejorDistancia = d;
                mejor = rb;
            }
        }
        return mejor;
    }

    // El Rigidbody al que apunta el control. Los cinemáticos no se traen a distancia, salvo las piezas de robot y el bote.
    private Rigidbody Apuntado(out Vector3 punto)
    {
        punto = Vector3.zero;
        RaycastHit golpe;
        if (!Physics.SphereCast(transform.position, 0.04f, transform.forward, out golpe, alcanceLejano, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)) return null;
        Rigidbody rb = golpe.rigidbody;
        if (rb == null || (rb.isKinematic && rb.GetComponent<PiezaRM>() == null && rb.GetComponent<BoteBasura>() == null)) return null;
        punto = golpe.point;
        return rb;
    }

    private void MostrarRayo(bool conControl)
    {
        if (rayo == null) return;
        Vector3 punto = Vector3.zero;
        bool ver = conControl && agarrado == null && Apuntado(out punto) != null;
        rayo.enabled = ver;
        if (!ver) return;
        rayo.SetPosition(0, transform.position);
        rayo.SetPosition(1, punto);
    }

    private void Soltar()
    {
        Soltar(velocidadMano * multiplicadorLanzamiento, false);
    }

    // Soltar con impulso (o con el botón B) tira piezas y objetos al minijuego del bote de basura:
    // si encesta suma, y encesten o no desaparecen. Lo demás (el robot entero, la tableta) solo se suelta.
    private void Soltar(Vector3 velocidad, bool alBote)
    {
        Rigidbody rb = agarrado;
        agarrado = null;
        Agarrados.Remove(rb);
        rb.isKinematic = eraCinematico;

        PiezaRM pieza = rb.GetComponent<PiezaRM>();
        bool tirable = pieza != null || !eraCinematico;
        if (tirable && BoteBasura.Instancia != null && (alBote || velocidad.magnitude > VelocidadDeLanzamiento))
        {
            if (pieza != null && pieza.modelo != null) pieza.modelo.Lanzar(pieza, velocidad);
            else Proyectil.Lanzar(rb, velocidad, encesto => Destroy(rb.gameObject));
            return;
        }
        if (!rb.isKinematic) rb.velocity = velocidad;
    }

    // Botón B: lo que tenga agarrado cualquier mano sale disparado en arco hacia el bote
    public static bool LanzarAlBote()
    {
        if (BoteBasura.Instancia == null) return false;
        foreach (ManoRM m in Manos)
        {
            if (m.agarrado == null) continue;
            m.Soltar(BoteBasura.Instancia.VelocidadHacia(m.agarrado.position, 0.8f), true);
            return true;
        }
        return false;
    }

    void OnDisable()
    {
        Manos.Remove(this);
        if (agarrado != null) Soltar();
    }
}
