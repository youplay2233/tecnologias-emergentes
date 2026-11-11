using UnityEngine;

// "La masa modifica la respuesta a una fuerza o impacto":
// aplica EL MISMO impulso a cada Rigidbody hijo. El de masa 1 sale volando,
// el de masa 100 casi no se mueve. Quest 3: botón B.  Editor: tecla E.
public class EmpujeDePrueba : MonoBehaviour
{
    public Vector3 impulso = new Vector3(0f, 0f, 6f);

    void Update()
    {
        if (EntradaRM.BotonB() || Input.GetKeyDown(KeyCode.E)) Empujar();
    }

    public void Empujar()
    {
        foreach (Rigidbody rb in GetComponentsInChildren<Rigidbody>())
        {
            if (!rb.isKinematic) rb.AddForce(impulso, ForceMode.Impulse);
        }
    }
}
