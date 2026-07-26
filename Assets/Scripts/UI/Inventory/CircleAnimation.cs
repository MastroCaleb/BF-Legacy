using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class CircleAnimation : MonoBehaviour
{
    public static bool isEvo = false;
    public int baseUnitKey;

    public UnitTableRenderer baseUnitRenderer;
    public List<UnitTableRenderer> materialUnitRenderers;
    public UnitTableRenderer endUnitRenderer;

    public SamAnimator circleSet;
    public SamAnimator circleSetAdd;

    public List<SamAnimator> convCircleAdds;

    public GameObject activateAfterEnd;

    public SamAnimator successSam;
    public SamAnimator successSamAdd;
    public SamAnimator greatSuccessSam;
    public SamAnimator greatSuccessSamAdd;
    public SamAnimator superSuccessSam;
    public SamAnimator superSuccessSamAdd;

    public Button nextButton;

    public float successAutoProceedDelay = 1f;

    private Unit evolvedUnit;

    int next = 0;
    Coroutine autoProceedCoroutine;
    GameObject coroutineHostGO;
    int playSession = 0;
    bool isNew = false;

    public IEnumerator Play(int baseUnitKey, List<Unit> materialUnits, bool isEvo, bool isNew = false)
    {
        nextButton.onClick.RemoveAllListeners();
        nextButton.onClick.AddListener(NextButton);
        int thisSession = ++playSession;

        CircleAnimation.isEvo = isEvo;
        this.baseUnitKey = baseUnitKey;
        next = 0;
        this.isNew = isNew;
        CancelAutoProceed();
        HideAllSuccessBanners();

        UnitInventoryData baseUnit = PlayerUnitInventoryDatabase.GetUnitByKey(baseUnitKey);
        evolvedUnit = UnitRegistry.GetUnitById(baseUnit.unit.evoInto);
        baseUnitRenderer.SetUnit(baseUnit, false);

        for (int i = 0; i < materialUnits.Count; i++)
            materialUnitRenderers[i].SetUnit(materialUnits[i], false);

        circleSet.gameObject.SetActive(true);
        circleSet.SetAnimation("start");
        circleSetAdd.gameObject.SetActive(true);
        circleSetAdd.SetAnimation("start");

        foreach (SamAnimator convCircle in convCircleAdds)
        {
            convCircle.gameObject.SetActive(true);
            convCircle.SetAnimation("loop");
        }

        yield return new WaitForSeconds(3);
        if (thisSession != playSession) yield break;

        foreach (UnitTableRenderer unitTableRenderer in materialUnitRenderers)
            unitTableRenderer.ClearUnit();

        yield return new WaitForSeconds(1);
        if (thisSession != playSession) yield break;

        foreach (SamAnimator convCircle in convCircleAdds)
            convCircle.gameObject.SetActive(false);

        yield return new WaitForSeconds(2);
        if (thisSession != playSession) yield break;

        circleSet.gameObject.SetActive(false);
        circleSetAdd.gameObject.SetActive(false);

        activateAfterEnd.SetActive(true);
        HideAllSuccessBanners();
        next++;
        ShowResult(isEvo);

        gameObject.SetActive(false);
    }

    // ── HELPERS ──────────────────────────────────────────────────────────────

    void HideAllSuccessBanners()
    {
        if (successSam == null) return;

        successSam.gameObject.SetActive(false);
        successSamAdd.gameObject.SetActive(false);
        greatSuccessSam.gameObject.SetActive(false);
        greatSuccessSamAdd.gameObject.SetActive(false);
        superSuccessSam.gameObject.SetActive(false);
        superSuccessSamAdd.gameObject.SetActive(false);
    }

    void CancelAutoProceed()
    {
        if (autoProceedCoroutine != null)
        {
            if (coroutineHostGO != null)
                coroutineHostGO.GetComponent<CoroutineHost>()?.StopCoroutine(autoProceedCoroutine);
            autoProceedCoroutine = null;
        }
    }

    MonoBehaviour GetOrCreateCoroutineHost()
    {
        if (coroutineHostGO == null)
        {
            coroutineHostGO = new GameObject($"_CoroutineHost_{gameObject.name}");
            DontDestroyOnLoad(coroutineHostGO);
        }
        var host = coroutineHostGO.GetComponent<CoroutineHost>();
        if (host == null) host = coroutineHostGO.AddComponent<CoroutineHost>();
        return host;
    }

    void ShowSuccessBanners()
    {
        CancelAutoProceed();

        if (FusionMenu.lastSuccessType == SuccessType.Success)
        {
            successSam.gameObject.SetActive(true);
            successSamAdd.gameObject.SetActive(true);
            successSam.SetAnimation("start", false);
            successSamAdd.SetAnimation("start", false);
        }
        else if (FusionMenu.lastSuccessType == SuccessType.GreatSuccess)
        {
            greatSuccessSam.gameObject.SetActive(true);
            greatSuccessSamAdd.gameObject.SetActive(true);
            greatSuccessSam.SetAnimation("start", false);
            greatSuccessSamAdd.SetAnimation("start", false);
        }
        else if (FusionMenu.lastSuccessType == SuccessType.SuperSuccess)
        {
            superSuccessSam.gameObject.SetActive(true);
            superSuccessSamAdd.gameObject.SetActive(true);
            superSuccessSam.SetAnimation("start", false);
            superSuccessSamAdd.SetAnimation("start", false);
        }

        autoProceedCoroutine = GetOrCreateCoroutineHost().StartCoroutine(AutoProceedAfterSuccess());
    }

    IEnumerator AutoProceedAfterSuccess()
    {
        int thisSession = playSession;
        yield return new WaitForSeconds(successAutoProceedDelay);
        if (thisSession != playSession) yield break;
        if (next != 1 || isEvo) yield break;
        autoProceedCoroutine = null;
        NextButton();
    }

    void ShowResult(bool evo)
    {
        UnitInventoryData baseUnit = PlayerUnitInventoryDatabase.GetUnitByKey(baseUnitKey);

        if (evo)
        {
            endUnitRenderer.SetUnit(evolvedUnit, false);
        }
        else
        {
            endUnitRenderer.SetUnit(baseUnit, false);
            ShowSuccessBanners();
        }
    }

    void SkipToResult(bool evo)
    {
        playSession++;
        StopAllCoroutines();
        CancelAutoProceed();
        next = 0;

        foreach (UnitTableRenderer r in materialUnitRenderers)
            r.ClearUnit();

        foreach (SamAnimator convCircle in convCircleAdds)
            convCircle.gameObject.SetActive(false);

        circleSet.gameObject.SetActive(false);
        circleSetAdd.gameObject.SetActive(false);
        
        activateAfterEnd.SetActive(true);

        HideAllSuccessBanners();
        next++;
        ShowResult(evo);

        gameObject.SetActive(false);
    }

    // ── BUTTON ───────────────────────────────────────────────────────────────

    public void NextButton()
    {
        if (isEvo)
        {
            if (next == 0)
            {
                SkipToResult(true);
                MainUI.fuseAndEvoText.gameObject.SetActive(true);
            }
            else if (next == 1)
            {
                next = 0;
                MainUI.endCircleSam.SetActive(false);
                MainUI.fuseAndEvoAnimations.SetActive(false);
                MainUI.unitEvo.SetActive(false);
                MainUI.unitFusion.SetActive(false);
                MainUI.fuseAndEvoText.gameObject.SetActive(false);
                MainUI.levelUpDetailsView.gameObject.SetActive(false);
                
                UnitInventoryData baseUnit = PlayerUnitInventoryDatabase.GetUnitByKey(baseUnitKey);
                bool hasEvo = baseUnit.unit.evoInto != null && baseUnit.unit.evoInto != "";

                if (!isNew)
                {
                    MainUI.unitSummary.gameObject.SetActive(true);
                    MainUI.unitSummary.SetUnit(
                        baseUnit,
                        new List<GameObject>() { hasEvo ? MainUI.unitEvo : MainUI.unitMenu },
                        null
                    );

                    MainUI.footer.SetActive(true);
                    MainUI.header.SetActive(true);
                    MainUI.extensionLow.SetActive(true);
                    MainUI.extensionUp.SetActive(true);
                }
                else
                {
                    MainUI.newSummonUnitUI.gameObject.SetActive(true);

                    MainUI.newSummonUnitUI.Play(baseUnit, new List<GameObject>() { hasEvo ? MainUI.unitEvo : MainUI.unitMenu }, new List<GameObject>() { MainUI.footer, MainUI.header, MainUI.extensionLow, MainUI.extensionUp}, null);
                }
            }
        }
        else
        {
            if (successSam == null) return; // istanza evo — non deve gestire il flusso fusione

            if (next == 0)
            {
                SkipToResult(false);
            }
            else if (next == 1)
            {
                CancelAutoProceed();
                HideAllSuccessBanners();

                MainUI.levelUpDetailsView.gameObject.SetActive(true);
                MainUI.levelUpDetailsView.UpdateView(baseUnitKey);
                MainUI.fuseAndEvoText.gameObject.SetActive(true);
                next++;
            }
            else
            {
                next = 0;

                CancelAutoProceed();
                HideAllSuccessBanners();

                MainUI.endCircleSam.SetActive(false);
                MainUI.fuseAndEvoAnimations.SetActive(false);
                MainUI.unitEvo.SetActive(false);
                MainUI.fuseAndEvoText.gameObject.SetActive(false);
                MainUI.unitFusion.SetActive(true);
                MainUI.footer.SetActive(true);
                MainUI.header.SetActive(true);
                MainUI.extensionLow.SetActive(true);
                MainUI.extensionUp.SetActive(true);
                MainUI.levelUpDetailsView.gameObject.SetActive(false);
                MainUI.unitSummary.gameObject.SetActive(false);
            }
        }
    }
}