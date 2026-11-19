using UnityEngine;

public class MovToques : MonoBehaviour
{
    public float multRotacao = 0.1f;
    public float fatorMin = 0.2f;
    public float fatorMax = 5f;

    private bool pincaAtiva;
    private float distInicial;
    private Vector3 escalaInicial;

    private Camera cam;

    void Awake()
    {
        cam = Camera.main;
    }

    public void MovPinca(GameObject moleculaAtual)
    {
        if (moleculaAtual == null || Input.touchCount != 2)
        {
            pincaAtiva = false;
            return;
        }

        Touch toque0 = Input.GetTouch(0);
        Touch toque1 = Input.GetTouch(1);

        bool algumTerminou =
            toque0.phase == TouchPhase.Ended || toque0.phase == TouchPhase.Canceled ||
            toque1.phase == TouchPhase.Ended || toque1.phase == TouchPhase.Canceled;

        if (algumTerminou)
        {
            pincaAtiva = false;
            return;
        }

        float distAtual = Vector2.Distance(toque0.position, toque1.position);
        bool comecou = toque0.phase == TouchPhase.Began || toque1.phase == TouchPhase.Began;

        if (!pincaAtiva || comecou)
        {
            distInicial = distAtual;
            escalaInicial = moleculaAtual.transform.localScale;
            pincaAtiva = distInicial > 0.01f;
            return;
        }

        float fator = Mathf.Clamp(distAtual / distInicial, fatorMin, fatorMax);
        moleculaAtual.transform.localScale = escalaInicial * fator;
    }

    public void MovGiro(GameObject moleculaAtual)
    {
        if (moleculaAtual == null || Input.touchCount != 1) return;

        Touch toque = Input.GetTouch(0);
        if (toque.phase != TouchPhase.Moved) return;

        if (cam == null) cam = Camera.main;

        moleculaAtual.transform.Rotate(Vector3.up, -toque.deltaPosition.x * multRotacao, Space.World);
        moleculaAtual.transform.Rotate(cam.transform.right, toque.deltaPosition.y * multRotacao, Space.World);
    }
}