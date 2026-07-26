using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class SummonDoorAnimations : MonoBehaviour
{
    public AudioClip startSummon;
    public AudioClip doorAppear;
    public AudioClip doorOpen;
    public AudioClip doorBreak;
    public Unit summonedUnit;
    public GameObject touch;
    public GameObject closeDoor;
    public GameObject closeDoorAdd;
    public GameObject openDoor;
    public GameObject openDoorAdd;
    public GameObject glareAndParticles;
    public GameObject whiteFlash;
    [SerializeField]
    public List<DoorBreakEntry> doorBreaks = new List<DoorBreakEntry>();

    private Button button;
    private Unit pulled;
    private Unit evo;
    private bool isNewUnit;

    // Se impostato, questa porta "finta" farà sempre un door break
    // verso surpriseTarget invece di aprirsi normalmente, anche se
    // pulled.unitId == evo.unitId (caso "surprise door break").
    private SummonDoorAnimations surpriseTarget;
    private string surpriseDoorBreakKey;

    void Awake()
    {
        button = GetComponent<Button>();
        button.onClick.AddListener(() => OpenDoor());
    }

    public void Play(Unit pulled, Unit evo, bool isNewUnit, SummonDoorAnimations surpriseTarget = null, string surpriseDoorBreakKey = null)
    {
        SoundManager.Instance.SetMusicVolume(0.5f);
        SoundManager.Instance.PlaySound(startSummon);
        this.isNewUnit = isNewUnit;
        this.pulled = pulled;
        this.evo = evo;
        this.surpriseTarget = surpriseTarget;
        this.surpriseDoorBreakKey = surpriseDoorBreakKey;
        button.enabled = true;
        this.summonedUnit = pulled;
        closeDoor.SetActive(true);
        closeDoor.GetComponent<SamAnimator>().SetAnimation("start", false);
        StartCoroutine(SwitchCloseAnimation());

        openDoor.SetActive(false);
        openDoorAdd.SetActive(false);
    }

    public void PlayAfterDoorBreak(Unit unit)
    {
        pulled = unit;
        evo = unit;
        surpriseTarget = null;

        button.enabled = true;
        this.summonedUnit = unit;
        closeDoor.SetActive(true);
        closeDoor.GetComponent<SamAnimator>().SetAnimation("loop");
        
        openDoor.SetActive(false);
        openDoorAdd.SetActive(false);
    }

    IEnumerator StartDoorBreak(Unit pulled, Unit evo, SummonDoorAnimations forcedTarget = null)
    {
        GameObject doorBreak = null;
        SummonDoorAnimations doorToActivate = null;

        if(forcedTarget != null)
        {
            doorToActivate = forcedTarget;
            doorBreak = GetDoorBreak(surpriseDoorBreakKey);
        }
        else {
            if(pulled.rarity == UnitRarity.THREE && evo.rarity == UnitRarity.FOUR)
            {
                doorBreak = GetDoorBreak("GoldToRed");
                doorToActivate = MainUI.superRareDoorAnim;
            }
            else if (pulled.rarity == UnitRarity.THREE && (evo.rarity == UnitRarity.FIVE || evo.rarity == UnitRarity.SIX))
            {
                doorBreak = GetDoorBreak("GoldToRainbow");
                doorToActivate = MainUI.megaRareDoorAnim;
            }
            else if (pulled.rarity == UnitRarity.FOUR && (evo.rarity == UnitRarity.FIVE || evo.rarity == UnitRarity.SIX))
            {
                doorBreak = GetDoorBreak("RedToRainbow");
                doorToActivate = MainUI.megaRareDoorAnim;
            }
            else if ((pulled.rarity == UnitRarity.FIVE || pulled.rarity == UnitRarity.SIX) && (evo.rarity == UnitRarity.SEVEN || evo.rarity == UnitRarity.OMNI))
            {
                doorBreak = GetDoorBreak("RainbowToBlack");
                doorToActivate = MainUI.ultraRareDoorAnim;
            }
        }
        
        if(doorBreak == null || doorToActivate == null)
        {
            Debug.Log("Did this fallback?");
            Deactivate();
            yield break;
        }

        doorBreak.SetActive(true);
        doorBreak.GetComponent<SamAnimator>().SetAnimation("start", false);

        yield return new WaitForSeconds(1.5f);

        doorBreak.SetActive(false);
        Deactivate();
        doorToActivate.gameObject.SetActive(true);
        doorToActivate.PlayAfterDoorBreak(evo);
    }
    
    IEnumerator SwitchCloseAnimation()
    {
        SamAnimator animator = closeDoor.GetComponent<SamAnimator>();
        while (animator.IsPlaying() && animator.GetCurrentAnimation() == "start")
        {
            yield return null;
        }
        touch.SetActive(true);
        touch.GetComponent<SamAnimator>().SetAnimation("loop", true);
        animator.SetAnimation("loop", true);
        if(closeDoorAdd != null)
            closeDoorAdd.SetActive(true);
    }

    void OpenDoor()
    {
        button.enabled = false;

        bool realDoorBreak = pulled.unitId != evo.unitId && pulled.rarity != UnitRarity.ONE && pulled.rarity != UnitRarity.TWO;
        bool surpriseDoorBreak = surpriseTarget != null;

        if(realDoorBreak || surpriseDoorBreak)
        {
            SoundManager.Instance.PlaySound(doorBreak);
            touch.SetActive(false);
            closeDoor.SetActive(false);
            if(closeDoorAdd != null)
                closeDoorAdd.SetActive(false);
            StartCoroutine(StartDoorBreak(pulled, evo, surpriseDoorBreak ? surpriseTarget : null));
            return;
        }

        SoundManager.Instance.PlaySound(doorAppear);
        SoundManager.Instance.PlaySound(doorOpen);

        StartCoroutine(DeactivateAfterOpenAnimation());
        touch.SetActive(false);
        closeDoor.SetActive(false);
        if(closeDoorAdd != null)
            closeDoorAdd.SetActive(false);
        openDoor.SetActive(true);
        openDoor.GetComponent<SamAnimator>().SetAnimation("start", false);
        StartCoroutine(AddParticlesAfterAddAnimation());
        if(openDoorAdd != null)
            StartCoroutine(OpenDoorAddAnimation());
    }

    IEnumerator OpenDoorAddAnimation()
    {
        yield return new WaitForSeconds(0.05f);
        openDoorAdd.SetActive(true);
        openDoorAdd.GetComponent<SamAnimator>().SetAnimation("start", false);
        yield return new WaitForSeconds(1.5f);
        whiteFlash.SetActive(true);
        whiteFlash.GetComponent<SamAnimator>().SetAnimation("start", false);
        while (whiteFlash.GetComponent<SamAnimator>().IsPlaying())
        {
            yield return null;
        }
        glareAndParticles.SetActive(false);
        glareAndParticles.GetComponent<SummonGlareAndParticles>().DestroyAllParticles();
        
        MainUI.newSummonUnitUI.gameObject.SetActive(true);
        MainUI.newSummonUnitUI.Play(PlayerUnitInventoryDatabase.GetUnitByKey(PlayerUnitInventoryDatabase._nextKey-1), new List<GameObject>() { MainUI.summonMenu }, new List<GameObject>() { MainUI.footer }, new List<GameObject>() { MainUI.summonMenu, MainUI.summonScreen });
        
        
        Deactivate();
    }

    IEnumerator AddParticlesAfterAddAnimation()
    {
        yield return new WaitForSeconds(0.05f);
        glareAndParticles.SetActive(true);
        glareAndParticles.GetComponent<SummonGlareAndParticles>().StartStarsAnimation();
        
        yield return new WaitForSeconds(0.6f);
        glareAndParticles.GetComponent<SummonGlareAndParticles>().StartGlaresAnimation();
    }

    IEnumerator DeactivateAfterOpenAnimation()
    {
        SamAnimator animator = openDoor.GetComponent<SamAnimator>();
        while (animator.IsPlaying())
        {
            yield return null;
        }
        openDoor.SetActive(false);
    }

    public void Deactivate()
    {
        SoundManager.Instance.SetMusicVolume(1f);
        touch.SetActive(false);
        closeDoor.SetActive(false);
        if(closeDoorAdd != null)
            closeDoorAdd.SetActive(false);
        openDoor.SetActive(false);
        if(openDoorAdd != null)
            openDoorAdd.SetActive(false);
        glareAndParticles.SetActive(false);
        whiteFlash.SetActive(false);
        surpriseTarget = null;
        gameObject.SetActive(false);
    }

    private GameObject GetDoorBreak(string key)
    {
        foreach (var entry in doorBreaks)
        {
            if (entry.key == key)
                return entry.value;
        }
        return null;
    }

}
[System.Serializable]
public class DoorBreakEntry
{
    public string key;
    public GameObject value;
}