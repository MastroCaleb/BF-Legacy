using System.Collections;
using TMPro;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class SplashScreenManager : MonoBehaviour
{
    [System.Serializable]
    public class Splash
    {
        public GameObject logoObject;
        public GameObject backgroundObject;
        public Color textColor;
    }

    public Splash[] splashes;
    public TextMeshProUGUI text;

    public float fadeTime = 1f;
    public float displayTime = 1.5f;

    void Start()
    {
        PrefabCache.Preload(); // Phase 6: warm common prefabs during the splash sequence
        StartCoroutine(PlaySplashSequence());
    }

    IEnumerator PlaySplashSequence()
    {
        foreach (Splash splash in splashes)
        {
            text.color = splash.textColor;
            Image logoImg = splash.logoObject.GetComponent<Image>();
            Image bgImg = splash.backgroundObject.GetComponent<Image>();

            splash.logoObject.SetActive(true);
            splash.backgroundObject.SetActive(true);

            // reset alpha
            Color logoColor = logoImg.color;
            Color bgColor = bgImg.color;

            logoColor.a = 0f;
            bgColor.a = 0f;

            logoImg.color = logoColor;
            bgImg.color = bgColor;

            // FADE IN
            float t = 0f;
            while (t < fadeTime)
            {
                t += Time.deltaTime;
                float bgA = Mathf.Lerp(0f, 1f, t / (fadeTime / 2)); // faster background
                float logoA = Mathf.Lerp(0f, 1f, t / fadeTime);  

                logoColor.a = bgA;
                bgColor.a = logoA;

                logoImg.color = logoColor;
                bgImg.color = bgColor;

                yield return null;
            }

            // HOLD
            yield return new WaitForSeconds(displayTime);

            // FADE OUT
            t = 0f;
            while (t < fadeTime)
            {
                t += Time.deltaTime;
                float a = Mathf.Lerp(1f, 0f, t / fadeTime);

                logoColor.a = a;

                logoImg.color = logoColor;

                yield return null;
            }
        }

        SceneManager.LoadScene("StartScene");
    }
}