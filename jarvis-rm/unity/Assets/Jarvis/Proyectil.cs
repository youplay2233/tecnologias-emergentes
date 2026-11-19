using System;
using System.Collections;
using UnityEngine;

// Algo lanzado hacia el bote de basura: si entra a la boca del bote cuenta como encestado; si no,
// desaparece igual al tocar algo (o a los pocos segundos). Al terminar se encoge y avisa con alTerminar.
public class Proyectil : MonoBehaviour
{
    private Action<bool> alTerminar;
    private float limite;
    private float desde;
    private string tocoCon = "";
    private float tocoEn = -1f;
    private bool terminado;
    private Vector3 escalaInicial;

    public static void Lanzar(Rigidbody rb, Vector3 velocidad, Action<bool> alTerminar)
    {
        Proyectil p = rb.GetComponent<Proyectil>();
        if (p == null) p = rb.gameObject.AddComponent<Proyectil>();
        p.alTerminar = alTerminar;
        p.limite = Time.time + 4f;
        p.desde = Time.time;
        p.tocoEn = -1f;
        p.terminado = false;
        p.escalaInicial = rb.transform.localScale;
        rb.isKinematic = false;
        rb.useGravity = true;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;   // rápido y chico: que no atraviese el bote
        rb.velocity = velocidad;
        rb.angularVelocity = UnityEngine.Random.insideUnitSphere * 5f;
        Holo.Sonido("blip", rb.position, 0.2f);
    }

    void Update()
    {
        if (terminado) return;
        bool tardo = Time.time > limite || transform.position.y < -1f;
        bool fallo = tocoEn > 0f && Time.time - tocoEn > 0.5f;   // rebotó en algo que no es el bote
        if (tardo || fallo)
        {
            Debug.Log("[Jarvis] " + name + " falló (" + (fallo ? "tocó " + tocoCon : "tiempo") + ") en " + transform.position.ToString("F2")
                      + "; bote en " + (BoteBasura.Instancia != null ? BoteBasura.Instancia.transform.position.ToString("F2") : "?"));
            Terminar(false);
        }
    }

    void OnCollisionEnter(Collision c)
    {
        // Rozar otras piezas del robot al salir no cuenta como fallar: solo el piso, objetos, etc.
        if (Time.time - desde < 0.2f || c.collider.GetComponentInParent<ModeloRobot>() != null) return;
        if (tocoEn < 0f) { tocoEn = Time.time; tocoCon = c.collider.name; }
    }

    void OnTriggerEnter(Collider c)
    {
        if (!terminado && c.GetComponentInParent<BoteBasura>() != null) Terminar(true);
    }

    private void Terminar(bool encesto)
    {
        terminado = true;
        BoteBasura.Registrar(encesto, transform.position);
        StartCoroutine(Desvanecer(encesto));
    }

    private IEnumerator Desvanecer(bool encesto)
    {
        for (float t = 0f; t < 1f; t += Time.deltaTime / 0.25f)
        {
            transform.localScale = escalaInicial * (1f - t);
            yield return null;
        }
        transform.localScale = escalaInicial;
        Action<bool> aviso = alTerminar;
        Destroy(this);
        if (aviso != null) aviso(encesto);
    }
}
