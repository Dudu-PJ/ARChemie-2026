using UnityEngine;
using UnityEngine.UI;

public class ControleSom : MonoBehaviour
{
    private bool som;
    [SerializeField] Image somON;
    [SerializeField] Image somOFF;

    void Start()
    {
        Volume();
    }

    public void Volume()
    {
        som = !som;

        if (som == true)
        {
            AudioListener.volume = 1;

        }
        else
        {
            AudioListener.volume = 0;
        }

        OnOff();
    }

    private void OnOff()
    {
        if (som == false)
        {
            somON.enabled = false;
            somOFF.enabled = true;
        }
        else
        {
            somON.enabled = true;
            somOFF.enabled = false;
        }
    }
}
