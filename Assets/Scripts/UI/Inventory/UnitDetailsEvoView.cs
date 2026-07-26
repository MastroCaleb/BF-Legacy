using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UnitDetailsEvoView : MonoBehaviour
{
    public TextMeshProUGUI lvText;
    public TextMeshProUGUI lvEvoText;
    public TextMeshProUGUI hpText;
    public TextMeshProUGUI hpEvoText;
    public TextMeshProUGUI atkText;
    public TextMeshProUGUI atkEvoText;
    public TextMeshProUGUI defText;
    public TextMeshProUGUI defEvoText;
    public TextMeshProUGUI recText;
    public TextMeshProUGUI recEvoText;
    public TextMeshProUGUI costText;
    public TextMeshProUGUI costEvoText;

    public void UpdateDetails()
    {
        UnitInventoryData baseUnitData = PlayerUnitInventoryDatabase.GetUnitByKey(EvoMenu.baseUnitKey);
        Unit evoUnit = UnitRegistry.GetUnitById(baseUnitData.unit.evoInto);

        lvText.text = "<size=+10>" + baseUnitData.currentLevel.ToString() + "</size>/" + baseUnitData.unit.maxLevel.ToString();

        if (evoUnit == null)
        {
            // Nessuna ulteriore evoluzione: mostra gli stessi valori della base, senza crashare
            lvEvoText.text = lvText.text;
            hpEvoText.text = $"{baseUnitData.unit.maxHealth + baseUnitData.hpLevelUpBonus + baseUnitData.hpImpBonus}";
            atkEvoText.text = $"{baseUnitData.unit.atk + baseUnitData.atkLevelUpBonus + baseUnitData.atkImpBonus}";
            defEvoText.text = $"{baseUnitData.unit.def + baseUnitData.defLevelUpBonus + baseUnitData.defImpBonus}";
            recEvoText.text = $"{baseUnitData.unit.rec + baseUnitData.recLevelUpBonus + baseUnitData.recImpBonus}";
            costEvoText.text = $"{baseUnitData.unit.summonCost}";
        }
        else
        {
            lvEvoText.text = "<size=+10>" + 1 + "</size>/" + evoUnit.maxLevel.ToString();
            hpEvoText.text = $"{evoUnit.maxHealth + baseUnitData.hpLevelUpBonus + baseUnitData.hpImpBonus}";
            atkEvoText.text = $"{evoUnit.atk + baseUnitData.atkLevelUpBonus + baseUnitData.atkImpBonus}";
            defEvoText.text = $"{evoUnit.def + baseUnitData.defLevelUpBonus + baseUnitData.defImpBonus}";
            recEvoText.text = $"{evoUnit.rec + baseUnitData.recLevelUpBonus + baseUnitData.recImpBonus}";
            costEvoText.text = $"{evoUnit.summonCost}";
        }

        hpText.text = $"{baseUnitData.unit.maxHealth + baseUnitData.hpLevelUpBonus + baseUnitData.hpImpBonus}";
        atkText.text = $"{baseUnitData.unit.atk + baseUnitData.atkLevelUpBonus + baseUnitData.atkImpBonus}";
        defText.text = $"{baseUnitData.unit.def + baseUnitData.defLevelUpBonus + baseUnitData.defImpBonus}";
        recText.text = $"{baseUnitData.unit.rec + baseUnitData.recLevelUpBonus + baseUnitData.recImpBonus}";
        costText.text = $"{baseUnitData.unit.summonCost}";
    }
}
