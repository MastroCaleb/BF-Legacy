using UnityEngine;
using System.IO;

public class Options : MonoBehaviour
{
    public static Options Instance;

    [Header("Defaults")]
    public float defaultMusicVolume = 0.75f;
    public float defaultSoundVolume = 0.75f;

    private const string SAVE_FILE_NAME = "options.json";

    private OptionsData currentOptions;

    [System.Serializable]
    private class OptionsData
    {
        public float musicVolume;
        public float soundVolume;
    }

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);

            LoadFromJson();
        }
        else
        {
            Destroy(gameObject);
        }
    }

    void Start()
    {
        if (Instance == this)
            ApplyOptions();
    }

    public float GetMusicVolume() => currentOptions.musicVolume;
    public float GetSoundVolume() => currentOptions.soundVolume;

    public void SetMusicVolume(float volume)
    {
        currentOptions.musicVolume = volume;

        if (SoundManager.Instance != null)
            SoundManager.Instance.SetMusicVolume(volume);
    }

    public void SetSoundVolume(float volume)
    {
        currentOptions.soundVolume = volume;

        if (SoundManager.Instance != null)
            SoundManager.Instance.SetSoundVolume(volume);
    }

    private void ApplyOptions()
    {
        if (SoundManager.Instance == null)
            return;

        SoundManager.Instance.SetMusicVolume(currentOptions.musicVolume);
        SoundManager.Instance.SetSoundVolume(currentOptions.soundVolume);
    }

    public void SaveToJson()
    {
        string json = JsonUtility.ToJson(currentOptions, true);
        string path = Path.Combine(Application.persistentDataPath, SAVE_FILE_NAME);

        try
        {
            File.WriteAllText(path, json);
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Failed to save options to {path}: {e.Message}");
        }
    }

    public void LoadFromJson()
    {
        string path = Path.Combine(Application.persistentDataPath, SAVE_FILE_NAME);

        if (File.Exists(path))
        {
            try
            {
                string json = File.ReadAllText(path);
                currentOptions = JsonUtility.FromJson<OptionsData>(json);
                return;
            }
            catch (System.Exception e)
            {
                Debug.LogError($"Failed to load options from {path}, using defaults: {e.Message}");
            }
        }

        // No file, or load failed -> defaults
        currentOptions = new OptionsData
        {
            musicVolume = defaultMusicVolume,
            soundVolume = defaultSoundVolume
        };
    }
}