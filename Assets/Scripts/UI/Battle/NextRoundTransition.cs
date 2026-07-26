using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using TMPro;

public class NextRoundTransition : MonoBehaviour
{
    [Header("References")]
    public RectTransform transition;
    public Image blackMask;
    public BarUI loadingBar;
    public RectTransform swordIcon;
    public ImageToFont roundCounterText;
    public TextMeshProUGUI dungeonNameText;
    public TextMeshProUGUI missionNameText;

    [Header("Sound")]
    public AudioClip progress;

    [Header("Dungeon Info")]
    public string dungeonName;
    public string missionName;
    public int maxRounds;

    [Header("Positions")]
    public Vector2 startPosition;
    public Vector2 endPosition;

    [Header("Movement")]
    public float moveSpeed = 500f;

    [Header("Timing")]
    public float waitBeforeFade = 1f;
    public float delayBeforeFadeBack = 2f;
    public float fadeSpeed = 2f;

    private void Start()
    {
        transition.anchoredPosition = startPosition;
    }

    public void Initialize(string dungeon, string mission, int maxRounds)
    {
        dungeonName = dungeon;
        missionName = mission;
        this.maxRounds = maxRounds;

        // Update UI elements
        roundCounterText.SetText(1 + "/" + maxRounds);
        loadingBar.maxValue = maxRounds-1;
        loadingBar.currentValue = 0;
        loadingBar.UpdateUI();
    }

    public IEnumerator PlayTransition(int currentRound)
    {
        dungeonNameText.text = dungeonName;
        missionNameText.text = "\""+ missionName + "\"";
        // Move to end position
        yield return StartCoroutine(MoveToPosition(endPosition));

        // Wait before starting fade
        yield return new WaitForSeconds(waitBeforeFade);

        // Fade mask to transparent
        yield return StartCoroutine(FadeMask(0f));

        yield return StartCoroutine(AnimateBarAndSword(currentRound, 0.5f));
        
        roundCounterText.SetText((currentRound + 1) + "/" + maxRounds);

        // Wait before fading back
        yield return new WaitForSeconds(delayBeforeFadeBack);

        // Fade mask back to black
        yield return StartCoroutine(FadeMask(1f));

        // Move transition back to start
        yield return StartCoroutine(MoveToPosition(startPosition));
    }

    public float GetTotalTransitionTime()
    {
        float moveTime = Vector2.Distance(startPosition, endPosition) / moveSpeed;
        return (moveTime * 2 + waitBeforeFade + delayBeforeFadeBack + (1f / fadeSpeed) * 2) + 2;
    }

    IEnumerator MoveToPosition(Vector2 target)
    {
        while (Vector2.Distance(transition.anchoredPosition, target) > 0.1f)
        {
            transition.anchoredPosition = Vector2.MoveTowards(
                transition.anchoredPosition,
                target,
                moveSpeed * Time.deltaTime
            );

            yield return null;
        }

        transition.anchoredPosition = target;
    }

    IEnumerator FadeMask(float targetAlpha)
    {
        Color color = blackMask.color;

        while (!Mathf.Approximately(color.a, targetAlpha))
        {
            color.a = Mathf.MoveTowards(
                color.a,
                targetAlpha,
                fadeSpeed * Time.deltaTime
            );

            blackMask.color = color;
            yield return null;
        }

        color.a = targetAlpha;
        blackMask.color = color;
    }

    IEnumerator AnimateBarAndSword(int targetRound, float speed)
    {
        if (loadingBar.maxValue == 0) yield break;

        SoundManager.Instance.PlaySound(progress);

        float targetFill = (float)targetRound / loadingBar.maxValue;
        float currentFill = loadingBar.fillImage.fillAmount;

        yield return new WaitForEndOfFrame();

        RectTransform fillRect = loadingBar.fillImage.rectTransform;
        float fillWidth = fillRect.rect.width;
        float leftX = -fillWidth * fillRect.pivot.x;
        float rightX = leftX + fillWidth;

        // Convert left and right edges from fill image local space to world space, then to sword parent space
        RectTransform swordParent = swordIcon.parent as RectTransform;

        Vector3 worldLeft = fillRect.TransformPoint(new Vector3(leftX, 0, 0));
        Vector3 worldRight = fillRect.TransformPoint(new Vector3(rightX, 0, 0));

        Vector2 localLeft = swordParent.InverseTransformPoint(worldLeft);
        Vector2 localRight = swordParent.InverseTransformPoint(worldRight);

        float halfSwordWidth = swordIcon.rect.width / 5;

        while (!Mathf.Approximately(currentFill, targetFill))
        {
            currentFill = Mathf.MoveTowards(currentFill, targetFill, speed * Time.deltaTime);
            loadingBar.fillImage.fillAmount = currentFill;

            float swordX = Mathf.Lerp(localRight.x, localLeft.x, currentFill) - halfSwordWidth;
            swordIcon.anchoredPosition = new Vector2(swordX, swordIcon.anchoredPosition.y);

            yield return null;
        }

        loadingBar.currentValue = targetRound;
        loadingBar.UpdateUI();

        float finalSwordX = Mathf.Lerp(localRight.x, localLeft.x, targetFill) - halfSwordWidth;
        swordIcon.anchoredPosition = new Vector2(finalSwordX, swordIcon.anchoredPosition.y);
    }
}
