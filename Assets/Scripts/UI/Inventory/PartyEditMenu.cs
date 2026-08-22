using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class PartyEditMenu : MonoBehaviour
{
    
    public List<UnitTableRenderer> unitTableRenderersHelper;
    public static List<UnitTableRenderer> unitTableRenderers;
    public List<Button> tableButtonsHelper;
    public static List<Button> tableButtons;
    public List<BaseUnitDetails> baseUnitDetailsHelper;
    public static List<BaseUnitDetails> baseUnitDetails;
    public List<GameObject> leaderSamsHelper;
    public static List<GameObject> leaderSams;
    public TextMeshProUGUI skillNameHelper;
    public static TextMeshProUGUI skillName;
    public ScrollingTMPText skillDescHelper;
    public static ScrollingTMPText skillDesc;

    public static int currentPartyKey;
    public static int currentUnitIndex;

    private static int[] lastRenderedUnitKeys = { -2, -2, -2, -2, -2 }; // -2 = never rendered, forces first refresh

    void Awake()
    {
        lastRenderedUnitKeys = new int[]{ -2, -2, -2, -2, -2 };
        unitTableRenderers = unitTableRenderersHelper;
        tableButtons = tableButtonsHelper;
        baseUnitDetails = baseUnitDetailsHelper;
        leaderSams = leaderSamsHelper;

        skillName = skillNameHelper;
        skillDesc = skillDescHelper;
    }

    void Start()
    {
        currentPartyKey = 0; // For now, we only have one party. This will need to be changed when we have multiple parties.

        for (int i = 0; i < tableButtons.Count; i++)
        {
            int index = i; // Capture the current value of i for the lambda
            tableButtons[i].onClick.AddListener(() => SelectTable(index));
            currentUnitIndex = index;
        }
    }

    public void SelectTable(int index)
    {
        currentUnitIndex = index;

        MainUI.unitParty.SetActive(false);
        MainUI.unitList.SetActive(true);
        InventoryRenderer.selectionMode = InventorySelectionMode.UnitPartySelect;
        MainUI.inventoryRenderer.DarkenUnclickableSlots();
    }

    public static void UpdateView()
    {
        Debug.Log("Updating Party Edit Menu View");
        RefreshUnitTables();
        UpdateLeaderSkillInfo();
        SetLeaderSamActive();
    }

    public static void RefreshUnitTables()
    {
        PartyData currentParty = PartyDatabase.GetParty(currentPartyKey);

        for (int i = 0; i < 5; i++)
        {
            int unitKey = currentParty.GetUnitAt(i);

            if (unitKey == lastRenderedUnitKeys[i])
                continue; // nothing changed in this slot, skip re-render

            lastRenderedUnitKeys[i] = unitKey;

            if (unitKey != -1)
            {
                UnitInventoryData unitData = PlayerUnitInventoryDatabase.GetUnitByKey(unitKey);
                unitTableRenderers[i].SetUnit(unitData, false);
                baseUnitDetails[i].unitRenderer = unitTableRenderers[i];
                baseUnitDetails[i].gameObject.SetActive(true);
                baseUnitDetails[i].UpdateDetails();
            }
            else
            {
                unitTableRenderers[i].ClearUnit();
                baseUnitDetails[i].gameObject.SetActive(false);
            }
        }
    }

    public static void UpdateLeaderSkillInfo()
    {
        PartyData currentParty = PartyDatabase.GetParty(currentPartyKey);
        int leaderUnitKey = currentParty.GetUnitAt(currentParty.leaderUnitIndex);

        if (leaderUnitKey != -1)
        {
            UnitInventoryData leaderUnitData = PlayerUnitInventoryDatabase.GetUnitByKey(leaderUnitKey);
            skillName.text = leaderUnitData.unit.leaderAbility != null ? leaderUnitData.unit.leaderAbility.abilityName : "None";
            skillDesc.SetText(leaderUnitData.unit.leaderAbility != null ? leaderUnitData.unit.leaderAbility.abilityDesc : "");
        }
        else
        {
            skillName.text = "";
            skillDesc.SetText("");
        }
    }

    public static void SetLeaderSamActive()
    {
        PartyData currentParty = PartyDatabase.GetParty(currentPartyKey);

        for (int i = 0; i < 5; i++)
        {
            leaderSams[i].SetActive(i == currentParty.leaderUnitIndex);
        }
    }
}
