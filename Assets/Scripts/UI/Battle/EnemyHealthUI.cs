using System;
using System.Collections.Generic;
using TMPro;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.UI;

public class EnemyHealthUI : MonoBehaviour
{
    public static UnitBehaviour enemyUnit;
    public TextMeshProUGUI healthPercentageText;
    public TextMeshProUGUI enemyNameText;
    public BarUI healthBar;
    public Image elementIcon;
    private static Dictionary<UnitBehaviour, string> enemyNames = new();

    void Update()
    {
        if (enemyUnit == null) return;
        GetEnemyName();
        elementIcon.sprite = BattleUI.elementIcons[(int)enemyUnit.unitData.element];
        healthBar.maxValue = enemyUnit.isEnemyUnit ? enemyUnit.enemyData.health : enemyUnit.unitData.maxHealth;
        healthBar.currentValue = enemyUnit.currentHealth;
        healthBar.UpdateUI();
        healthPercentageText.text = $"HP: {(int)((float)enemyUnit.currentHealth / (enemyUnit.isEnemyUnit ? enemyUnit.enemyData.health : enemyUnit.unitData.maxHealth) * 100f)}%";
    }

    void GetEnemyName()
    {
        if (enemyNames.ContainsKey(enemyUnit))
        {
            enemyNameText.text = enemyNames[enemyUnit];
        }
        else
        {
            enemyNameText.text = enemyUnit.unitData.unitName;
        }
    }

    public static void SetEnemyNames(TeamBehaviour enemyTeam)
    {
        enemyUnit = enemyTeam.units[0];
        enemyNames.Clear();
        Dictionary<string, int> nameCounts = new();

        foreach (var enemy in enemyTeam.units)
        {
            string baseName = enemy.unitData.unitName;
            if (!nameCounts.ContainsKey(baseName))
            {
                nameCounts[baseName] = 0;
            }
            nameCounts[baseName]++;
        }

        Dictionary<string, int> currentCounts = new();
        foreach (var enemy in enemyTeam.units)
        {
            string baseName = enemy.unitData.unitName;
            if (!currentCounts.ContainsKey(baseName))
            {
                currentCounts[baseName] = 0;
            }
            currentCounts[baseName]++;

            string uniqueName = baseName;
            if (nameCounts[baseName] > 1)
            {
                uniqueName += " " + IntToAlphabetic(currentCounts[baseName] - 1);
            }

            enemyNames[enemy] = uniqueName;
        }
    }

    private static string IntToAlphabetic(int number)
    {
        number++; // convert 0 → A, 1 → B, ...

        string result = "";
        while (number > 0)
        {
            number--;
            result = (char)('A' + (number % 26)) + result;
            number /= 26;
        }
        return result;
    }
}
