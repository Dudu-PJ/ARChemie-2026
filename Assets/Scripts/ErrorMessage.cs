
using System.Collections;
using UnityEngine;

public class ErrorMessage : MonoBehaviour
{
    public Animator animator;
    public float duracao = 2.5f;

    private Coroutine rotina;

    public void Mostrar()
    {
        if (animator == null)
        {
            Debug.LogWarning("ErrorMessage: Animator não atribuído.");
            return;
        }

        if (rotina != null)
            StopCoroutine(rotina);

        gameObject.SetActive(true);
        animator.gameObject.SetActive(true);
        animator.Play("EntradaAviso", 0, 0f);

        rotina = StartCoroutine(SequenciaAviso());
    }

    private IEnumerator SequenciaAviso()
    {
        yield return new WaitForSeconds(duracao);

        animator.Play("SaidaAviso", 0, 0f);

        yield return null;

        while (animator.GetCurrentAnimatorStateInfo(0).IsName("SaidaAviso") &&
               animator.GetCurrentAnimatorStateInfo(0).normalizedTime < 1f)
        {
            yield return null;
        }

        animator.gameObject.SetActive(false);
        rotina = null;
    }
}
