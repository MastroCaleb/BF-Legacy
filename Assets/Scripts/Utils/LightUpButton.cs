using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class LightUpButton : MonoBehaviour
{
    public Image lightUpButtonImg;
    Button button;
    public float fadeDuration = 0.2f; // Duration for each fade (in/out)

    void Start()
    {
        button = GetComponent<Button>();
        if (button != null)
            button.onClick.AddListener(OnButtonClicked);
    }

    public void OnButtonClicked()
    {
        StartCoroutine(FadeImageAnimation());
    }

    private IEnumerator FadeImageAnimation()
    {
        // Fade in
        float elapsedTime = 0f;
        Color startColor = lightUpButtonImg.color;
        Color targetColor = startColor;
        targetColor.a = 1f; // Full color
        
        while (elapsedTime < fadeDuration)
        {
            elapsedTime += Time.deltaTime;
            float t = elapsedTime / fadeDuration;
            lightUpButtonImg.color = Color.Lerp(startColor, targetColor, t);
            yield return null;
        }
        lightUpButtonImg.color = targetColor;
        
        // Fade out
        elapsedTime = 0f;
        startColor = lightUpButtonImg.color;
        targetColor = startColor;
        targetColor.a = 0f; // Transparent
        
        while (elapsedTime < fadeDuration)
        {
            elapsedTime += Time.deltaTime;
            float t = elapsedTime / fadeDuration;
            lightUpButtonImg.color = Color.Lerp(startColor, targetColor, t);
            yield return null;
        }
        lightUpButtonImg.color = targetColor;
    }
}
