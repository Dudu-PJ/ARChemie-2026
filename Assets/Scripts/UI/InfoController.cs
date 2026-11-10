
using UnityEngine;
using System.Collections;

public class InfoController : MonoBehaviour
{
    public GameObject painelInfo;
    public GameObject painelDescricao;
    public GameObject painelInicial;

    public Animator animatorDescricao;
    public Animator animatorInfo;
    public Animator animatorPainelInicial;

    private bool transicaoEmAndamento;
    private bool descricaoEstavaAtiva;
    private bool painelInicialEstavaAtivo;

    public void abrirPainel()
    {
        if (transicaoEmAndamento || painelInfo.activeSelf)
            return;

        StartCoroutine(AbrirPainelCoroutine());
    }

    public void fecharPainel()
    {
        if (transicaoEmAndamento || !painelInfo.activeSelf)
            return;

        StartCoroutine(FecharPainelCoroutine());
    }

    private IEnumerator AbrirPainelCoroutine()
    {
        transicaoEmAndamento = true;

        painelInicialEstavaAtivo = painelInicial.activeSelf;
        descricaoEstavaAtiva = painelDescricao.activeSelf;

        if (painelInicialEstavaAtivo)
        {
            animatorPainelInicial.Play(
                "SaidaMoldura", 0, 0f
            );

            yield return null;

            float duracao = animatorPainelInicial
                .GetCurrentAnimatorStateInfo(0).length;

            yield return new WaitForSeconds(duracao);

            painelInicial.SetActive(false);
        }

        if (descricaoEstavaAtiva)
        {
            animatorDescricao.Play("Saida_UIDescription", 0, 0f);

            yield return null;

            yield return new WaitForSeconds(0.65f);

            painelDescricao.SetActive(false);
        }

        painelInfo.SetActive(true);

        transicaoEmAndamento = false;
    }

    private IEnumerator FecharPainelCoroutine()
    {
        transicaoEmAndamento = true;

        animatorInfo.Play("SaidaAnimacao_Info", 0, 0f);

        yield return null;

        float duracaoInfo = animatorInfo
            .GetCurrentAnimatorStateInfo(0).length;

        yield return new WaitForSeconds(duracaoInfo);

        painelInfo.SetActive(false);

        if (painelInicialEstavaAtivo)
        {
            painelInicial.SetActive(true);

            animatorPainelInicial.Play(
                "EntradaMoldura", 0, 0f
            );
        }

        if (descricaoEstavaAtiva)
        {
            painelDescricao.SetActive(true);

            animatorDescricao.Play(
                "Entrada_UIDescription", 0, 0f
            );
        }

        transicaoEmAndamento = false;
    }
}
