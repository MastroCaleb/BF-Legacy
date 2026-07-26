using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class LevelUpDetailsUI : MonoBehaviour
{

    public AudioClip levelUpSound;
    public AudioClip barProgressSound;
    public TextMeshProUGUI levelText;
    public TextMeshProUGUI oldLevelText;
    public TextMeshProUGUI hpText;
    public TextMeshProUGUI oldHpText;
    public TextMeshProUGUI bbLvlText;
    public TextMeshProUGUI oldBbLvlText;
    public TextMeshProUGUI atkText;
    public TextMeshProUGUI oldAtkText;
    public TextMeshProUGUI defText;
    public TextMeshProUGUI oldDefText;
    public TextMeshProUGUI recText;
    public TextMeshProUGUI oldRecText;
    public TextMeshProUGUI unitNextLevelText;
    public BarUI expBar;

    public List<Image> arrowImages;

    public OldSamAnimator levelUpAnimator;
    public OldSamAnimator levelUpAddAnimator;

    public static int oldLevel, oldHp, oldAtk, oldDef, oldRec, oldBBLv;

    // Update is called once per frame
    public void UpdateView(int unitKey)
    {
        UnitInventoryData unitData = PlayerUnitInventoryDatabase.GetUnitByKey(unitKey);
        Unit unit = unitData.unit;

        Debug.Log($"UpdateView OLD: level={oldLevel} hp={oldHp} atk={oldAtk}");
        Debug.Log($"UpdateView NEW: level={unitData.currentLevel} hp={unit.maxHealth + unitData.hpImpBonus + unitData.hpLevelUpBonus} atk={unit.atk + unitData.atkImpBonus + unitData.atkLevelUpBonus}");

        int newLevel = unitData.currentLevel;
        int newHp = unit.maxHealth + unitData.hpImpBonus + unitData.hpLevelUpBonus;
        int newBBLv = unitData.currentBBLevel;
        int newAtk = unit.atk + unitData.atkImpBonus + unitData.atkLevelUpBonus;
        int newDef = unit.def + unitData.defImpBonus + unitData.defLevelUpBonus;
        int newRec = unit.rec + unitData.recImpBonus + unitData.recLevelUpBonus;

        oldLevelText.text = $"{oldLevel}/{unit.maxLevel}";
        levelText.text = oldLevel < newLevel ? $"<color=#2767ab>{unitData.currentLevel}</color> / {unit.maxLevel}" : $"{unitData.currentLevel}/{unit.maxLevel}";

        oldHpText.text = $"{oldHp}";
        hpText.text = oldHp < newHp ? $"<color=#2767ab>{newHp}</color>" : $"{newHp}";

        oldBbLvlText.text = $"{oldBBLv} / 10";
        bbLvlText.text = oldBBLv < newBBLv ? $"<color=#2767ab>{newBBLv}</color> / 10" : $"{newBBLv} / 10";

        oldAtkText.text = $"{oldAtk}";
        atkText.text = oldAtk < newAtk ? $"<color=#2767ab>{newAtk}</color>" : $"{newAtk}";

        oldDefText.text = $"{oldDef}";
        defText.text = oldDef < newDef ? $"<color=#2767ab>{newDef}</color>" : $"{newDef}";

        oldRecText.text = $"{oldRec}";
        recText.text = oldRec < newRec ? $"<color=#2767ab>{newRec}</color>" : $"{newRec}";

        if (unitData.currentLevel == unitData.unit.maxLevel)
        {
            expBar.maxValue = 1;
            expBar.currentValue = 1;
        }
        else
        {
            long cumulativeThisLevel = ExperienceTable.GetCumulativeExp(unitData.currentLevel, unitData.unit.baseExp);
            long cumulativeNextLevel = cumulativeThisLevel + ExperienceTable.GetExpToNextLevel(unitData.currentLevel, unitData.unit.baseExp);

            expBar.maxValue = (int)(cumulativeNextLevel - cumulativeThisLevel);
            expBar.currentValue = (int)(unitData.currentExperience - cumulativeThisLevel);
        }

        expBar.UpdateUI();
        unitNextLevelText.text = unitData.currentLevel == unitData.unit.maxLevel
            ? "____"
            : $"{expBar.maxValue - expBar.currentValue}";

        if(oldLevel != unitData.currentLevel)
        {
            SoundManager.Instance.PlaySound(levelUpSound);
            levelUpAnimator.gameObject.SetActive(true);
            levelUpAddAnimator.gameObject.SetActive(true);
            levelUpAnimator.InitializeAnimator();
            levelUpAddAnimator.InitializeAnimator();
            levelUpAnimator.Replay();
            levelUpAddAnimator.Replay();
        }
        else
        {
            levelUpAnimator.gameObject.SetActive(false);
            levelUpAddAnimator.gameObject.SetActive(false);
        }
    }

    //TODO: Add animations and old to new stats for comparison
    public IEnumerator LevelUpAnimation(int unitKey)
    {
        UnitInventoryData unitData = PlayerUnitInventoryDatabase.GetUnitByKey(unitKey);
        Unit unit = unitData.unit;
        
        yield return null;
    }
}
