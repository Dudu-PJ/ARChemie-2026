using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
using Unity.InferenceEngine;
using System.Linq;
using TMPro;

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
    private ARRaycastManager raycastManager;

    // Toque duplo
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

    // Deteccao
    public float intervaloDeteccao = 1f;
    private float tempoUltimaDeteccaoConcluida = -999f;
    private bool processando = false;

    // UI
    public TMPro.TextMeshProUGUI textoDeteccao;

    // Lista de moléculas orgâncias
    private static readonly Dictionary<(int C, int H, int L), string> tabelaMoleculas = new Dictionary<(int, int, int), string> {
        { (1, 4, 0), "metano" },
        { (2, 4, 0), "eteno" },
        { (2, 6, 0), "etano" }};

    void Start()
    {
        prefabDict = new Dictionary<string, GameObject>();
        foreach (var prefab in prefabsMoleculas)
            prefabDict[prefab.name] = prefab;

        modelo = ModelLoader.Load(modeloYoloAsset);
        var backend = SystemInfo.supportsComputeShaders ? BackendType.GPUCompute : BackendType.CPU;
        worker = new Worker(modelo, backend);
        Debug.Log($"Backend de inferência: {backend}");

        nomes = new string[] { "carbono", "hidrogenio" };

        raycastManager = FindAnyObjectByType<ARRaycastManager>();
        if (raycastManager == null)
            Debug.LogWarning("ARRaycastManager não encontrado na cena.");

        Debug.Log("Modelo carregado");
    }

    void Update()
    {
        bool toqueDuplo = DetectaToqueDuplo();
        bool nCooldown = Time.time - tempoUltimaDeteccaoConcluida >= intervaloDeteccao;

        if (toqueDuplo && !processando && nCooldown)
        {
            CapturaFrame();
        }

        if (vddMolecula && ultimaMoleculaDetectada != moleculaInstanciadaAtual)
        {
            moleculaInstanciadaAtual = ultimaMoleculaDetectada;
            InstanciaMolecula(ultimasDeteccoes, ultimaMoleculaDetectada);
        }
    }

    private bool DetectaToqueDuplo()
    {
        bool toqueIniciado = false;

        if (Input.touchCount > 0 && Input.GetTouch(0).phase == TouchPhase.Began)
        {
            toqueIniciado = true;
        }
        #if UNITY_EDITOR
        else if (Input.GetMouseButtonDown(0))
        {
            toqueIniciado = true;
        }
        #endif

        if (!toqueIniciado)
            return false;

        float agora = Time.time;
        bool duplo = (agora - ultimoToqueTempo) <= duploToqueMaxTempo;
        ultimoToqueTempo = duplo ? -999f : agora; // evita que um triplo toque conte como dois duplos seguidos
        return duplo;
    }

    private void CapturaFrame()
    {
        if (!arCameraManager.TryAcquireLatestCpuImage(out XRCpuImage cpuImage)) return;
        processando = true;
        RodaInferenciaAsync(cpuImage);
    }

    private async void RodaInferenciaAsync(XRCpuImage cpuImage)
    {
        try
        {
            Texture2D inputTex = ConverteFrame(cpuImage);
            cpuImage.Dispose();

            using Tensor<float> inputTensor = TexturaPraTensor(inputTex);
            Destroy(inputTex);

            worker.Schedule(inputTensor);

            var outputTensor = worker.PeekOutput() as Tensor<float>;
            if (outputTensor == null)
            {
                Debug.LogWarning("Saída do worker não é um Tensor<float> válido.");
                vddMolecula = false;
                return;
            }

            using Tensor<float> cpuOutput = await outputTensor.ReadbackAndCloneAsync();

            List<AtomDetection> deteccoes = DecodeYoloOutput(cpuOutput);
            string molecula = IdentificaMolecula(deteccoes);
            ultimasDeteccoes = deteccoes;
            ultimaMoleculaDetectada = molecula;

            if (molecula != null)
            {
                Handheld.Vibrate();
                vddMolecula = true;
                if (textoDeteccao != null)
                    textoDeteccao.text = $"Molécula detectada: {molecula}";
            }
            else
            {
                vddMolecula = false;
                if (textoDeteccao != null)
                    textoDeteccao.text = "Nenhuma molécula detectada";
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Erro na inferência: {e}");
            vddMolecula = false;
        }
        finally
        {
            processando = false;
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
            if (melhorClasse >= nomes.Length) continue;

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
        int C = 0, H = 0, L = 0;
        foreach (var det in deteccoes)
        {
            if (det.elemento == "carbono") C++;
            else if (det.elemento == "hidrogenio") H++;
            else if (det.elemento == "ligacao") L++;
        }

        Debug.Log($"Elementos detectados: C={C} H={H} L={L}");

        var chave = (C, H, L);
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
