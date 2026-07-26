using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class SpeedUpBattleButtonUI : MonoBehaviour
{
    public Button button;
    public TextMeshProUGUI buttonText;
    public bool canBeClicked = true;
    public static float currentSpeed = 1f;

    void Start()
    {
        button = GetComponent<Button>();
        buttonText = GetComponentInChildren<TextMeshProUGUI>();
        if (button != null){
            UpdateButtonVisual();
            button.onClick.AddListener(OnSlotClicked);
        }

        Time.timeScale = currentSpeed;
        UpdateButtonVisual();
    }

    private void OnSlotClicked()
    {
        if(CutInAnimation.isPlaying) return;
        if (canBeClicked)
        {
            currentSpeed = currentSpeed == 1f ? 2f : currentSpeed == 2f ? 3f : 1f;
            Time.timeScale = currentSpeed;
        }

        UpdateButtonVisual();
    }

    public void UpdateButtonVisual()
    {
        buttonText.text = Time.timeScale == 1f ? ">" : Time.timeScale == 2f ? ">>" : ">>>";
    }
}
