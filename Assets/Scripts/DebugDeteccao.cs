using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class DebugDeteccao : MonoBehaviour
{
    public RectTransform marcadorPrefab;
    public RectTransform canvasRaiz;
    public float duracaoMarcador = 3f;

    public void MostrarMarcadores(List<Vector2> posicoesTela)
    {
        if (marcadorPrefab == null || canvasRaiz == null || posicoesTela == null) return;

        foreach (var p in posicoesTela)
        {
            var m = Instantiate(marcadorPrefab, canvasRaiz);
            m.position = new Vector3(p.x, p.y, 0f);
            Destroy(m.gameObject, duracaoMarcador);
        }
    }
}