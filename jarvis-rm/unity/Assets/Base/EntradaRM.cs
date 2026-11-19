using System;
using UnityEngine;

// Entrada de los controles Touch del Meta Quest 3 (Meta XR Core SDK: OVRInput).
// Un solo lugar para leer botones, así los scripts de las prácticas quedan iguales a los de clase.
// En el editor sin visor todas las funciones devuelven false / cero.
public static class EntradaRM
{
    // Lo apaga ArranqueRM cuando no hay visor (por ejemplo, en el editor de Linux).
    public static bool XRActivo = true;

    private static bool fallo;

    public static bool GatilloDerechoAbajo() { return Boton(BotonRM.GatilloDerecho, true); }
    public static bool GatilloIzquierdoAbajo() { return Boton(BotonRM.GatilloIzquierdo, true); }
    public static bool AgarreDerecho() { return Boton(BotonRM.AgarreDerecho, false); }
    public static bool AgarreIzquierdo() { return Boton(BotonRM.AgarreIzquierdo, false); }
    public static bool BotonA() { return Boton(BotonRM.A, true); }
    public static bool BotonB() { return Boton(BotonRM.B, true); }
    public static bool BotonX() { return Boton(BotonRM.X, true); }
    public static bool BotonY() { return Boton(BotonRM.Y, true); }

    public enum BotonRM { GatilloDerecho, GatilloIzquierdo, AgarreDerecho, AgarreIzquierdo, A, B, X, Y }

    // soloAlPresionar = true  -> verdadero únicamente en el cuadro en que se presiona
    // soloAlPresionar = false -> verdadero mientras se mantiene presionado
    public static bool Boton(BotonRM boton, bool soloAlPresionar)
    {
        if (!XRActivo || fallo) return false;
#if TEC_META_XR
        try
        {
            OVRInput.RawButton crudo;
            switch (boton)
            {
                case BotonRM.GatilloDerecho: crudo = OVRInput.RawButton.RIndexTrigger; break;
                case BotonRM.GatilloIzquierdo: crudo = OVRInput.RawButton.LIndexTrigger; break;
                case BotonRM.AgarreDerecho: crudo = OVRInput.RawButton.RHandTrigger; break;
                case BotonRM.AgarreIzquierdo: crudo = OVRInput.RawButton.LHandTrigger; break;
                case BotonRM.A: crudo = OVRInput.RawButton.A; break;
                case BotonRM.B: crudo = OVRInput.RawButton.B; break;
                case BotonRM.X: crudo = OVRInput.RawButton.X; break;
                default: crudo = OVRInput.RawButton.Y; break;
            }
            return soloAlPresionar ? OVRInput.GetDown(crudo) : OVRInput.Get(crudo);
        }
        catch (Exception e)
        {
            Fallo(e);
            return false;
        }
#else
        return false;
#endif
    }

    // Palanca (joystick) del control derecho: x = izquierda/derecha, y = adelante/atrás.
    public static Vector2 PalancaDerecha()
    {
        if (!XRActivo || fallo) return Vector2.zero;
#if TEC_META_XR
        try
        {
            return OVRInput.Get(OVRInput.RawAxis2D.RThumbstick);
        }
        catch (Exception e)
        {
            Fallo(e);
            return Vector2.zero;
        }
#else
        return Vector2.zero;
#endif
    }

    private static void Fallo(Exception e)
    {
        fallo = true;
        Debug.LogWarning("EntradaRM: los controles de Meta no están disponibles aquí (" + e.GetType().Name + "). Se usan teclado y ratón.");
    }
}
