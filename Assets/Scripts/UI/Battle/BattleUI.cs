using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class BattleUI : MonoBehaviour
{
    public List<Sprite> elementIconsHelper;
    public static List<Sprite> elementIcons;
    public List<Sprite> elementDisableIconsHelper;
    public static List<Sprite> elementDisableIcons;

    public List<UnitSlotUI> unitSlotsHelper;
    public static List<UnitSlotUI> unitSlots;

    public RectTransform playerUnitsLayerHelper;
    public static RectTransform playerUnitsLayer;
    public RectTransform enemyUnitsLayerHelper;
    public static RectTransform enemyUnitsLayer;
    public RectTransform dropsLayerHelper;
    public static RectTransform dropsLayer;
    public RectTransform uiEffectLayerHelper;
    public static RectTransform uiEffectLayer;
    public RectTransform popupLayerHelper;
    public static RectTransform popupLayer;

    public RectTransform titleTextLayerHelper;
    public static RectTransform titleTextLayer;

    public RectTransform sliderHelper;
    public static RectTransform slider;
    public GameObject bbSliderHelper;
    public static GameObject bbSlider;
    public GameObject sbbSliderHelper;
    public static GameObject sbbSlider;
    public GameObject guardSliderHelper;
    public static GameObject guardSlider;

    public RectTransform zelPointHelper;
    public static RectTransform zelPoint;
    public RectTransform karmaPointHelper;
    public static RectTransform karmaPoint;
    public RectTransform unitPointHelper;
    public static RectTransform unitPoint;

    public ImageToFont zelTextHelper;
    public static ImageToFont zelText;
    public ImageToFont karmaTextHelper;
    public static ImageToFont karmaText;
    public ImageToFont unitTextHelper;
    public static ImageToFont unitText;

    public GameObject uiDropLayerHelper;
    public static GameObject uiDropLayer;

    public GameObject enemySelectHelper;
    public static GameObject enemySelect;

    public Image fadeHelper;
    public static Image fadeImage;
    public float fadeDurationHelper = 1f; // Duration of the fade
    public static float fadeDuration;

    public void Awake()
    {
        elementIcons = elementIconsHelper;
        elementDisableIcons = elementDisableIconsHelper;

        unitSlots = unitSlotsHelper;

        playerUnitsLayer = playerUnitsLayerHelper;
        enemyUnitsLayer = enemyUnitsLayerHelper;
        dropsLayer = dropsLayerHelper;
        popupLayer = popupLayerHelper;

        titleTextLayer = titleTextLayerHelper;

        slider = sliderHelper;
        bbSlider = bbSliderHelper;
        sbbSlider = sbbSliderHelper;
        guardSlider = guardSliderHelper;

        zelPoint = zelPointHelper;
        karmaPoint = karmaPointHelper;
        unitPoint = unitPointHelper;

        zelText = zelTextHelper;
        karmaText = karmaTextHelper;
        unitText = unitTextHelper;

        uiDropLayer = uiDropLayerHelper;
        uiEffectLayer = uiEffectLayerHelper;

        enemySelect = enemySelectHelper;

        fadeImage = fadeHelper;
        fadeDuration = fadeDurationHelper;
    }

    public static void UpdateText()
    {   
        zelText.SetText("X" + BattleManager.totalZelReward.ToString("D5"));
        karmaText.SetText("X" + BattleManager.totalKarmaReward.ToString("D5"));
        unitText.SetText("X" + BattleManager.unitDrops.Count.ToString("D2"));
    }

    public static IEnumerator FadeAndLoad(string sceneName)
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
