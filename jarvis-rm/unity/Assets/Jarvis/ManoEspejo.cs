using UnityEngine;

// "Mano espejo": la Shadow Hand imita los dedos de tu mano derecha usando el seguimiento de manos del
// Quest. De cada dedo se toman las posiciones de sus articulaciones (esqueleto OpenXR), se mide cuánto
// dobla cada nudillo (ángulo entre huesos seguidos) y se aplica a la articulación equivalente del robot.
public class ManoEspejo : MonoBehaviour
{
    public ModeloRobot modelo;
    public bool activa;

    // Giro relativo: al calibrar se guarda cómo estaban tu muñeca y la mano robótica; después la mano
    // robótica gira lo mismo que tu muñeca desde ese momento. Así no hay que alinear ejes entre los dos.
    private bool calibrada;
    private Quaternion muñecaInicial, modeloInicial;

    public string Activar(bool si)
    {
        if (si && (modelo == null || modelo.Clave != "mano")) return "La mano espejo funciona con la Shadow Hand: ábrela primero.";
        activa = si;
        calibrada = false;   // activar (o "reconectar") vuelve a calibrar
        if (si) modelo.DetenerAnimacion();
        else if (modelo != null && modelo.Cargado)
        {
            modelo.OrientarDesde(null);
            modelo.Animar("reposo");
        }
        if (!si) return "Mano espejo apagada.";
        bool veo = ManoRM.AnclaDeManoReal(true) != null;
        return "Mano espejo activa: la Shadow Hand copia los dedos y el giro de la muñeca derecha."
               + (veo ? "" : " Ahora mismo no veo la mano derecha: hay que soltar el control derecho para que el visor la siga.");
    }

    void Update()
    {
        if (!activa || modelo == null || !modelo.Cargado || modelo.Clave != "mano") return;
        Transform muñeca = ManoRM.AnclaDeManoReal(true);
        if (muñeca == null)
        {
            calibrada = false;   // al volver la mano se calibra de nuevo y la mano robótica sigue desde donde quedó
            return;
        }
        if (!calibrada)
        {
            muñecaInicial = muñeca.rotation;
            // El modelo está de frente al usuario (como un reflejo); se voltea para que apunte igual que su mano:
            // antebrazo hacia él y dedos hacia adelante
            modeloInicial = Quaternion.AngleAxis(180f, Vector3.up) * modelo.GiroActual;
            calibrada = true;
        }
        modelo.OrientarDesde(muñeca.rotation * Quaternion.Inverse(muñecaInicial) * modeloInicial);
#if TEC_META_XR
        OVRSkeleton esqueleto = ManoRM.EsqueletoDe(true);
        if (esqueleto == null || !esqueleto.IsInitialized || esqueleto.Bones == null) return;

        System.Collections.Generic.Dictionary<OVRSkeleton.BoneId, Vector3> h = new System.Collections.Generic.Dictionary<OVRSkeleton.BoneId, Vector3>();
        foreach (OVRBone b in esqueleto.Bones) h[b.Id] = b.Transform.position;

        Dedo(h, "FF", OVRSkeleton.BoneId.XRHand_IndexMetacarpal, OVRSkeleton.BoneId.XRHand_IndexProximal,
             OVRSkeleton.BoneId.XRHand_IndexIntermediate, OVRSkeleton.BoneId.XRHand_IndexDistal, OVRSkeleton.BoneId.XRHand_IndexTip);
        Dedo(h, "MF", OVRSkeleton.BoneId.XRHand_MiddleMetacarpal, OVRSkeleton.BoneId.XRHand_MiddleProximal,
             OVRSkeleton.BoneId.XRHand_MiddleIntermediate, OVRSkeleton.BoneId.XRHand_MiddleDistal, OVRSkeleton.BoneId.XRHand_MiddleTip);
        Dedo(h, "RF", OVRSkeleton.BoneId.XRHand_RingMetacarpal, OVRSkeleton.BoneId.XRHand_RingProximal,
             OVRSkeleton.BoneId.XRHand_RingIntermediate, OVRSkeleton.BoneId.XRHand_RingDistal, OVRSkeleton.BoneId.XRHand_RingTip);
        Dedo(h, "LF", OVRSkeleton.BoneId.XRHand_LittleMetacarpal, OVRSkeleton.BoneId.XRHand_LittleProximal,
             OVRSkeleton.BoneId.XRHand_LittleIntermediate, OVRSkeleton.BoneId.XRHand_LittleDistal, OVRSkeleton.BoneId.XRHand_LittleTip);

        // Pulgar: el robot tiene 5 articulaciones; se copian las dos de la punta (las más visibles)
        Vector3 m, p, d, t;
        if (h.TryGetValue(OVRSkeleton.BoneId.XRHand_ThumbMetacarpal, out m) && h.TryGetValue(OVRSkeleton.BoneId.XRHand_ThumbProximal, out p) &&
            h.TryGetValue(OVRSkeleton.BoneId.XRHand_ThumbDistal, out d) && h.TryGetValue(OVRSkeleton.BoneId.XRHand_ThumbTip, out t))
        {
            modelo.FijarArticulacion("rh_THJ2", Doblez(m, p, d));
            modelo.FijarArticulacion("rh_THJ1", Doblez(p, d, t));
        }
#endif
    }

#if TEC_META_XR
    private void Dedo(System.Collections.Generic.Dictionary<OVRSkeleton.BoneId, Vector3> h, string dedo,
                      OVRSkeleton.BoneId metacarpo, OVRSkeleton.BoneId proximal, OVRSkeleton.BoneId medio,
                      OVRSkeleton.BoneId distal, OVRSkeleton.BoneId punta)
    {
        Vector3 m, p, i, d, t;
        if (!h.TryGetValue(metacarpo, out m) || !h.TryGetValue(proximal, out p) || !h.TryGetValue(medio, out i) ||
            !h.TryGetValue(distal, out d) || !h.TryGetValue(punta, out t)) return;
        modelo.FijarArticulacion("rh_" + dedo + "J3", Doblez(m, p, i));   // nudillo
        modelo.FijarArticulacion("rh_" + dedo + "J2", Doblez(p, i, d));   // articulación media
        modelo.FijarArticulacion("rh_" + dedo + "J1", Doblez(i, d, t));   // punta
    }
#endif

    // Ángulo (radianes) entre el hueso a->b y el siguiente b->c: 0 = recto
    private static float Doblez(Vector3 a, Vector3 b, Vector3 c)
    {
        return Vector3.Angle(b - a, c - b) * Mathf.Deg2Rad;
    }
}
