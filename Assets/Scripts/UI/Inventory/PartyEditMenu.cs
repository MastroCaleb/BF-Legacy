using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class PartyEditMenu : MonoBehaviour
{
    public List<PartyPanel> partyPanelsHelper;
    public static List<PartyPanel> partyPanels;

    public TextMeshProUGUI skillNameHelper;
    public static TextMeshProUGUI skillName;
    public ScrollingTMPText skillDescHelper;
    public static ScrollingTMPText skillDesc;

    public CustomScrollMenu partySelectorMenuHelper;
    public static CustomScrollMenu partySelectorMenu;

    public static int currentUnitIndex;
    public static int currentEditPartyKey;
    public static int currentFocusedPanelIndex;

    private Coroutine focusChangeCoroutine;

    void Awake()
    {
        partyPanels = partyPanelsHelper;
        skillName = skillNameHelper;
        skillDesc = skillDescHelper;
        partySelectorMenu = partySelectorMenuHelper;

        foreach (var panel in partyPanels)
            ResetRenderCache(panel);

        for (int p = 0; p < partyPanels.Count; p++)
        {
            int panelIndex = p;
            PartyPanel panel = partyPanels[p];

            for (int i = 0; i < panel.tableButtons.Count; i++)
            {
                int slotIndex = i;
                panel.tableButtons[i].onClick.AddListener(() => SelectTable(panelIndex, slotIndex));
            }
        }
    }

    void OnEnable()
    {
        if (partySelectorMenu != null)
        {
            partySelectorMenu.OnCenterIndexChanged += OnFocusedPanelChanged;
            OnFocusedPanelChanged(partySelectorMenu.CenterIndex);
        }
    }

    void OnDisable()
    {
        if (partySelectorMenu != null)
            partySelectorMenu.OnCenterIndexChanged -= OnFocusedPanelChanged;

        if (focusChangeCoroutine != null)
        {
            StopCoroutine(focusChangeCoroutine);
            focusChangeCoroutine = null;
        }
    }

    private void OnFocusedPanelChanged(int panelIndex)
    {
        Debug.Log(
            $"[PartyEditMenu] OnFocusedPanelChanged called: " +
            $"panelIndex={panelIndex}, " +
            $"CenterIndex={partySelectorMenu.CenterIndex}"
        );

        currentFocusedPanelIndex = panelIndex;

        if (focusChangeCoroutine != null)
            StopCoroutine(focusChangeCoroutine);

        focusChangeCoroutine = StartCoroutine(ApplyFocusChangeNextFrame(panelIndex));
    }

    private IEnumerator ApplyFocusChangeNextFrame(int panelIndex)
    {
        yield return null;

        List<int> partyKeys = PartyDatabase.GetPartyKeysOrdered();
        if (panelIndex < 0 || panelIndex >= partyKeys.Count) yield break;

        int focusedPartyKey = partyKeys[panelIndex];
        PartyData party = PartyDatabase.GetParty(focusedPartyKey);

        if (HasAtLeastOneUnit(party))
            PartyDatabase.currentPartyKey = focusedPartyKey;

        PartyDatabase.SaveToJson();
        MainUI.inventoryRenderer.RefreshAllSlotsPartyIndicator();
        RenderFocusedPanel();
        UpdateLeaderSkillInfo(party);
    }

    public void SelectTable(int panelIndex, int slotIndex)
    {
        List<int> partyKeys = PartyDatabase.GetPartyKeysOrdered();
        if (panelIndex < 0 || panelIndex >= partyKeys.Count) return;

        currentEditPartyKey = partyKeys[panelIndex];
        currentUnitIndex = slotIndex;

        MainUI.unitParty.SetActive(false);
        MainUI.unitList.SetActive(true);
        InventoryRenderer.selectionMode = InventorySelectionMode.UnitPartySelect;
        MainUI.inventoryRenderer.DarkenUnclickableSlots();
    }

    private static bool HasAtLeastOneUnit(PartyData party)
    {
        for (int i = 0; i < 5; i++)
            if (party.GetUnitAt(i) != -1) return true;
        return false;
    }

    public static void UpdateView()
    {
        RenderFocusedPanel();
        RefreshFocusedSkillInfo();
    }

    private static void RefreshFocusedSkillInfo()
    {
        List<int> partyKeys = PartyDatabase.GetPartyKeysOrdered();
        if (currentFocusedPanelIndex < 0 || currentFocusedPanelIndex >= partyKeys.Count) return;

        PartyData party = PartyDatabase.GetParty(partyKeys[currentFocusedPanelIndex]);
        UpdateLeaderSkillInfo(party);
    }

    private static void RenderFocusedPanel()
    {
        List<int> partyKeys = PartyDatabase.GetPartyKeysOrdered();
        if (currentFocusedPanelIndex < 0 || currentFocusedPanelIndex >= partyKeys.Count) return;

        PartyData party = PartyDatabase.GetParty(partyKeys[currentFocusedPanelIndex]);
        PartyPanel panel = partyPanels[currentFocusedPanelIndex];

        Debug.Log($"[PartyEditMenu] RenderFocusedPanel: panel {currentFocusedPanelIndex}, partyKey {partyKeys[currentFocusedPanelIndex]}");

        RefreshUnitTable(panel, party);
        SetLeaderSamActive(panel, party);
    }

    private static void RefreshUnitTable(PartyPanel panel, PartyData party)
    {
        for (int i = 0; i < 5; i++)
        {
            int unitKey = party.GetUnitAt(i);
            UnitTableRenderer renderer = panel.unitTableRenderers[i];

            panel.lastRenderedUnitKeys[i] = unitKey;

            if (unitKey == -1)
            {
                renderer.ClearUnit();
                panel.baseUnitDetails[i].gameObject.SetActive(false);
                continue;
            }

            UnitInventoryData unitData =
                PlayerUnitInventoryDatabase.GetUnitByKey(unitKey);

            if (unitData == null)
                continue;

            // Il pannello/renderer potrebbe essere ancora inattivo
            if (!renderer.gameObject.activeInHierarchy)
                continue;

            renderer.SetUnit(unitData, false);

            panel.baseUnitDetails[i].unitRenderer = renderer;
            panel.baseUnitDetails[i].gameObject.SetActive(true);
            panel.baseUnitDetails[i].UpdateDetails();
        }
    }

    public static void UpdateLeaderSkillInfo(PartyData party)
    {
        int leaderUnitKey = party.GetUnitAt(party.leaderUnitIndex);

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

    public static void SetLeaderSamActive(PartyPanel panel, PartyData party)
    {
        for (int i = 0; i < 5; i++)
            panel.leaderSams[i].SetActive(i == party.leaderUnitIndex);
    }

    private static void ResetRenderCache(PartyPanel panel)
    {
        panel.lastRenderedUnitKeys = new int[] { -2, -2, -2, -2, -2 };
    }
}

[System.Serializable]
public class PartyPanel
{
    public List<UnitTableRenderer> unitTableRenderers;
    public List<Button> tableButtons;
    public List<BaseUnitDetails> baseUnitDetails;
    public List<GameObject> leaderSams;

    [System.NonSerialized] public int[] lastRenderedUnitKeys = { -2, -2, -2, -2, -2 };
}