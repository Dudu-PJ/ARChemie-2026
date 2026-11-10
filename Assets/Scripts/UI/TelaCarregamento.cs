using System.Collections;
using UnityEngine;

[RequireComponent(typeof(CanvasGroup))]
public class TelaCarregamento : MonoBehaviour
{
    public Detector detector;
    public float tempoMinimo = 1f;
    public float duracaoFade = 0.4f;

    private CanvasGroup grupo;

    IEnumerator Start()
    {
        grupo = GetComponent<CanvasGroup>();
        grupo.alpha = 1f;
        grupo.blocksRaycasts = true;

        float inicio = Time.unscaledTime;

        while (!detector.ModeloPronto || Time.unscaledTime - inicio < tempoMinimo)
        {
            yield return null;
        }

        float t = 0f;
        while (t < duracaoFade)
        {
            t += Time.unscaledDeltaTime;
            grupo.alpha = 1f - (t / duracaoFade);
            yield return null;
        }

        grupo.blocksRaycasts = false;
        gameObject.SetActive(false);
    }
}