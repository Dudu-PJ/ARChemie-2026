using UnityEngine;
using TMPro;
using Unity.InferenceEngine.Tokenization.PreTokenizers;
using System;

public class DescriptionUI : MonoBehaviour
{

// GameObject do painel da descrição
public GameObject painelDescricao;

// TextMeshPro UI
public TextMeshProUGUI textoDescricao;
public TextMeshProUGUI formulaComposto;
public TextMeshProUGUI nomeComposto;

// Animação

public Animator animator;
    
public void MostrarDescricao(String molecula){

    if (string.IsNullOrEmpty(molecula))
        return;

    if (animator != null)
    {
    animator.Play("Entrada_UI", 0, 0f);
    }
    
    painelDescricao.SetActive(true);

    switch (molecula)
    {

        case "metano":
        nomeComposto.text = "METANO";
        formulaComposto.text = "CH<sub>4</sub>";
        textoDescricao.text = "O metano é um hidrocarboneto gasoso, inodoro e incolor, presente no gás natural e amplamente utilizado como combustível.";
        break;

        case "etano": 
        nomeComposto.text = "ETANO";
        formulaComposto.text = "C<sub>2</sub>H<sub>6</sub>";
        textoDescricao.text = "O etano é um hidrocarboneto presente no gás natural, utilizado para gerar energia e como matéria-prima na indústria química.";

        break;

        case "eteno":
        nomeComposto.text = "ETENO";
        formulaComposto.text = "C<sub>2</sub>H<sub>4</sub>";
        textoDescricao.text = "O eteno, também conhecido como etileno, é um hidrocarboneto gasoso utilizado na produção de plásticos, solventes e como hormônio vegetal.";

        break;

        default:

        break;
    }


}
}
