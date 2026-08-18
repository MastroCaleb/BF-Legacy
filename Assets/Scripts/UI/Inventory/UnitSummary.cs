using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UnitSummary : MonoBehaviour
{
    public UnitInventoryData unitInventoryData;

    public TextMeshProUGUI unitNameText;
    public TextMeshProUGUI unitNumText;
    public TextMeshProUGUI unitRarityText;
    public TextMeshProUGUI unitTypeText;
    public TextMeshProUGUI unitLevelText;
    public TextMeshProUGUI unitNextLevelText;
    public TextMeshProUGUI unitHpText;
    public TextMeshProUGUI unitAtkText;
    public TextMeshProUGUI unitDefText;
    public TextMeshProUGUI unitRecText;
    public TextMeshProUGUI unitCostText;
    public Image unitFullArtwork;
    public Image elementIcon;
    public List<Sprite> elementIcons;
    public ScrollingTMPText skill1Desc;
    public ScrollingTMPText skill2Desc;
    public TextMeshProUGUI skill2Lv;
    public TextMeshProUGUI skill1Name;
    public TextMeshProUGUI skill2Name;
    public BarUI expBar;

    List<GameObject> toActivateOnBackTargets;
    List<GameObject> toDeactivateOnBackTargets;

    public void SetUnit(UnitInventoryData unit, List<GameObject> toActivateOnBack = null, List<GameObject> toDeactivateOnBack = null)
    {
        this.unitInventoryData = unit;
        this.toActivateOnBackTargets = toActivateOnBack;
        this.toDeactivateOnBackTargets = toDeactivateOnBack;
        UpdateSummary();
    }

    public void UpdateSummary()
    {
        if (unitInventoryData == null) return;

        unitNameText.text = unitInventoryData.unit.unitName;
        unitNumText.text = $"Unit No.{unitInventoryData.unit.unitNumber}";
        unitRarityText.text = ReturnStarIcons(unitInventoryData.unit.rarity);
        unitTypeText.text = unitInventoryData.type.ToString();
        unitLevelText.text = "<size=+10>" + unitInventoryData.currentLevel.ToString() + "</size>/" + unitInventoryData.unit.maxLevel.ToString();
        unitHpText.text = $"{unitInventoryData.unit.maxHealth + unitInventoryData.hpLevelUpBonus + unitInventoryData.hpImpBonus}";
        unitAtkText.text = $"{unitInventoryData.unit.atk + unitInventoryData.atkLevelUpBonus + unitInventoryData.atkImpBonus}";
        unitDefText.text = $"{unitInventoryData.unit.def + unitInventoryData.defLevelUpBonus + unitInventoryData.defImpBonus}";
        unitRecText.text = $"{unitInventoryData.unit.rec + unitInventoryData.recLevelUpBonus + unitInventoryData.recImpBonus}";
        unitCostText.text = $"{unitInventoryData.unit.summonCost}";
        // Max = exp required for this level gap only
        if (unitInventoryData.currentLevel == unitInventoryData.unit.maxLevel)
        {
            expBar.maxValue = 1;
            expBar.currentValue = 1;
        }
        else
        {
            long cumulativeThisLevel = ExperienceTable.GetCumulativeExp(unitInventoryData.currentLevel, unitInventoryData.unit.baseExp);
            long cumulativeNextLevel = cumulativeThisLevel + ExperienceTable.GetExpToNextLevel(unitInventoryData.currentLevel, unitInventoryData.unit.baseExp);

            expBar.maxValue = (int)(cumulativeNextLevel - cumulativeThisLevel);
            expBar.currentValue = (int)(unitInventoryData.currentExperience - cumulativeThisLevel);
        }

        expBar.UpdateUI();
        unitNextLevelText.text = unitInventoryData.currentLevel == unitInventoryData.unit.maxLevel
            ? "____"
            : $"{expBar.maxValue - expBar.currentValue}";
        UpdateSkillDescriptions();
        UpdateElementIcon();

        unitFullArtwork.sprite = unitInventoryData.unit.unitFullArt;
        unitFullArtwork.SetNativeSize();

        float imageW = unitInventoryData.unit.unitFullArt.texture.width;
        float imageH = unitInventoryData.unit.unitFullArt.texture.height;
        float jsonX = unitInventoryData.unit.unitDisplayDetailPosition.x;
        float jsonY = unitInventoryData.unit.unitDisplayDetailPosition.y;
        float jsonW = unitInventoryData.unit.unitDisplayDetailPosition.width;
        float jsonH = unitInventoryData.unit.unitDisplayDetailPosition.height;

        const float CANVAS_W = 640f;
        const float CANVAS_H = 1136f;

        float S = 1f;

        float unityX = (jsonX + jsonW / 2f - CANVAS_W / 2f) * S;
        float unityY = (CANVAS_H / 2f - jsonY - jsonH / 2f) * S;
        float unityW = jsonW * S;
        float unityH = jsonH * S;

        RectTransform rt = unitFullArtwork.GetComponent<RectTransform>();
        rt.anchoredPosition = new Vector2(unityX, unityY-120);
        rt.sizeDelta = new Vector2(unityW, unityH);
    }

    public void UpdateSkillDescriptions()
    {
        if (unitInventoryData == null) return;

        skill1Name.text = unitInventoryData.unit.leaderAbility?.abilityName ?? "None";
        skill2Name.text = unitInventoryData.unit.bbAbility?.abilityName ?? "None";
        skill2Lv.text = "Lv. " + unitInventoryData.currentBBLevel;
        skill1Desc.SetText(unitInventoryData.unit.leaderAbility?.abilityDesc);
        skill2Desc.SetText(unitInventoryData.unit.bbAbility?.abilityDesc);
    }

    public void UpdateElementIcon()
    {
        if (unitInventoryData == null) return;

        switch (unitInventoryData.unit.element)
        {
            case ElementalType.Fire:
                elementIcon.sprite = elementIcons[0];
                break;
            case ElementalType.Water:
                elementIcon.sprite = elementIcons[1];
                break;
            case ElementalType.Earth:
                elementIcon.sprite = elementIcons[2];
                break;
            case ElementalType.Thunder:
                elementIcon.sprite = elementIcons[3];
                break;
            case ElementalType.Light:
                elementIcon.sprite = elementIcons[4];
                break;
            case ElementalType.Dark:
                elementIcon.sprite = elementIcons[5];
                break;
            default:
                elementIcon.sprite = null;
                break;
        }
    }

    public string ReturnStarIcons(UnitRarity rarity)
    {
        if (rarity == UnitRarity.OMNI)
        {
            return "<sprite index=2>";
        }

        string starIcon = "<sprite index=0>";
        string result = "";
        for (int i = 0; i < (int)rarity + 1; i++)
        {
            result += starIcon;
        }
        return result;
    }

    public void Back()
    {
        this.gameObject.SetActive(false);
        if (toActivateOnBackTargets != null)
        {
            foreach (GameObject obj in toActivateOnBackTargets)
            {
                obj.SetActive(true);
            }
        }
        if (toDeactivateOnBackTargets != null)
        {
            foreach (GameObject obj in toDeactivateOnBackTargets)
            {
                obj.SetActive(false);
            }
        }
    }
}
