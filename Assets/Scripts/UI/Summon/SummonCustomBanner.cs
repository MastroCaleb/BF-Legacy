using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class SummonCustomBanner : MonoBehaviour
{
    public SummonBanner summonBanner;

    public TextMeshProUGUI bannerDescText;
    public Image bannerImage;
    public Button summonButton;

    public Button pullChancesButton;
    public GameObject pullChancesList;

    public GameObject summonGate;

    [Header("Rates UI")]
    public TMP_FontAsset ratesFont;
    public int separatorFontSize = 22;
    public int unitFontSize = 16;
    public RectTransform content;

    void Start()
    {
        bannerDescText.text = summonBanner.bannerDesc;
        bannerImage.sprite = summonBanner.bgBannerSprite;

        summonButton.GetComponent<Image>().sprite = summonBanner.buttonSprite;
        SpriteState state = summonButton.spriteState;
        state.pressedSprite = summonBanner.pressedButtonSprite;
        summonButton.spriteState = state;

        summonButton.GetComponent<ActivateDeactivateButton>().objectToActivate = summonGate;
        summonButton.GetComponent<ActivateDeactivateButton>().objectToDeactivate = gameObject.transform.parent.parent.gameObject;

        summonButton.onClick.AddListener(SetSummonBanner);

        pullChancesButton.onClick.AddListener(PopulateSummonRatesUI);
        pullChancesButton.GetComponent<ActivateDeactivateButton>().objectToActivate = pullChancesList;
    }

    public void SetSummonBanner()
    {
        summonGate.GetComponent<SummonCustomGate>().summonBanner = summonBanner;
    }

    // Builds the summon rate breakdown into `content` (grid/vertical layout).
    // Layout: "Featured Units (n%):" separator, then each featured unit with
    // its individual pull chance, then "Base Units (n%):" separator and the
    // base units. Call this whenever the rates panel is opened.
    public void PopulateSummonRatesUI()
    {
        if (content == null || summonBanner == null) return;

        for (int i = content.childCount - 1; i >= 0; i--)
        {
            Destroy(content.GetChild(i).gameObject);
        }

        content.position = Vector3.zero;

        AddSeparator($"Featured Units ({FormatPercent(summonBanner.featuredPullChance)}%):");
        AddPoolEntries(summonBanner.featuredSummonPools, summonBanner.featuredPullChance);

        float basePullChance = 100f - summonBanner.featuredPullChance;
        AddSeparator($"Base Units ({FormatPercent(basePullChance)}%):");
        AddPoolEntries(summonBanner.baseSummonPools, basePullChance);
    }

    private void AddPoolEntries(List<SummonPool> pools, float sectionChance)
    {
        if (pools == null || pools.Count == 0) return;

        float chancePerPool = sectionChance / pools.Count;

        foreach (var pool in pools)
        {
            if (pool == null || pool.poolUnitKeys == null || pool.poolUnitKeys.Count == 0) continue;

            float chancePerUnit = chancePerPool / pool.poolUnitKeys.Count;

            foreach (var unitKey in pool.poolUnitKeys)
            {
                Unit unit = UnitRegistry.GetUnitById(unitKey);
                string unitName = unit != null ? unit.unitName : unitKey;
                AddUnitEntry(unitName, chancePerUnit);
            }
        }
    }

    private void AddSeparator(string label)
    {
        GameObject go = new GameObject("Separator");
        go.transform.SetParent(content, false);
        go.AddComponent<RectTransform>();

        TextMeshProUGUI text = go.AddComponent<TextMeshProUGUI>();
        text.richText = true;
        text.text = $"<b>{label}</b>";
        text.fontSize = separatorFontSize;
        text.alignment = TextAlignmentOptions.Left;
        if (ratesFont != null) text.font = ratesFont;
    }

    private void AddUnitEntry(string unitName, float chance)
    {
        GameObject go = new GameObject("UnitEntry");
        go.transform.SetParent(content, false);
        go.AddComponent<RectTransform>();

        TextMeshProUGUI text = go.AddComponent<TextMeshProUGUI>();
        text.richText = true;
        text.text = $"{unitName}    {FormatPercent(chance)}%";
        text.fontSize = unitFontSize;
        text.alignment = TextAlignmentOptions.Left;
        if (ratesFont != null) text.font = ratesFont;
    }

    private string FormatPercent(float value)
    {
        return value % 1f == 0f ? value.ToString("0") : value.ToString("0.##");
    }
}
