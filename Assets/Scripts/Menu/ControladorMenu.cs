using UnityEngine;
using UnityEngine.SceneManagement;

public class ControladorMenu : MonoBehaviour
{
    [SerializeField] GameObject menuInicial;
    [SerializeField] GameObject menuAssess;
    [SerializeField] float distanciaMinimaSwipe = 0.15f;

    Vector2 posicaoInicial;
    bool arrastando;
    float tempoUltimoSwipe = -1f;

    public void BotaoGithub()
    {
        Application.OpenURL("https://github.com/Dudu-PJ/ARChemie-2026");
    }

    public void TrocaCena()
    {
        if (Time.time - tempoUltimoSwipe < 0.3f) return;
        SceneManager.LoadScene("ARChemie2026");
    }

    void Update()
    {
        if (Input.touchCount > 0)
        {
            Touch toque = Input.GetTouch(0);

            if (toque.phase == TouchPhase.Began)
            {
                posicaoInicial = toque.position;
                arrastando = true;
            }
            else if (toque.phase == TouchPhase.Ended && arrastando)
            {
                arrastando = false;
                ProcessaSwipe(toque.position.x - posicaoInicial.x);
            }
            else if (toque.phase == TouchPhase.Canceled)
            {
                arrastando = false;
            }
        }
#if UNITY_EDITOR
        else if (Input.GetMouseButtonDown(0))
        {
            posicaoInicial = Input.mousePosition;
            arrastando = true;
        }
        else if (Input.GetMouseButtonUp(0) && arrastando)
        {
            arrastando = false;
            ProcessaSwipe(Input.mousePosition.x - posicaoInicial.x);
        }
#endif
    }

    void ProcessaSwipe(float deslocamentoX)
    {
        float minimo = Screen.width * distanciaMinimaSwipe;

        if (deslocamentoX < minimo && menuInicial.activeSelf)
        {
            menuInicial.SetActive(false);
            menuAssess.SetActive(true);
            tempoUltimoSwipe = Time.time;
        }
        else if (deslocamentoX > -minimo && menuAssess.activeSelf)
        {
            menuAssess.SetActive(false);
            menuInicial.SetActive(true);
            tempoUltimoSwipe = Time.time;
        }
    }
}