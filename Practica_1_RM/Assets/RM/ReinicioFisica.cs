using UnityEngine;

// Regresa todos los Rigidbody hijos a su posición inicial para repetir el experimento
// sin reiniciar la app. Quest 3: botón A.  Editor: tecla R.
public class ReinicioFisica : MonoBehaviour
{
    public float repetirCada = 0f;   // 0 = solo manual

    private Rigidbody[] cuerpos;
    private Vector3[] posiciones;
    private Quaternion[] rotaciones;
    private float reloj;

    void Awake()
    {
        cuerpos = GetComponentsInChildren<Rigidbody>();
        posiciones = new Vector3[cuerpos.Length];
        rotaciones = new Quaternion[cuerpos.Length];
        for (int i = 0; i < cuerpos.Length; i++)
        {
            posiciones[i] = cuerpos[i].transform.position;
            rotaciones[i] = cuerpos[i].transform.rotation;
        }
    }

    void Update()
    {
        if (EntradaRM.BotonA() || Input.GetKeyDown(KeyCode.R)) Reiniciar();

        if (repetirCada > 0f)
        {
            reloj += Time.deltaTime;
            if (reloj >= repetirCada) Reiniciar();
        }
    }

    public void Reiniciar()
    {
        reloj = 0f;
        for (int i = 0; i < cuerpos.Length; i++)
        {
            Rigidbody rb = cuerpos[i];
            if (rb == null) continue;
            if (!rb.isKinematic)
            {
                rb.velocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
            }
            rb.transform.SetPositionAndRotation(posiciones[i], rotaciones[i]);
            rb.position = posiciones[i];
            rb.rotation = rotaciones[i];
        }
        BroadcastMessage("AlReiniciar", SendMessageOptions.DontRequireReceiver);
    }
}
