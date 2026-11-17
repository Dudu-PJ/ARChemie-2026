using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

public class Emergencia : MonoBehaviour
{
    public GameObject emergenciaButton;
    public GameObject botao1;
    public GameObject botao2;
    public GameObject botao3;

    public GameObject modelo1;
    public GameObject modelo2;
    public GameObject modelo3;

    [Header("AR Foundation")]
    public ARRaycastManager raycastManager;
    public ARAnchorManager anchorManager;

    [Header("Fluxo padrão")]
    public Detector detector;

    private static readonly List<ARRaycastHit> hits = new List<ARRaycastHit>();
    private ARAnchor ancoraAtual;
    private bool colocando;

    public void BotaoEmergencia()
    {
        emergenciaButton.SetActive(false);
        botao1.SetActive(true);
        botao2.SetActive(true);
        botao3.SetActive(true);
    }

    public void Botao1() => Instanciar(modelo1);
    public void Botao2() => Instanciar(modelo2);
    public void Botao3() => Instanciar(modelo3);

    public void RemoverMolecula()
    {
        if (ancoraAtual == null) return;

        Debug.Log("Removendo molécula de emergência");
        Destroy(ancoraAtual.gameObject);
        ancoraAtual = null;
    }

    private async void Instanciar(GameObject prefab)
    {
        if (colocando) return;

        Vector2 centroTela = new Vector2(Screen.width / 2f, Screen.height / 2f);

        if (!raycastManager.Raycast(centroTela, hits, TrackableType.PlaneWithinPolygon))
        {
            Debug.Log("Nenhuma superfície detectada no centro da tela");
            return;
        }

        Pose pose = hits[0].pose;

        colocando = true;
        try
        {
            var resultado = await anchorManager.TryAddAnchorAsync(pose);

            if (!resultado.status.IsSuccess())
            {
                Debug.LogWarning("Falha ao criar a âncora: " + resultado.status);
                return;
            }

            ARAnchor novaAncora = resultado.value;

            GameObject modelo = Instantiate(prefab, novaAncora.transform);
            Moleculas.Registrar(novaAncora.gameObject, modelo);

            emergenciaButton.SetActive(true);
            botao1.SetActive(false);
            botao2.SetActive(false);
            botao3.SetActive(false);
        }
        finally
        {
            colocando = false;
        }
    }
}