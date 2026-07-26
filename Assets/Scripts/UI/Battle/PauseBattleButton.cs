using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class PauseBattleButton : MonoBehaviour
{
    public GameObject pauseMenu;

    public Button pauseButton;
    public Button resumeButton;
    public Button exitButton;

    [Header("References")]
    public BarUI loadingBar;
    public RectTransform swordIcon;
    public ImageToFont roundCounterText;
    public TextMeshProUGUI dungeonNameText;
    public TextMeshProUGUI missionNameText;

    private int _currentRound;

    void Start()
    {
        if (pauseButton != null)
            pauseButton.onClick.AddListener(OnPause);

        if (resumeButton != null)
            resumeButton.onClick.AddListener(OnResume);

        if (exitButton != null)
            exitButton.onClick.AddListener(OnExit);
    }

    public void UpdateState(string dungeon, string mission, int currentRound, int totalRounds)
    {
        dungeonNameText.text = dungeon;
        missionNameText.text = "\"" + mission + "\"";
        roundCounterText.SetText(currentRound + "/" + totalRounds);

        loadingBar.maxValue     = totalRounds - 1;
        loadingBar.currentValue = currentRound-1;
        loadingBar.UpdateUI();

        SyncSword(currentRound-1, totalRounds-1);
    }

    void SyncSword(int currentRound, int maxRounds)
    {
        if (maxRounds == 0) return;

        RectTransform fillRect    = loadingBar.fillImage.rectTransform;
        float fillWidth           = fillRect.rect.width;

        // Se rect.width è 0 (menu inattivo, layout non calcolato), usa sizeDelta
        if (fillWidth == 0f)
            fillWidth = fillRect.sizeDelta.x;

        float leftX  = -fillWidth * fillRect.pivot.x;
        float rightX = leftX + fillWidth;

        RectTransform swordParent = swordIcon.parent as RectTransform;
        Vector2 localLeft  = swordParent.InverseTransformPoint(fillRect.TransformPoint(new Vector3(leftX,  0, 0)));
        Vector2 localRight = swordParent.InverseTransformPoint(fillRect.TransformPoint(new Vector3(rightX, 0, 0)));

        float targetFill     = (float)currentRound / maxRounds;
        float halfSwordWidth = swordIcon.rect.width / 5f;
        float swordX         = Mathf.Lerp(localRight.x, localLeft.x, targetFill) - halfSwordWidth;

        swordIcon.anchoredPosition = new Vector2(swordX, swordIcon.anchoredPosition.y);
    }

    public void OnPause()
    {
        if(CutInAnimation.isPlaying) return;

        Time.timeScale = 0;
        pauseMenu.SetActive(true);
    }

    public void OnResume()
    {
        Time.timeScale = SpeedUpBattleButtonUI.currentSpeed;
        pauseMenu.SetActive(false);
    }

    public void OnExit()
    {
        Time.timeScale = 1;
        SceneManager.LoadScene("MainMenuScene");
    }
}