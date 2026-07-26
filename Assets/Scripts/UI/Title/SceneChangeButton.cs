using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[RequireComponent(typeof(Button))]
public class SceneChangeButton : MonoBehaviour
{
    public string sceneName;
    public float fadeDuration = 1f; // Duration of the fade
    public Image fadeImage; // Fullscreen black image

    protected Button button;
    protected bool canBeClicked = true;

    void Start()
    {
        button = GetComponent<Button>();
        if (button != null)
            button.onClick.AddListener(OnButtonClicked);

        // Ensure fadeImage is fully transparent at start
        if (fadeImage != null)
        {
            fadeImage.gameObject.SetActive(false);
            Color c = fadeImage.color;
            c.a = 0f;
            fadeImage.color = c;
        }
        else
        {
            fadeImage = MainUI.fadeImage;
        }
    }

    public virtual void OnButtonClicked()
    {
        if (!canBeClicked)
            return;
        canBeClicked = false;
        Debug.Log($"Changing scene to: {sceneName}");
        if (fadeImage != null)
            StartCoroutine(FadeAndLoad());
        else
            SceneManager.LoadScene(sceneName);
    }

    private IEnumerator FadeAndLoad()
    {
        fadeImage.gameObject.SetActive(true);
        float timer = 0f;
        Color c = fadeImage.color;

        while (timer < fadeDuration)
        {
            timer += Time.unscaledDeltaTime; // Use unscaled in case timeScale = 0
            c.a = Mathf.Clamp01(timer / fadeDuration);
            fadeImage.color = c;
            yield return null;
        }

        // Ensure fully black
        c.a = 1f;
        fadeImage.color = c;

        SceneManager.LoadScene(sceneName);
    }
}