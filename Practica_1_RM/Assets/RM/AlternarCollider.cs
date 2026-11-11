using UnityEngine;

// Prueba de la Sesión 2: "Desactivar temporalmente el Collider del piso y observar el resultado".
// Quest 3: gatillo izquierdo.  Editor: tecla C.
public class AlternarCollider : MonoBehaviour
{
    public Collider objetivo;

    void Update()
    {
        if (EntradaRM.GatilloIzquierdoAbajo() || Input.GetKeyDown(KeyCode.C)) Alternar();
    }

    public void Alternar()
    {
        if (objetivo == null) return;
        objetivo.enabled = !objetivo.enabled;
        Debug.Log("Collider de " + objetivo.name + ": " + (objetivo.enabled ? "ACTIVADO" : "DESACTIVADO"));
    }

    void AlReiniciar()
    {
        if (objetivo != null) objetivo.enabled = true;
    }
}
