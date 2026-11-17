using UnityEngine;

public static class Moleculas
{
    private static GameObject ancoraAtual;
    private static GameObject modeloAtual;

    public static void Registrar(GameObject ancora, GameObject modelo)
    {
        Remover();
        ancoraAtual = ancora;
        modeloAtual = modelo;
        Debug.Log("Moleculas: nova molécula registrada");
    }

    public static void Remover()
    {
        if (ancoraAtual == null && modeloAtual == null)
        {
            Debug.Log("Moleculas: nenhuma molécula registrada para remover");
            return;
        }

        Debug.Log("Moleculas: removendo molécula anterior");

        if (modeloAtual != null) Object.Destroy(modeloAtual);
        if (ancoraAtual != null) Object.Destroy(ancoraAtual);

        modeloAtual = null;
        ancoraAtual = null;
    }
}