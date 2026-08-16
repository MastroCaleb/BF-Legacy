using UnityEngine;

public class SoundManager : MonoBehaviour
{
    public static SoundManager Instance;

    private AudioSource musicSource;
    private AudioSource loopingSource;

    private static float musicVolume;
    private static float soundVolume;

    [Header("SFX")]
    public int sfxPoolSize = 10;

    private AudioSource[] sfxSources;

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);

            // Create the music source
            musicSource = gameObject.AddComponent<AudioSource>();
            musicSource.playOnAwake = false;
            musicSource.loop = true;
            musicSource.spatialBlend = 0f; // 2D audio
            musicSource.volume = musicVolume;

            // Create the SFX pool
            sfxSources = new AudioSource[sfxPoolSize];

            for (int i = 0; i < sfxPoolSize; i++)
            {
                sfxSources[i] = gameObject.AddComponent<AudioSource>();
                sfxSources[i].playOnAwake = false;
                sfxSources[i].spatialBlend = 0f; // 2D audio
                sfxSources[i].volume = soundVolume;
            }
        }
        else
        {
            Destroy(gameObject);
        }
    }

    public void PlayMusicLoop(AudioClip music)
    {
        if (music == null)
            return;

        if (musicSource.clip == music && musicSource.isPlaying)
            return;

        musicSource.clip = music;
        musicSource.volume = Options.Instance.GetMusicVolume();
        musicSource.Play();
    }

    public void SetMusicVolume(float volume)
    {
        musicSource.volume = volume;
    }

    public void StopMusic()
    {
        musicSource.Stop();
    }

    public void PlaySound(AudioClip clip)
    {
        foreach (AudioSource source in sfxSources)
        {
            if (!source.isPlaying)
            {
                source.PlayOneShot(clip, Options.Instance.GetSoundVolume());
                return;
            }
        }

        // If they're all busy, reuse the first one.
        sfxSources[0].PlayOneShot(clip, Options.Instance.GetSoundVolume());
    }

    public void PlayLoopingSound(AudioClip clip)
    {
        if (loopingSource == null)
        {
            loopingSource = gameObject.AddComponent<AudioSource>();
            loopingSource.playOnAwake = false;
            loopingSource.spatialBlend = 0f;
            loopingSource.loop = true;
        }

        loopingSource.clip = clip;
        loopingSource.volume = Options.Instance.GetSoundVolume();
        loopingSource.Play();
    }

    public void StopLoopingSound()
    {
        if (loopingSource != null && loopingSource.isPlaying)
            loopingSource.Stop();
    }

    public void SetSoundVolume(float volume)
    {
        soundVolume = volume;

        foreach (AudioSource source in sfxSources)
        {
            source.volume = soundVolume;
        }

        if (loopingSource != null)
            loopingSource.volume = soundVolume;
    }
}