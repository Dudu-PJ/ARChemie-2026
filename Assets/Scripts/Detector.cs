using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
using Unity.InferenceEngine;
using System.Linq;

//Info de elemento
public struct AtomDetection
{
    public string elemento;
    public Rect screenRect;
    public float confianca;
}

public class Detector : MonoBehaviour
{
    // YOLO e Sentis
    public ModelAsset modeloYoloAsset;
    private Model modelo;
    private Worker worker;

    // AR
    public ARCameraManager arCameraManager;
    public ARAnchorManager arAnchorManager;
    public ARPlaneManager arPlaneManager;
    public Camera arCamera;
    private ARRaycastManager raycastManager; // cacheado uma vez no Start, em vez de FindObjectOfType a cada instância

    // Toque duplo (mantido caso queira voltar a exigir toque para instanciar)
    float ultimoToqueTempo = 0;
    float duploToqueMaxTempo = 0.3f;

    // Frame
    private Texture2D texFrame;

    // Parâmetros
    public int dimensao = 416;
    public float confiancaMin = 0.5f;
    public float iouMin = 0.45f;
    public string[] nomes;

    // Modelo 3D
    public GameObject[] prefabsMoleculas;
    private Dictionary<string, GameObject> prefabDict;
    private GameObject moleculaAtual;

    private bool vddMolecula = false;
    private List<AtomDetection> ultimasDeteccoes = new List<AtomDetection>();
    private string ultimaMoleculaDetectada;
    private string moleculaInstanciadaAtual;

    public float intervaloDeteccao = 1f;
    private float tempoDesdeUltimaDeteccao = 0f;

    // Lista de moléculas orgâncias
    private static readonly Dictionary<(int C, int H, int O), string> tabelaMoleculas = new Dictionary<(int, int, int), string> {
        { (1, 4, 0), "metano" },
        { (2, 6, 0), "etano" },
        { (2, 4, 0), "eteno" },
        { (1, 4, 1), "metanol" },
        { (2, 6, 1), "etanol" },
        { (1, 2, 2), "acidoformico" },
        { (2, 4, 2), "acidoacetico" },
        { (3, 6, 1), "propanona" },
        { (1, 2, 1), "metanal" },
        { (4, 10, 1), "eterdietilico" },
        { (3, 6, 2), "metanoatodeetila" },
        { (2, 4, 1), "eteno" },
        };

    void Start()
    {
        prefabDict = new Dictionary<string, GameObject>();
        foreach (var prefab in prefabsMoleculas)
            prefabDict[prefab.name] = prefab;

        modelo = ModelLoader.Load(modeloYoloAsset);
        worker = new Worker(modelo, BackendType.GPUCompute);

        nomes = new string[] { "carbono", "hidrogenio" };

        raycastManager = FindObjectOfType<ARRaycastManager>();
        if (raycastManager == null)
            Debug.LogWarning("ARRaycastManager não encontrado na cena.");

        Debug.Log("Modelo carregado");
    }

    void Update()
    {
        tempoDesdeUltimaDeteccao += Time.deltaTime;
        if (tempoDesdeUltimaDeteccao >= intervaloDeteccao)
        {
            tempoDesdeUltimaDeteccao = 0f;
            CapturaFrame();
        }

        // Só instancia quando a molécula detectada MUDA (ou aparece pela primeira vez),
        // em vez de destruir/recriar o objeto a cada frame enquanto vddMolecula for true.
        if (vddMolecula && ultimaMoleculaDetectada != moleculaInstanciadaAtual)
        {
            InstanciaMolecula(ultimasDeteccoes, ultimaMoleculaDetectada);
            moleculaInstanciadaAtual = ultimaMoleculaDetectada;
        }
        else if (!vddMolecula)
        {
            moleculaInstanciadaAtual = null;
        }

        /*
        if (vddMolecula && Input.touchCount == 1)
        {
            Touch toque = Input.GetTouch(0);
            if (toque.phase == TouchPhase.Began)
            {
                if (Time.time - ultimoToqueTempo <= duploToqueMaxTempo)
                {
                    Debug.Log("Toque duplo detectado");
                    ultimoToqueTempo = 0;
                    InstanciaMolecula(ultimasDeteccoes, ultimaMoleculaDetectada);
                    moleculaInstanciadaAtual = ultimaMoleculaDetectada;
                }
                else
                {
                    ultimoToqueTempo = Time.time;
                }
            }
        }
        */
    }

    private void CapturaFrame()
    {
        if (!arCameraManager.TryAcquireLatestCpuImage(out XRCpuImage cpuImage)) return;
        RodaInferencia(cpuImage);
    }

    private void RodaInferencia(XRCpuImage cpuImage)
    {
        Texture2D inputTex = ConverteFrame(cpuImage);
        cpuImage.Dispose();

        using Tensor<float> inputTensor = TexturaPraTensor(inputTex);
        Destroy(inputTex);

        worker.Schedule(inputTensor);

        using Tensor<float> outputTensor = worker.PeekOutput() as Tensor<float>;
        if (outputTensor == null)
        {
            Debug.LogWarning("Saída do worker não é um Tensor<float> válido.");
            vddMolecula = false;
            return;
        }

        using Tensor<float> cpuOutput = outputTensor.ReadbackAndClone();

        List<AtomDetection> deteccoes = DecodeYoloOutput(cpuOutput);
        Debug.Log($"Total de detecções: {deteccoes.Count}");

        string molecula = IdentificaMolecula(deteccoes);
        ultimasDeteccoes = deteccoes;
        ultimaMoleculaDetectada = molecula;

        if (molecula != null)
        {
            Handheld.Vibrate();
            Debug.Log($"Molécula detectada: {molecula}");
            vddMolecula = true;
        }
        else
        {
            vddMolecula = false;
        }
    }

    private Texture2D ConverteFrame(XRCpuImage cpuImage)
    {
        // Converte no tamanho NATIVO da imagem
        var conversionParams = new XRCpuImage.ConversionParams
        {
            inputRect = new RectInt(0, 0, cpuImage.width, cpuImage.height),
            outputDimensions = new Vector2Int(cpuImage.width, cpuImage.height),
            outputFormat = TextureFormat.RGBA32,
            transformation = XRCpuImage.Transformation.MirrorY
        };

        var texNativa = new Texture2D(cpuImage.width, cpuImage.height, TextureFormat.RGBA32, false);
        cpuImage.Convert(conversionParams, texNativa.GetRawTextureData<byte>());
        texNativa.Apply();

        // Redimensiona pra 416x416 via GPU
        var rt = RenderTexture.GetTemporary(dimensao, dimensao, 0, RenderTextureFormat.ARGB32);
        Graphics.Blit(texNativa, rt);
        Destroy(texNativa);

        var texFinal = new Texture2D(dimensao, dimensao, TextureFormat.RGBA32, false);
        var rtAtivaAnterior = RenderTexture.active;
        RenderTexture.active = rt;
        texFinal.ReadPixels(new Rect(0, 0, dimensao, dimensao), 0, 0);
        texFinal.Apply();
        RenderTexture.active = rtAtivaAnterior;

        RenderTexture.ReleaseTemporary(rt);

        return texFinal;
    }

    private Tensor<float> TexturaPraTensor(Texture2D tex)
    {
        Color32[] pixels = tex.GetPixels32();
        int total = dimensao * dimensao;
        var data = new float[1 * 3 * dimensao * dimensao];

        for (int y = 0; y < dimensao; y++)
        {
            for (int x = 0; x < dimensao; x++)
            {
                int idx = y * dimensao + x;

                data[0 * total + idx] = pixels[idx].r / 255f;
                data[1 * total + idx] = pixels[idx].g / 255f;
                data[2 * total + idx] = pixels[idx].b / 255f;
            }
        }

        return new Tensor<float>(new TensorShape(1, 3, dimensao, dimensao), data);
    }

    // Decodifica a saída do YOLO (formato [1, 4+numClasses, numAnchors]) em detecções:
    // extrai caixa + melhor classe por âncora, filtra por confiancaMin e aplica NMS por classe.
    private List<AtomDetection> DecodeYoloOutput(Tensor<float> output)
    {
        var deteccoes = new List<AtomDetection>();
        int numClasses = output.shape[1] - 4;
        int numAnchors = output.shape[2];

        var raw = new List<(Rect rect, int classIdx, float conf)>();

        for (int a = 0; a < numAnchors; a++)
        {
            float cx = output[0, 0, a];
            float cy = output[0, 1, a];
            float w = output[0, 2, a];
            float h = output[0, 3, a];

            int melhorClasse = -1;
            float melhorConf = 0f;
            for (int c = 0; c < numClasses; c++)
            {
                float score = output[0, 4 + c, a];
                if (score > melhorConf)
                {
                    melhorConf = score;
                    melhorClasse = c;
                }
            }

            if (melhorClasse < 0 || melhorConf < confiancaMin) continue;
            if (melhorClasse >= nomes.Length) continue; // classe sem nome mapeado em `nomes`

            // Converte de coordenadas de pixel (espaço 'dimensao') para normalizadas (0-1)
            float xMin = (cx - w / 2f) / dimensao;
            float yMin = (cy - h / 2f) / dimensao;
            float largura = w / dimensao;
            float altura = h / dimensao;

            raw.Add((new Rect(xMin, yMin, largura, altura), melhorClasse, melhorConf));
        }

        foreach (var group in raw.GroupBy(r => r.classIdx))
        {
            var sorted = group.OrderByDescending(r => r.conf).ToList();
            var keep = new List<(Rect rect, int classIdx, float conf)>();

            while (sorted.Count > 0)
            {
                var best = sorted[0];
                keep.Add(best);
                sorted.RemoveAt(0);
                sorted.RemoveAll(r => IoU(r.rect, best.rect) > iouMin);
            }

            foreach (var det in keep)
            {
                deteccoes.Add(new AtomDetection
                {
                    elemento = nomes[det.classIdx],
                    screenRect = det.rect,
                    confianca = det.conf
                });
            }
        }

        return deteccoes;
    }

    private float IoU(Rect a, Rect b)
    {
        float x1 = Mathf.Max(a.xMin, b.xMin);
        float y1 = Mathf.Max(a.yMin, b.yMin);
        float x2 = Mathf.Min(a.xMax, b.xMax);
        float y2 = Mathf.Min(a.yMax, b.yMax);

        float interArea = Mathf.Max(0, x2 - x1) * Mathf.Max(0, y2 - y1);
        float unionArea = a.width * a.height + b.width * b.height - interArea;

        return unionArea <= 0 ? 0 : interArea / unionArea;
    }

    private string IdentificaMolecula(List<AtomDetection> deteccoes)
    {
        int C = 0, H = 0, O = 0;
        foreach (var det in deteccoes)
        {
            if (det.elemento == "carbono") C++;
            else if (det.elemento == "hidrogenio") H++;
            else if (det.elemento == "oxigenio") O++;
        }

        Debug.Log($"Átomos detectados: C={C} H={H} O={O}");

        var chave = (C, H, O);
        if (tabelaMoleculas.TryGetValue(chave, out string nome))
            return nome;

        return null;
    }

    private void InstanciaMolecula(List<AtomDetection> deteccoes, string nomeMolecula)
    {
        if (deteccoes == null || deteccoes.Count == 0)
        {
            Debug.LogWarning("Nenhuma detecção disponível para posicionar a molécula.");
            return;
        }

        if (!prefabDict.TryGetValue(nomeMolecula, out GameObject prefab))
        {
            Debug.LogWarning($"Prefab não encontrado: {nomeMolecula}");
            return;
        }

        if (raycastManager == null)
        {
            Debug.LogWarning("ARRaycastManager não encontrado");
            return;
        }

        // Centro médio dos bounding boxes
        Vector2 centroTela = Vector2.zero;
        foreach (var det in deteccoes)
            centroTela += new Vector2(
                det.screenRect.center.x * Screen.width,
                det.screenRect.center.y * Screen.height
            );
        centroTela /= deteccoes.Count;

        var hits = new List<ARRaycastHit>();
        if (!raycastManager.Raycast(centroTela, hits, TrackableType.PlaneWithinPolygon))
        {
            Debug.Log("Raycast não acertou nenhum plano");
            return;
        }

        if (moleculaAtual != null)
            Destroy(moleculaAtual);

        var hit = hits[0];
        moleculaAtual = Instantiate(prefab, hit.pose.position, hit.pose.rotation);
        Debug.Log($"Molécula instanciada: {nomeMolecula}");
    }

    void OnDestroy()
    {
        worker?.Dispose();
    }
}
