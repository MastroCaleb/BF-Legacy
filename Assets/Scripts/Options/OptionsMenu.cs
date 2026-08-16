using UnityEngine;
using UnityEngine.UI;

public class OptionsMenu : MonoBehaviour
{
    public Slider musicSlider;
    public Slider soundSlider;

    void OnEnable()
    {
        musicSlider.SetValueWithoutNotify(Options.Instance.GetMusicVolume());
        soundSlider.SetValueWithoutNotify(Options.Instance.GetSoundVolume());

        musicSlider.onValueChanged.AddListener(Options.Instance.SetMusicVolume);
        soundSlider.onValueChanged.AddListener(Options.Instance.SetSoundVolume);
    }

    void OnDisable()
    {
        musicSlider.onValueChanged.RemoveListener(Options.Instance.SetMusicVolume);
        soundSlider.onValueChanged.RemoveListener(Options.Instance.SetSoundVolume);

        Options.Instance.SaveToJson();
    }
}