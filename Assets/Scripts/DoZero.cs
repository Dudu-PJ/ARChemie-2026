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

public class DoZero : MonoBehaviour
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

    // Lista de moléculas orgâncias
    private static readonly Dictionary<(int C, int H, int O), string> tabelaMoleculas = new Dictionary<(int, int, int), string> {
        { (1, 4, 0), "Metano" },
        { (2, 6, 0), "Etano" },
        { (2, 4, 0), "Eteno" },
        { (1, 4, 1), "Metanol" },
        { (2, 6, 1), "Etanol" },
        { (1, 2, 2), "AcidoFormico" },
        { (2, 4, 2), "AcidoAcetico" },
        { (3, 6, 1), "Propanona" },
        { (1, 2, 1), "Metanal" },
        { (4, 10, 1), "EterDietilico" },
        { (3, 6, 2), "MetanoatoDeEtila" },
        { (2, 4, 1), "Etenol" },
        };

    void Start()
    {
        //
        prefabDict = new Dictionary<string, GameObject>();
        foreach (var prefab in prefabsMoleculas)
            prefabDict[prefab.name] = prefab;
        //

        modelo = ModelLoader.Load(modeloYoloAsset);
        worker = new Worker(modelo, BackendType.GPUCompute);

        nomes = new string[] { "carbono", "hidrogenio" };

        Debug.Log("Modelo carregado");
    }

    void Update()
    {
        CapturaFrame();
        if(vddMolecula)
        {
            InstanciaMolecula(ultimasDeteccoes, ultimaMoleculaDetectada);
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
        Debug.Log("Schedule OK");

        using Tensor<float> outputTensor = worker.PeekOutput() as Tensor<float>;
        Debug.Log($"Shape do Tensor: {outputTensor.shape}");

        using Tensor<float> cpuOutput = outputTensor.ReadbackAndClone();

        List<AtomDetection> deteccoes = DecodeYoloOutput(cpuOutput);
        Debug.Log($"Total de detecções: {deteccoes.Count}");

        string molecula = IdentificaMolecula(deteccoes);
        ultimasDeteccoes = deteccoes;
        ultimaMoleculaDetectada = molecula;

        if (molecula != null)
        {
            Handheld.Vibrate();
            Debug.Log("Molecula detectada");
            vddMolecula = true;
        }
        else
        {
            Debug.Log("Nenhuma molécula detectada");
            vddMolecula = false;
        }
    }

    private Texture2D ConverteFrame(XRCpuImage cpuImage)
    {
        var conversionParams = new XRCpuImage.ConversionParams
        {
            inputRect = new RectInt(0, 0, cpuImage.width, cpuImage.height),
            outputDimensions = new Vector2Int(dimensao, dimensao),
            outputFormat = TextureFormat.RGBA32,
            transformation = XRCpuImage.Transformation.MirrorY
        };

        var tex = new Texture2D(dimensao, dimensao, TextureFormat.RGBA32, false);
        cpuImage.Convert(conversionParams, tex.GetRawTextureData<byte>());
        tex.Apply();

        Debug.Log("Frame convertido para textura");
        return tex;
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
                int srcIdx = y * dimensao + x;
                int dstBase = y * dimensao + x;

                data[0 * total + dstBase] = pixels[srcIdx].r / 255f;
                data[1 * total + dstBase] = pixels[srcIdx].g / 255f;
                data[2 * total + dstBase] = pixels[srcIdx].b / 255f;
            }
        }

        Debug.Log("Textura convertida pra Tensor");

        return new Tensor<float>(new TensorShape(1, 3, dimensao, dimensao), data);
    }

    private List<AtomDetection> DecodeYoloOutput(Tensor<float> output)
    {
        var deteccoes = new List<AtomDetection>();
        int numClasses = output.shape[1] - 4;
        int numAnchors = output.shape[2];

        var raw = new List<(Rect rect, int classIdx, float conf)>();

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
        if (!prefabDict.TryGetValue(nomeMolecula, out GameObject prefab))
        {
            Debug.LogWarning($"Prefab não encontrado: {nomeMolecula}");
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

        // Raycast AR
        var hits = new List<ARRaycastHit>();
        if (!arCamera.GetComponent<ARRaycastManager>() &&
            !FindObjectOfType<ARRaycastManager>())
        {
            Debug.LogWarning("ARRaycastManager não encontrado");
            return;
        }

        var raycastManager = FindObjectOfType<ARRaycastManager>();
        if (!raycastManager.Raycast(centroTela, hits, TrackableType.PlaneWithinPolygon))
        {
            Debug.Log("Raycast não acertou nenhum plano");
            return;
        }

        // Destrói molécula anterior e instancia nova
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