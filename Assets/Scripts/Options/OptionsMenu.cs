using UnityEngine;
using UnityEngine.UI;

public class OptionsMenu : MonoBehaviour
{
    public Slider musicSlider;
    public Slider soundSlider;
    public Toggle vfxToggle;

    void OnEnable()
    {
        musicSlider.SetValueWithoutNotify(Options.Instance.GetMusicVolume());
        soundSlider.SetValueWithoutNotify(Options.Instance.GetSoundVolume());
        vfxToggle.SetIsOnWithoutNotify(Options.Instance.GetVfxEnabled());

        musicSlider.onValueChanged.AddListener(Options.Instance.SetMusicVolume);
        soundSlider.onValueChanged.AddListener(Options.Instance.SetSoundVolume);
        vfxToggle.onValueChanged.AddListener(Options.Instance.SetVfxEnabled);
    }

    void OnDisable()
    {
        musicSlider.onValueChanged.RemoveListener(Options.Instance.SetMusicVolume);
        soundSlider.onValueChanged.RemoveListener(Options.Instance.SetSoundVolume);

        Options.Instance.SaveToJson();
    }
}