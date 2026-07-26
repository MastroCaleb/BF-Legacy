using UnityEngine;
using UnityEngine.UI;

public class PlaySoundButtonHelper : MonoBehaviour
{
    public AudioClip sound;
    public bool stopMusic = false;
    Button button;

    void Start()
    {
        button = GetComponent<Button>();
        button.onClick.AddListener(Play);
    }

    void Play()
    {
        if(stopMusic) SoundManager.Instance.StopMusic();
        SoundManager.Instance.PlaySound(sound);
    }
}
