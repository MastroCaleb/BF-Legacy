using UnityEngine;

public class SimpleStartMusicHelper : MonoBehaviour
{
    public AudioClip music;

    void Start()
    {
        SoundManager.Instance.PlayMusicLoop(music);
    }
}
