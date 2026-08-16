using UnityEngine;
using UnityEngine.UI;

public class StartSummonButton : MonoBehaviour
{
    public SummonGate summonGate;
    public Button button;

    void Start()
    {
        button.onClick.AddListener(StartSummon);
    }

    public void StartSummon()
    {
        if(!CanAffordSummon()) return;
        PayToSummon();
        MainUI.summonScreen.SetActive(true);
        var summoned = summonGate.Summon();
        PlayDoor(summoned.pulled, summoned.evolved, summoned.isSurprise, summoned.isNewUnit);
    }

    bool CanAffordSummon()
    {
        int currentValue;

        switch (summonGate.summonBanner.costType)
        {
            case CostType.Gems:
                currentValue = PlayerData.gems;
                break;
            case CostType.Zel:
                currentValue = PlayerData.zel;
                break;
            case CostType.Karma:
                currentValue = PlayerData.karma;
                break;
            default:
                return false;
        }
        
        return currentValue >= summonGate.summonBanner.cost;
    }

    void PayToSummon()
    {
        switch (summonGate.summonBanner.costType)
        {
            case CostType.Gems:
                PlayerData.gems -= summonGate.summonBanner.cost;
                break;
            case CostType.Zel:
                PlayerData.zel -= summonGate.summonBanner.cost;
                break;
            case CostType.Karma:
                PlayerData.karma -= summonGate.summonBanner.cost;
                break;
        }

        MainUI.header.GetComponent<HeaderPlayerData>().UpdateHeader();
        PlayerData.SaveDataToJson();
    }

    void PlayDoor(Unit summonedUnit, Unit evolvedUnit, bool isSurprise, bool isNewUnit)
    {
        
        if(isSurprise)
        {
            PlaySurpriseDoor(evolvedUnit, isNewUnit);
            return;
        }

        switch (summonedUnit.rarity)
        {
            case UnitRarity.ONE or UnitRarity.TWO:
                if(summonedUnit.unitId != evolvedUnit.unitId)
                {
                    PlayDoor(evolvedUnit, evolvedUnit, false, isNewUnit);
                    break;
                }
                MainUI.commonDoorAnim.gameObject.SetActive(true);
                MainUI.commonDoorAnim.Play(summonedUnit, evolvedUnit, isNewUnit);
                break;
            case UnitRarity.THREE:
                MainUI.rareDoorAnim.gameObject.SetActive(true);
                MainUI.rareDoorAnim.Play(summonedUnit, evolvedUnit, isNewUnit);
                break;
            case UnitRarity.FOUR:
                MainUI.superRareDoorAnim.gameObject.SetActive(true);
                MainUI.superRareDoorAnim.Play(summonedUnit, evolvedUnit, isNewUnit);
                break;
            case UnitRarity.FIVE or UnitRarity.SIX:
                MainUI.megaRareDoorAnim.gameObject.SetActive(true);
                MainUI.megaRareDoorAnim.Play(summonedUnit, evolvedUnit, isNewUnit);
                break;
            case UnitRarity.SEVEN or UnitRarity.OMNI:
                MainUI.ultraRareDoorAnim.gameObject.SetActive(true);
                MainUI.ultraRareDoorAnim.Play(summonedUnit, evolvedUnit, isNewUnit);
                break;
        }
    }

    // Sceglie una falsa porta di partenza di rarità inferiore, in base
    // alle uniche transizioni di door-break che esistono, poi finisce
    // mostrando la vera unità ottenuta.
    void PlaySurpriseDoor(Unit realUnit, bool isNewUnit)
    {
        switch (realUnit.rarity)
        {
            case UnitRarity.FOUR:
                MainUI.rareDoorAnim.gameObject.SetActive(true);
                MainUI.rareDoorAnim.Play(realUnit, realUnit, isNewUnit, MainUI.superRareDoorAnim, "GoldToRed");
                break;

            case UnitRarity.FIVE or UnitRarity.SIX:
                if(Random.Range(0f, 100f) <= 50f)
                {
                    MainUI.rareDoorAnim.gameObject.SetActive(true);
                    MainUI.rareDoorAnim.Play(realUnit, realUnit, isNewUnit, MainUI.megaRareDoorAnim, "GoldToRainbow");
                }
                else
                {
                    MainUI.superRareDoorAnim.gameObject.SetActive(true);
                    MainUI.superRareDoorAnim.Play(realUnit, realUnit, isNewUnit, MainUI.megaRareDoorAnim, "RedToRainbow");
                }
                break;

            case UnitRarity.SEVEN or UnitRarity.OMNI:
                MainUI.megaRareDoorAnim.gameObject.SetActive(true);
                MainUI.megaRareDoorAnim.Play(realUnit, realUnit, isNewUnit, MainUI.ultraRareDoorAnim, "RainbowToBlack");
                break;

            default:
                PlayDoor(realUnit, realUnit, isNewUnit, false);
                break;
        }
    }
}