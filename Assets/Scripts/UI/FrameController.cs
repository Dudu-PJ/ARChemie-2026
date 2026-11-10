
using UnityEngine;
using System.Collections;

public class FrameController : MonoBehaviour
{
    public GameObject molduraDetector;
    public Animator animatorMoldura;

    private bool transicaoEmAndamento;

    public void ReproduzirSaida()
    {
        if (transicaoEmAndamento)
            return;

        StartCoroutine(SaidaCoroutine());
    }

    private IEnumerator SaidaCoroutine()
    {
        transicaoEmAndamento = true;

        animatorMoldura.Play("SaidaMoldura", 0, 0f);

        yield return null;

        AnimatorStateInfo estado =
            animatorMoldura.GetCurrentAnimatorStateInfo(0);

        yield return new WaitForSeconds(
            estado.length
        );

        molduraDetector.SetActive(false);

        transicaoEmAndamento = false;
    }
}
