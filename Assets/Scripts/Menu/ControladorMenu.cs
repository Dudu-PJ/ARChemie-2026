using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class ControladorMenu : MonoBehaviour
{
    Canvas menuInicial;
    Canvas menuAssess;

    void Start()
    {
        menuInicial = GameObject.Find("MenuInicial").GetComponent<Canvas>();
        menuAssess = GameObject.Find("MenuAssess").GetComponent<Canvas>();
    }

    public void BotaoGithub()
    {
        Application.OpenURL("https://github.com/Dudu-PJ/ARChemie-2026");
    }

    public void TrocaCena()
    {
        SceneManager.LoadScene("ARChemie2026");
    }

    void OnUpdate()
    {
        if (Input.touchCount > 0)
        {
            Touch toque = Input.GetTouch(0);
            if (toque.deltaPosition.x > 0 && menuInicial.enabled == true)
            {
                menuInicial.enabled = false;
                menuAssess.enabled = true;
            }
            else if (toque.deltaPosition.x < 0 && menuAssess.enabled == true)
            {
                menuInicial.enabled = true;
                menuAssess.enabled = false;
            }
        }
    }
}
