using UnityEngine;
using System.Collections;

public class InfoController : MonoBehaviour
{
    public GameObject painelInfo;
    public GameObject painelDescricao;
    public Animator animatorDescricao;

    private bool transicaoEmAndamento;
    private bool descricaoEstavaAtiva;

    public void abrirPainel(){
        if (transicaoEmAndamento || painelInfo.activeSelf)
        return;

        StartCoroutine(AbrirPainelCoroutine());
    }

    public void fecharPainel(){
        if (transicaoEmAndamento || !painelInfo.activeSelf)
        return;

        StartCoroutine(FecharPainelCoroutine());
    }

    private IEnumerator AbrirPainelCoroutine(){
        transicaoEmAndamento = true; 

        descricaoEstavaAtiva = painelDescricao.activeSelf;

        if(descricaoEstavaAtiva){
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

        painelInfo.SetActive(false);

        if (descricaoEstavaAtiva)
        {
            painelDescricao.SetActive(true);

            animatorDescricao.Play(
                "Entrada_UIDescription", 0, 0f);

            yield return null;

            yield return new WaitForSeconds(0.65f);
        }

        transicaoEmAndamento = false;
    }

}
