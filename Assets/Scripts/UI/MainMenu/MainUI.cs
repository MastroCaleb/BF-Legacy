using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class MainUI : MonoBehaviour
{
    public GameObject homeMenuHelper;
    public static GameObject homeMenu;
    public GameObject unitMenuHelper;
    public static GameObject unitMenu;
    public GameObject townMenuHelper;
    public static GameObject townMenu;
    public GameObject shopMenuHelper;
    public static GameObject shopMenu;
    public GameObject summonMenuHelper;
    public static GameObject summonMenu;
    public GameObject socialMenuHelper;
    public static GameObject socialMenu;
    public GameObject mapMenuHelper;
    public static GameObject mapMenu;
    public GameObject missionSelectionHelper;
    public static GameObject missionSelection;
    public GameObject mapNameHelper;
    public static GameObject mapName;
    public GameObject mapDungeonsHelper;
    public static GameObject mapDungeons;
    public GameObject vortexMenuHelper;
    public static GameObject vortexMenu;
    public GameObject rewardsScreenHelper;
    public static GameObject rewardsScreen;

    public UnitSummary unitSummaryHelper;
    public static UnitSummary unitSummary;

    public GameObject unitListHelper;
    public static GameObject unitList;
    public GameObject unitFusionHelper;
    public static GameObject unitFusion;
    public GameObject unitEvoHelper;
    public static GameObject unitEvo;
    public GameObject unitPartyHelper;
    public static GameObject unitParty;
    public InventoryRenderer inventoryRendererHelper;
    public static InventoryRenderer inventoryRenderer;

    public GameObject footerHelper;
    public static GameObject footer;
    public GameObject headerHelper;
    public static GameObject header;

    public GameObject extensionLowHelper;
    public static GameObject extensionLow;
    public GameObject extensionUpHelper;
    public static GameObject extensionUp;

    public GameObject fuseAndEvoAnimationsHelper;
    public static GameObject fuseAndEvoAnimations;
    public TextMeshProUGUI fuseAndEvoTextHelper;
    public static TextMeshProUGUI fuseAndEvoText;

    public GameObject endCircleSamHelper;
    public static GameObject endCircleSam;
    public GameObject evoCircleSamHelper;
    public static GameObject evoCircleSam;
    public GameObject fusionCircleSamHelper;
    public static GameObject fusionCircleSam;
    public LevelUpDetailsUI levelUpDetailsViewHelper;
    public static LevelUpDetailsUI levelUpDetailsView;

    public GameObject summonScreenHelper;
    public static GameObject summonScreen;
    public SummonDoorAnimations commonDoorAnimHelper;
    public static SummonDoorAnimations commonDoorAnim;
    public SummonDoorAnimations rareDoorAnimHelper;
    public static SummonDoorAnimations rareDoorAnim;
    public SummonDoorAnimations superRareDoorAnimHelper;
    public static SummonDoorAnimations superRareDoorAnim;
    public SummonDoorAnimations megaRareDoorAnimHelper;
    public static SummonDoorAnimations megaRareDoorAnim;
    public SummonDoorAnimations ultraRareDoorAnimHelper;
    public static SummonDoorAnimations ultraRareDoorAnim;
    public NewSummonUnitUI newSummonUnitUIHelper;
    public static NewSummonUnitUI newSummonUnitUI;


    [SerializeField]
    public Image fadeImageHelper;
    public static Image fadeImage;

    void Awake()
    {
        ExperienceTable.LoadAll();
        homeMenu = homeMenuHelper;
        unitMenu = unitMenuHelper;
        townMenu = townMenuHelper;
        shopMenu = shopMenuHelper;
        summonMenu = summonMenuHelper;
        socialMenu = socialMenuHelper;
        rewardsScreen = rewardsScreenHelper;
        mapMenu = mapMenuHelper;
        missionSelection = missionSelectionHelper;
        mapName = mapNameHelper;
        mapDungeons = mapDungeonsHelper;
        vortexMenu = vortexMenuHelper;

        unitFusion = unitFusionHelper;
        unitEvo = unitEvoHelper;
        unitParty = unitPartyHelper;
        unitList = unitListHelper;

        unitSummary = unitSummaryHelper;

        footer = footerHelper;
        header = headerHelper;

        inventoryRenderer = inventoryRendererHelper;

        extensionLow = extensionLowHelper;
        extensionUp = extensionUpHelper;

        fuseAndEvoAnimations = fuseAndEvoAnimationsHelper;
        fuseAndEvoText = fuseAndEvoTextHelper;

        endCircleSam = endCircleSamHelper;
        evoCircleSam = evoCircleSamHelper;
        fusionCircleSam = fusionCircleSamHelper;
        levelUpDetailsView = levelUpDetailsViewHelper;

        summonScreen = summonScreenHelper;
        commonDoorAnim = commonDoorAnimHelper;
        rareDoorAnim = rareDoorAnimHelper;
        superRareDoorAnim = superRareDoorAnimHelper;
        megaRareDoorAnim = megaRareDoorAnimHelper;
        ultraRareDoorAnim = ultraRareDoorAnimHelper;
        newSummonUnitUI = newSummonUnitUIHelper;

        fadeImage = fadeImageHelper;
    }
}
