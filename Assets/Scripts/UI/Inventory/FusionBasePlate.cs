using TMPro;
using UnityEngine;

public class FusionBasePlate : MonoBehaviour
{
    public TextMeshProUGUI zelCostText;
    public TextMeshProUGUI xpGainText;
    public TextMeshProUGUI remainingXpText;
    public TextMeshProUGUI nextLevelText;
    public BarUI currentXpBar;
    public BarUI gainedXpBar;

    public void UpdateView()
    {
        zelCostText.text = FusionMenu.totalZelCost.ToString();
        xpGainText.text = FusionMenu.totalXpGain.ToString();

        if(remainingXpText == null || nextLevelText == null || currentXpBar == null || gainedXpBar == null)
        {
            return;
        }

        UnitInventoryData unit = PlayerUnitInventoryDatabase.GetUnitByKey(FusionMenu.baseUnit);
        if (unit == null || unit.unit == null)
        {
            nextLevelText.text = "";
            remainingXpText.text = "";
            return;
        }

        // Simulate levelling up with the gained XP
        int simulatedLevel = unit.currentLevel;
        int simulatedXp = unit.currentExperience + FusionMenu.totalXpGain;

        Debug.Log($"[FusionBasePlate] baseUnit key={FusionMenu.baseUnit}, unit={unit?.unitId}, unit.unit={unit?.unit?.unitId}, baseExp={unit?.unit?.baseExp}");
        while (simulatedLevel < unit.unit.maxLevel)
        {
            long cumulative = 0;
            for (int lvl = 1; lvl < simulatedLevel + 1; lvl++)
                cumulative += ExperienceTable.GetExpToNextLevel(lvl, unit.unit.baseExp);

            if (simulatedXp < cumulative) break;
            simulatedLevel++;
        }

        nextLevelText.text = simulatedLevel.ToString();

        // XP needed to reach simulatedLevel (the threshold we're sitting at after gains)
        long xpFloor = 0;
        for (int lvl = 1; lvl < simulatedLevel; lvl++)
            xpFloor += ExperienceTable.GetExpToNextLevel(lvl, unit.unit.baseExp);

        // XP needed to reach simulatedLevel + 1 (the next level up from there)
        long xpCeiling = xpFloor + ExperienceTable.GetExpToNextLevel(simulatedLevel, unit.unit.baseExp);

        int xpIntoCurrentLevel = simulatedXp - (int)xpFloor;
        int xpNeededForNextLevel = (int)(xpCeiling - xpFloor);

        remainingXpText.text = simulatedLevel >= unit.unit.maxLevel
            ? "MAX"
            : (xpNeededForNextLevel - xpIntoCurrentLevel).ToString();

        // Current XP bar: where we are now in the current level (before fusion)
        long currentFloor = 0;
        for (int lvl = 1; lvl < unit.currentLevel; lvl++)
            currentFloor += ExperienceTable.GetExpToNextLevel(lvl, unit.unit.baseExp);

        long currentCeiling = currentFloor + ExperienceTable.GetExpToNextLevel(unit.currentLevel, unit.unit.baseExp);
        int xpIntoNow = unit.currentExperience - (int)currentFloor;
        int xpNeededNow = (int)(currentCeiling - currentFloor);

        currentXpBar.maxValue = xpNeededNow;
        currentXpBar.currentValue = xpIntoNow;
        currentXpBar.UpdateUI();

        // Gained XP bar: how much of the current level's requirement is covered by the gain
        // Capped at full if the gain exceeds it (i.e. unit levels up)
        gainedXpBar.maxValue = xpNeededNow;
        gainedXpBar.currentValue = Mathf.Min(xpIntoNow + FusionMenu.totalXpGain, xpNeededNow);
        gainedXpBar.UpdateUI();
    }
}