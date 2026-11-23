using UnityEngine;
using UnityEngine.SceneManagement;

public class NavegacionEscenas : MonoBehaviour
{
	public void IrAInicio()
	{
	   SceneManager.LoadScene("Inicio");
	}

	public void IrAActividad1()
	{
	   SceneManager.LoadScene("Actividad1");
	}

	public void IrAActividad2()
	{
	   SceneManager.LoadScene("Actividad2");
	}
}
