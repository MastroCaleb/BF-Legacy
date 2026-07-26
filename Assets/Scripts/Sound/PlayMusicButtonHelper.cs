using UnityEngine;
using UnityEngine.UI;

public class PlayMusicButtonHelper : MonoBehaviour
{
    public AudioClip music;
    Button button;

    void Start()
    {
        button = GetComponent<Button>();
        button.onClick.AddListener(Play);
    }

    void Play()
    {
        SoundManager.Instance.PlayMusicLoop(music);
    }
}
