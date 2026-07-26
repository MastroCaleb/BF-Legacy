using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class BaseUnitDetails : MonoBehaviour
{
    public UnitTableRenderer unitRenderer;
    public TextMeshProUGUI lvText;
    public TextMeshProUGUI hpText;
    public TextMeshProUGUI atkText;
    public TextMeshProUGUI defText;
    public TextMeshProUGUI recText;
    public TextMeshProUGUI costText;
    public Image elementIcon;
    public List<Sprite> elementIcons;

    public void UpdateDetails()
    {
        if (unitRenderer != null && unitRenderer.inventoryData != null)
        {
            UnitInventoryData baseUnitData = unitRenderer.inventoryData;
            unitRenderer.SetUnit(baseUnitData, true);
            lvText.text = $"{baseUnitData.currentLevel}";
            hpText.text = $"{baseUnitData.unit.maxHealth + baseUnitData.hpLevelUpBonus + baseUnitData.hpImpBonus}";
            atkText.text = $"{baseUnitData.unit.atk + baseUnitData.atkLevelUpBonus + baseUnitData.atkImpBonus}";
            defText.text = $"{baseUnitData.unit.def + baseUnitData.defLevelUpBonus + baseUnitData.defImpBonus}";
            recText.text = $"{baseUnitData.unit.rec + baseUnitData.recLevelUpBonus + baseUnitData.recImpBonus}";
            costText.text = $"{baseUnitData.unit.summonCost}";
            elementIcon.sprite = elementIcons[(int)baseUnitData.unit.element-1];
        }
    }
}
