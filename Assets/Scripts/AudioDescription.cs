using UnityEngine;

public class AudioDescription : MonoBehaviour
{


public AudioSource audioSource;

// Insere os arquivos de áudio em uma variável.
public AudioClip metano_descricao;
public AudioClip metanol_descricao;
public AudioClip etano_descricao;
public AudioClip eteno_descricao;
public AudioClip etanol_descricao;

// Obtém o respectivo áudio a partir da molécula detectada no Detector.cs e aponta o clipe correto.
    public void ReproduzirDescricao(string molecula){
        switch (molecula)
        {
            case "metano":
            audioSource.clip = metano_descricao;
            break;

            case "etano":
            audioSource.clip = etano_descricao;
            break;

            case "metanol":
            audioSource.clip = metanol_descricao;
            break;

            case "eteno":
            audioSource.clip = eteno_descricao;
            break;

            case "etanol":
            audioSource.clip = etanol_descricao;
            break;

            default:
            return;
        }

// Roda o áudio.
        audioSource.Play();
    }
}
