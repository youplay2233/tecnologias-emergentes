using UnityEngine;
using UnityEngine.SceneManagement;

// Cambia de práctica sin quitarse el visor.
// Quest 3: botón Y = siguiente práctica, botón X = anterior (control izquierdo).
// Editor:  tecla N = siguiente, tecla P = anterior.
public class MenuPracticasRM : MonoBehaviour
{
    public string[] escenas;
    public string[] titulos;
    public TextMesh letrero;

    private int indice;

    void Start()
    {
        string actual = SceneManager.GetActiveScene().name;
        indice = 0;
        for (int i = 0; i < escenas.Length; i++)
        {
            if (escenas[i] == actual) indice = i;
        }
        if (letrero != null && titulos != null && indice < titulos.Length)
        {
            letrero.text = titulos[indice] + "\n(Y) siguiente    (X) anterior";
        }
    }

    void Update()
    {
        if (escenas == null || escenas.Length == 0) return;
        if (EntradaRM.BotonY() || Input.GetKeyDown(KeyCode.N)) Cargar(indice + 1);
        else if (EntradaRM.BotonX() || Input.GetKeyDown(KeyCode.P)) Cargar(indice - 1);
    }

    private void Cargar(int nuevo)
    {
        nuevo = (nuevo + escenas.Length) % escenas.Length;
        SceneManager.LoadScene(escenas[nuevo]);
    }
}
