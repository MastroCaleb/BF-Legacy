using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class PartyViewUI : MonoBehaviour
{
    public List<Image> unitArtsHelper;
    public static List<Image> unitArts;
    public List<Button> unitButtonsHelper;
    public static List<Button> unitButtons;
    public List<GameObject> elementSamsHelper;
    public static List<GameObject> elementSams;
    public List<TextAsset> elementSamJsonsHelper;
    public static List<TextAsset> elementSamJsons;
    public  List<Image> elementImagesHelper;
    public static List<Image> elementImages;
    public List<Sprite> elementIconsHelper;
    public static List<Sprite> elementIcons;

    static List<GameObject> currentElementSams;
    bool playOnce;
    public static PartyViewUI instance;

    private GameObject coroutineHostGO;

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

    /* 
    Party View Display calc
    unityX = 0.2178*jsonX - 2.435*jsonW - 0.1643*imageW + 420
    unityY = 0.2178*jsonY + 0.4860*jsonH - 0.1643*imageH - 181
    unityW = imageW * (128/jsonW)
    unityH = imageH * (350/jsonH)
    */

    void Awake()
    {
        instance = this;
        unitArts = unitArtsHelper;
        unitButtons = unitButtonsHelper;
        elementIcons = elementIconsHelper;
        elementImages = elementImagesHelper;
        elementSams = elementSamsHelper;
        elementSamJsons = elementSamJsonsHelper;
    }

    void Start()
    {
        UpdatePartyView(playStartAnimation: true);
    }

    public void OpenUnitSummary(int i)
    {
        PartyData party = PartyDatabase.GetParty(0);

        if (party == null)
            return;

        int unitKey = party.GetUnitAt(i);

        if (unitKey == -1)
            return; // empty slot, do nothing

        var unitData = PlayerUnitInventoryDatabase.GetUnitByKey(unitKey);

        if (unitData == null)
            return;

        MainUI.homeMenu.SetActive(false);
        MainUI.unitSummary.gameObject.SetActive(true);
        MainUI.unitSummary.SetUnit(
            unitData,
            new List<GameObject>() { MainUI.homeMenu },
            null
        );
    }

    public void UpdatePartyView(bool playStartAnimation = true)
    {
        if (unitArts == null)
        {
            if (instance == null)
                instance = UnityEngine.Object.FindAnyObjectByType<PartyViewUI>();

            if (instance != null)
            {
                unitArts = instance.unitArtsHelper;
                elementIcons = instance.elementIconsHelper;
                elementImages = instance.elementImagesHelper;
                elementSams = instance.elementSamsHelper;
                elementSamJsons = instance.elementSamJsonsHelper;
            }
            else
            {
                Debug.LogError("PartyViewUI instance not found in scene.");
                return;
            }
        }

        Clear();
        int i = 0;
        foreach (int key in PartyDatabase.GetParty(0).unitKeys)
        {
            var entry = PlayerUnitInventoryDatabase.GetUnitByKey(key);
            Unit unit = entry?.unit;
            if (unit != null)
            {
                unitArts[i].enabled = true;
                unitArts[i].sprite = unit.unitFullArt;
                elementImages[i].enabled = true;
                elementImages[i].sprite = elementIcons[(int)unit.element];
                elementSams[i].SetActive(true);
                elementSams[i].GetComponent<SamAnimator>().enabled = true;
                elementSams[i].GetComponent<SamAnimator>().jsonFile = elementSamJsons[(int)unit.element];
                elementSams[i].GetComponent<SamAnimator>().Reinitialize();

                float imageW = unit.unitFullArt.texture.width;
                float imageH = unit.unitFullArt.texture.height;
                float jsonX = unit.unitDisplayHomePosition.x;
                float jsonY = unit.unitDisplayHomePosition.y;
                float jsonW = unit.unitDisplayHomePosition.width;
                float jsonH = unit.unitDisplayHomePosition.height;

                float unityX = (imageW / 2f - jsonX - jsonW / 2f) * (128f / jsonW);
                float unityY = (jsonY + jsonH / 2f - imageH / 2f) * (350f / jsonH);
                float unityW = imageW * (128f / jsonW);
                float unityH = imageH * (350f / jsonH);

                RectTransform rt = unitArts[i].GetComponent<RectTransform>();
                rt.anchoredPosition = new Vector2(unityX, unityY);
                rt.sizeDelta = new Vector2(unityW, unityH);
            }

            i++;
        }

        for (int j = 0; j < unitButtons.Count; j++)
        {
            int index = j;
            unitButtons[j].onClick.AddListener(() => OpenUnitSummary(index));
        }

        RefreshElementAnimations(playStartAnimation);
    }


    private void RefreshElementAnimations(bool playStartAnimation = true)
    {
        PartyData party = PartyDatabase.GetParty(0);

        for (int i = 0; i < elementSams.Count; i++)
        {
            SamAnimator animator = elementSams[i].GetComponent<SamAnimator>();

            bool hasUnit = party != null
                && i < party.unitKeys.Count
                && party.unitKeys[i] != -1
                && PlayerUnitInventoryDatabase.GetUnitByKey(party.unitKeys[i]) != null;

            animator.enabled = hasUnit;

            if (loopCoroutines.TryGetValue(animator, out Coroutine existing) && existing != null)
                GetOrCreateCoroutineHost().StopCoroutine(existing);
            loopCoroutines.Remove(animator);

            if (!hasUnit)
                continue;

            if (playStartAnimation)
            {
                loopCoroutines[animator] = GetOrCreateCoroutineHost().StartCoroutine(SetToLoopAfterStartAnimation(animator));
            }
            else
            {
                animator.SetAnimation("loop");
            }
        }
    }

    public static void Clear()
    {
        for(int i = 0; i < 5; i++)
        {
            unitArts[i].sprite = null;
            unitArts[i].enabled = false;
            elementImages[i].sprite = null;
            elementImages[i].enabled = false;
            elementSams[i].GetComponent<SamAnimator>().enabled = false;
            elementSams[i].SetActive(false);
        }
    }

    private Dictionary<SamAnimator, Coroutine> loopCoroutines = new Dictionary<SamAnimator, Coroutine>();

    IEnumerator SetToLoopAfterStartAnimation(SamAnimator animator)
    {
        yield return new WaitUntil(() => animator.GetCurrentAnimation() == "start");

        yield return new WaitUntil(() => !animator.IsPlaying());

        animator.SetAnimation("loop");

    }

    void OnEnable()
    {
        unitArts = unitArtsHelper;
        elementIcons = elementIconsHelper;
        elementImages = elementImagesHelper;
        elementSams = elementSamsHelper;
        elementSamJsons = elementSamJsonsHelper;

        RefreshElementAnimations();
    }

    void OnDisable()
    {
        foreach (var sam in elementSams)
        {
            SamAnimator animator = sam.GetComponent<SamAnimator>();
            if (loopCoroutines.TryGetValue(animator, out Coroutine existing) && existing != null && coroutineHostGO != null)
                GetOrCreateCoroutineHost().StopCoroutine(existing);
        }
        loopCoroutines.Clear();
    }
}