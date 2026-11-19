using UnityEngine;

// Se ejecuta antes que todo lo demás en la escena.
//
// El OVRCameraRig de Meta se guarda apagado en la escena. Al iniciar:
//   - En el Quest 3 (o en Windows con Quest Link) se enciende: aparece el passthrough y los controles.
//   - En Linux (editor o ejecutable de escritorio) no existe runtime de XR, así que se deja apagado:
//     en el editor se ve con la cámara de simulación y en el ejecutable con la cámara de escritorio.
[DefaultExecutionOrder(-20000)]
public class ArranqueRM : MonoBehaviour
{
    public GameObject rig;               // OVRCameraRig de Meta
    public GameObject camaraSimulacion;  // Solo existe en el editor (etiqueta EditorOnly)

    void Awake()
    {
        bool sinVisor = Application.platform == RuntimePlatform.LinuxEditor || Application.platform == RuntimePlatform.LinuxPlayer;
        EntradaRM.XRActivo = !sinVisor;

        if (rig != null)
        {
            rig.SetActive(!sinVisor);
        }
        if (camaraSimulacion != null)
        {
            camaraSimulacion.SetActive(Application.isEditor);
        }
    }
}
