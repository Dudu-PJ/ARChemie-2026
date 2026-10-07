using UnityEngine;
using UnityEngine.UI;

public class QuadradoGuia : MonoBehaviour
{
    public Color cor = new Color(1f, 1f, 1f, 0.9f);
    public int espessura = 4;

    void Start()
    {
        var canvasRect = GetComponent<RectTransform>();

        var go = new GameObject("QuadradoDeteccao", typeof(RectTransform), typeof(Image));
        go.transform.SetParent(transform, false);

        var rt = (RectTransform)go.transform;
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = Vector2.zero;
        float lado = canvasRect.rect.width;
        rt.sizeDelta = new Vector2(lado, lado);

        const int tam = 16;
        var tex = new Texture2D(tam, tam, TextureFormat.RGBA32, false);
        var pix = new Color32[tam * tam];
        for (int i = 0; i < pix.Length; i++) pix[i] = new Color32(255, 255, 255, 255);
        tex.SetPixels32(pix);
        tex.Apply();

        var sprite = Sprite.Create(tex, new Rect(0, 0, tam, tam), new Vector2(0.5f, 0.5f),
                                   100f, 0, SpriteMeshType.FullRect,
                                   new Vector4(espessura, espessura, espessura, espessura));

        var img = go.GetComponent<Image>();
        img.sprite = sprite;
        img.type = Image.Type.Sliced;
        img.fillCenter = false;
        img.color = cor;
        img.raycastTarget = false;
    }
}