using System.Collections.Generic;
using Unity.Collections;
using System.Threading.Tasks;
using System.Linq;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
using Unity.InferenceEngine;
using TMPro;

//Info de elemento
public struct AtomDetection
{
    public string elemento;
    public Rect screenRect;
}

public class Detector : MonoBehaviour
{
    // YOLO e Sentis
    public ModelAsset modeloYoloAsset;
    private Model modelo;
    private Worker worker;
    private bool modeloPronto = false;

    // AR
    public ARCameraManager arCameraManager;
    public ARAnchorManager arAnchorManager;
    public ARPlaneManager arPlaneManager;
    private ARRaycastManager raycastManager;

    // Toque duplo
    private float ultimoToqueTempo = -999f;
    private float duploToqueMaxTempo = 0.3f;

    // Parametros
    private int dimensao = 416;
    public float confiancaMin = 0.5f;
    public float iouMin = 0.45f;
    private string[] nomes;
    private float[] bufferTensor;
    private Texture2D texEntrada;

    // Modelo 3D
    public GameObject[] prefabsMoleculas;
    private Dictionary<string, GameObject> prefabDict;
    private GameObject moleculaAtual;
    private ARAnchor anchorAtual;

    // Deteccao
    public float intervaloDeteccao = 1f;
    private float tempoUltimaDeteccaoConcluida = -999f;
    private bool processando = false;

    // UI
    public DescriptionUI descriptionUI;

    // Audio
    public AudioDescription audioDescription;

    // Movimentos por toque
    public MovToques movToques;


    public DebugDeteccao debug;


    // Lista de moleculas organicas
    private static readonly Dictionary<(int C, int H), string> tabelaMoleculas = new Dictionary<(int, int), string> {
        { (1, 4), "metano" },
        { (2, 4), "eteno" },
        { (2, 6), "etano" }}; //Teria ligação, mas o reconhecimento de ligação está deficiente, então vou fingir que não existe por agora

    void Start()
    {
        if (movToques == null) movToques = GetComponent<MovToques>();

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
            Debug.LogWarning("ARRaycastManager não encontrado na cena");

        _ = AquecerModelo();

        Debug.Log("Modelo carregado");
    }

    private async Task AquecerModelo()
    {
        try
        {
            using var dummy = new Tensor<float>(new TensorShape(1, 3, dimensao, dimensao));
            worker.Schedule(dummy);
            var saida = worker.PeekOutput() as Tensor<float>;
            if (saida != null)
            {
                using var _ = await saida.ReadbackAndCloneAsync();
            }

            Debug.Log("Aquecimento do modelo concluído");
            modeloPronto = true;
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Falha no aquecimento do modelo: {e}");
        }
    }

    void Update()
    {
        if (moleculaAtual != null)
        {
            movToques.MovPinca(moleculaAtual);
            movToques.MovGiro(moleculaAtual);
        }

        bool toqueDuplo = DetectaToqueDuplo();
        bool nCooldown = Time.time - tempoUltimaDeteccaoConcluida >= intervaloDeteccao;

        if (toqueDuplo && modeloPronto && !processando && nCooldown)
        {
            if (!arCameraManager.TryAcquireLatestCpuImage(out XRCpuImage cpuImage)) return;
            processando = true;
            RodaInferencia(cpuImage);
        }
    }

    private bool DetectaToqueDuplo()
    {
        if (Input.touchCount > 1) return false;

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
        ultimoToqueTempo = duplo ? -999f : agora;
        return duplo;
    }

    private async void RodaInferencia(XRCpuImage cpuImage)
    {
        try
        {
            Texture2D inputTex;
            try
            {
                inputTex = ConverteFrame(cpuImage);
            }
            finally
            {
                cpuImage.Dispose();
            }

            using Tensor<float> inputTensor = TexturaPraTensor(inputTex);

            worker.Schedule(inputTensor);

            var outputTensor = worker.PeekOutput() as Tensor<float>;
            if (outputTensor == null)
            {
                Debug.LogWarning("Saída do worker não é um Tensor<float> válido!");
                return;
            }

            using Tensor<float> cpuOutput = await outputTensor.ReadbackAndCloneAsync();

            List<AtomDetection> deteccoes = DecodeYoloOutput(cpuOutput);

            if (debug != null)
                debug.MostrarMarcadores(deteccoes.Select(d => QuadradoParaTela(d.screenRect.center)).ToList());

            string molecula = IdentificaMolecula(deteccoes);

            if (molecula != null)
            {
                Handheld.Vibrate();
                InstanciaMolecula(deteccoes, molecula);
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Erro na inferência: {e}");
        }
        finally
        {
            processando = false;
            tempoUltimaDeteccaoConcluida = Time.time;
        }
    }

    private int CalculaLadoCrop(int imgW, int imgH)
    {
        float menor = Mathf.Min(imgW, imgH);
        float maior = Mathf.Max(imgW, imgH);
        float escalaTela = Mathf.Max(Screen.width / menor, Screen.height / maior);
        return Mathf.Min((int)menor, Mathf.RoundToInt(Screen.width / escalaTela));
    }

    private Texture2D ConverteFrame(XRCpuImage cpuImage)
    {
        int lado = CalculaLadoCrop(cpuImage.width, cpuImage.height);
        int x0 = (cpuImage.width - lado) / 2;
        int y0 = (cpuImage.height - lado) / 2;

        int saida = Mathf.Min(lado, dimensao);

        var conversionParams = new XRCpuImage.ConversionParams
        {
            inputRect = new RectInt(x0, y0, lado, lado),
            outputDimensions = new Vector2Int(saida, saida),
            outputFormat = TextureFormat.RGBA32,
            transformation = XRCpuImage.Transformation.MirrorY
        };

        if (texEntrada == null || texEntrada.width != saida)
        {
            if (texEntrada != null) Destroy(texEntrada);
            texEntrada = new Texture2D(saida, saida, TextureFormat.RGBA32, false);
        }

        cpuImage.Convert(conversionParams, texEntrada.GetRawTextureData<byte>());
        return texEntrada;
    }

    private Tensor<float> TexturaPraTensor(Texture2D tex)
    {
        Color32[] pixels = tex.GetPixels32();
        int src = tex.width;
        int total = dimensao * dimensao;
        if (bufferTensor == null) bufferTensor = new float[3 * total];

        for (int y = 0; y < dimensao; y++)
        {
            for (int x = 0; x < dimensao; x++)
            {
                int idxTensor = y * dimensao + x;

                int oy = (dimensao - 1 - y) * src / dimensao;
                int ox = (dimensao - 1 - x) * src / dimensao;
                int idxPixel = oy * src + ox;

                bufferTensor[idxTensor] = pixels[idxPixel].r / 255f;
                bufferTensor[total + idxTensor] = pixels[idxPixel].g / 255f;
                bufferTensor[2 * total + idxTensor] = pixels[idxPixel].b / 255f;
            }
        }

        return new Tensor<float>(new TensorShape(1, 3, dimensao, dimensao), bufferTensor);
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

        var sorted = raw.OrderByDescending(r => r.conf).ToList();

        while (sorted.Count > 0)
        {
            var best = sorted[0];
            sorted.RemoveAt(0);

            deteccoes.Add(new AtomDetection
            {
                elemento = nomes[best.classIdx],
                screenRect = best.rect,
            });

            sorted.RemoveAll(r => r.classIdx == best.classIdx && IoU(r.rect, best.rect) > iouMin);
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
        int C = 0, H = 0;
        foreach (var det in deteccoes)
        {
            if (det.elemento == "carbono") C++;
            else if (det.elemento == "hidrogenio") H++;
        }

        Debug.Log($"Elementos detectados: C={C} H={H}");

        var chave = (C, H);
        if (tabelaMoleculas.TryGetValue(chave, out string nome))
            return nome;

        return null;
    }

    private Vector2 QuadradoParaTela(Vector2 n)
    {
        float ladoTela = Screen.width;
        float x = Screen.width / 2f + (n.x - 0.5f) * ladoTela;
        float y = Screen.height / 2f + (n.y - 0.5f) * ladoTela;
        return new Vector2(x, Screen.height - y);
    }

    private bool InstanciaMolecula(List<AtomDetection> deteccoes, string nomeMolecula)
    {
        if (deteccoes == null || deteccoes.Count == 0)
        {
            Debug.LogWarning("Nenhuma detecção disponível para posicionar a molécula");
            return false;
        }

        if (!prefabDict.TryGetValue(nomeMolecula, out GameObject prefab))
        {
            Debug.LogWarning($"Prefab não encontrado: {nomeMolecula}");
            return false;
        }

        if (raycastManager == null)
        {
            Debug.LogWarning("ARRaycastManager não encontrado");
            return false;
        }

        Vector2 centroTela = Vector2.zero;
        foreach (var det in deteccoes)
            centroTela += QuadradoParaTela(det.screenRect.center);
        centroTela /= deteccoes.Count;

        var hits = new List<ARRaycastHit>();
        if (!raycastManager.Raycast(centroTela, hits, TrackableType.PlaneWithinPolygon))
        {
            Debug.Log("Raycast não acertou nenhum plano");
            return false;
        }

        var hit = hits[0];

        var plano = arPlaneManager.GetPlane(hit.trackableId);
        if (plano == null) { Debug.LogWarning("Plano não encontrado pro hit"); return false; }

        var novaAncora = arAnchorManager.AttachAnchor(plano, hit.pose);
        if (novaAncora == null) { Debug.LogWarning("Falha ao criar âncora"); return false; }

        if (anchorAtual != null) Destroy(anchorAtual.gameObject);
        anchorAtual = novaAncora;

        moleculaAtual = Instantiate(prefab, anchorAtual.transform);
        moleculaAtual.transform.localPosition = Vector3.zero;
        moleculaAtual.transform.localRotation = Quaternion.identity;

        if (audioDescription != null)
            audioDescription.ReproduzirDescricao(nomeMolecula);

        Debug.Log($"Molécula instanciada: {nomeMolecula}");

        if (descriptionUI != null && !string.IsNullOrEmpty(nomeMolecula))
        {
            descriptionUI.MostrarDescricao(nomeMolecula);
        }

        return true;
    }

    void OnDestroy()
    {
        if (texEntrada != null) Destroy(texEntrada);
        worker?.Dispose();
        if (anchorAtual != null) Destroy(anchorAtual.gameObject);
    }
}
