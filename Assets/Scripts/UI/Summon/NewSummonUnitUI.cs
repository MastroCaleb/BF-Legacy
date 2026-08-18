using System.Collections;
using System.Collections.Generic;
using System.Security.Cryptography;
using TMPro;
using Unity.Burst.Intrinsics;
using UnityEngine;
using UnityEngine.UI;

public class NewSummonUnitUI : MonoBehaviour
{    
    public UnitInventoryData unit;
    public Image smallUnitArt;
    public Image largeUnitArt;
    public TextMeshProUGUI unitPhraseText;
    public RectTransform smallUnitAnchor;

    [Header("Summon Bob Animation")]
    public float bobAmplitude = 20f;
    public float bobSpeed = 2f;
    private Coroutine bobCoroutine;
    private float smallArtBaseY;

    public AudioClip commonPull;
    public GameObject commonGetText;
    public GameObject commonGetTextAdd;
    public GameObject commonCircleEffect;
    public GameObject commonCircleEffectAdd;
    public ParticleSystem commonParticle;

    public AudioClip rarePull;
    public GameObject rareGetText;
    public GameObject rareGetTextAdd;
    public GameObject rareCircleEffect;
    public GameObject rareCircleEffectAdd;
    public ParticleSystem rareParticle;

    public AudioClip superRarePull;
    public GameObject superRareGetText;
    public GameObject superRareGetTextAdd;
    public GameObject superRareCircleEffect;
    public GameObject superRareCircleEffectAdd;
    public ParticleSystem superRareParticle;
    

    public AudioClip megaRarePull;
    public GameObject megaRareGetText;
    public GameObject megaRareGetTextAdd;
    public GameObject megaRareCircleEffect;
    public GameObject megaRareCircleEffectAdd;
    public ParticleSystem megaRareParticle;

    public AudioClip ultraRarePull;
    public GameObject ultraRareGetText;
    public GameObject ultraRareGetTextAdd;
    public GameObject ultraRareCircleEffect;
    public GameObject ultraRareCircleEffectAdd;
    public ParticleSystem ultraRareParticle;

    public GameObject whiteFlash;

    private List<GameObject> toActivateOnBack;
    private List<GameObject> toActivateOnNext;
    private List<GameObject> toDeactivateOnNext;

    public void Play(UnitInventoryData unit, List<GameObject> toActivateOnBack, List<GameObject> toActivateOnNext, List<GameObject> toDeactivateOnNext)
    {
        this.toActivateOnBack = toActivateOnBack;
        this.toActivateOnNext = toActivateOnNext;
        this.toDeactivateOnNext = toDeactivateOnNext;
        this.unit = unit;
        SetUnitArt();
        unitPhraseText.text = unit.unit.summonDesc;
        switch (unit.unit.rarity)
        {
            case UnitRarity.ONE or UnitRarity.TWO:
                SoundManager.Instance.PlaySound(commonPull);
                PlayAnim(commonGetText, commonGetTextAdd, commonCircleEffect, commonCircleEffectAdd, commonParticle);
                break;
            case UnitRarity.THREE:
                SoundManager.Instance.PlaySound(rarePull);
                PlayAnim(rareGetText, rareGetTextAdd, rareCircleEffect, rareCircleEffectAdd, rareParticle);
                break;
            case UnitRarity.FOUR:
                SoundManager.Instance.PlaySound(superRarePull);
                PlayAnim(superRareGetText, superRareGetTextAdd, superRareCircleEffect, superRareCircleEffectAdd, superRareParticle);
                break;
            case UnitRarity.FIVE or UnitRarity.SIX:
                SoundManager.Instance.PlaySound(megaRarePull);
                PlayAnim(megaRareGetText, megaRareGetTextAdd, megaRareCircleEffect, megaRareCircleEffectAdd, megaRareParticle);
                break;
            case UnitRarity.SEVEN or UnitRarity.OMNI:
                SoundManager.Instance.PlaySound(ultraRarePull);
                PlayAnim(ultraRareGetText, ultraRareGetTextAdd, ultraRareCircleEffect, ultraRareCircleEffectAdd, ultraRareParticle);
                break;
        }
    }

    public void PlayAnim(GameObject getText, GameObject getTextAdd, GameObject circleEffect, GameObject circleEffectAdd, ParticleSystem particle)
    {
        whiteFlash.SetActive(true);
        whiteFlash.GetComponent<SamAnimator>().SetAnimation("start", false);
        getText.SetActive(true);
        getTextAdd.SetActive(true);
        getText.GetComponent<SamAnimator>().SetAnimation("start", false);
        getTextAdd.GetComponent<SamAnimator>().SetAnimation("start", false);
        StartCoroutine(SwitchTextAnimation(getText));
        StartCoroutine(SwitchTextAnimation(getTextAdd));
        circleEffect.SetActive(true);
        circleEffectAdd.SetActive(true);
        particle.gameObject.SetActive(true);
        particle.Play();
    }

    public void UpdateView()
    {
        SetUnitArt();
    }

    public void SetUnitArt()
    {
        smallUnitArt.sprite = unit.unit.unitFullArt;
        largeUnitArt.sprite = unit.unit.unitFullArt;

        const float CANVAS_W = 640f;
        const float CANVAS_H = 1136f;

        float jsonX = unit.unit.unitDisplaySummonPosition.x;
        float jsonY = unit.unit.unitDisplaySummonPosition.y;
        float jsonW = unit.unit.unitDisplaySummonPosition.width;
        float jsonH = unit.unit.unitDisplaySummonPosition.height;

        float unityX = jsonX + jsonW / 2f - CANVAS_W / 2f;
        float unityY = CANVAS_H / 2f - jsonY - jsonH / 2f;
        float unityW = jsonW;
        float unityH = jsonH;

        RectTransform smallRt = smallUnitArt.GetComponent<RectTransform>();

        if (bobCoroutine != null)
        {
            StopCoroutine(bobCoroutine);
            bobCoroutine = null;
        }

        ApplyDisplayRect(smallRt, unityX, -208.5f, unityW, unityH);
        ApplyDisplayRect(largeUnitArt.GetComponent<RectTransform>(), unityX, unityY, unityW, unityH);

        // This is the correct resting/lowest-point Y for small art, captured after being set above.
        smallArtBaseY = smallRt.anchoredPosition.y;

        if (unit.unit.unitDisplaySummonPosition.other == 1)
        {
            bobCoroutine = StartCoroutine(BobSmallArt(smallRt));
        }
    }

    private IEnumerator BobSmallArt(RectTransform rt)
    {
        while (true)
        {
            // Ranges 0 -> amplitude -> 0, never negative, so baseY is always the floor.
            float offset = bobAmplitude * (0.5f * (1f - Mathf.Cos(Time.time * bobSpeed)));
            Vector2 pos = rt.anchoredPosition;
            pos.y = smallArtBaseY + offset;
            rt.anchoredPosition = pos;
            yield return null;
        }
    }

    private void ApplyDisplayRect(RectTransform rt, float x, float y, float w, float h)
    {
        rt.anchoredPosition = new Vector2(x, y);
        rt.sizeDelta = new Vector2(w, h);
    }

    IEnumerator SwitchTextAnimation(GameObject sam)
    {
        SamAnimator animator = sam.GetComponent<SamAnimator>();
        while (animator.IsPlaying() && animator.GetCurrentAnimation() == "start")
        {
            yield return null;
        }
        sam.SetActive(true);
        animator.SetAnimation("loop", true);
    }

    public void NextButton()
    {
        Deactivate();
        MainUI.unitSummary.gameObject.SetActive(true);
        
        if(toDeactivateOnNext != null)
        {
            foreach(var g in toDeactivateOnNext)
            {
                g.SetActive(false);
            }
        }

        if(toActivateOnNext != null)
        {
            foreach(var g in toActivateOnNext)
            {
                g.SetActive(true);
            }
        }

        MainUI.unitSummary.SetUnit(unit, toActivateOnBack, null);
        
        gameObject.SetActive(false);
    }

    public void Deactivate()
    {
        if (bobCoroutine != null)
        {
            StopCoroutine(bobCoroutine);
            bobCoroutine = null;
        }

        smallUnitArt.sprite = null;
        largeUnitArt.sprite = null;

        commonGetText.SetActive(false);
        commonGetTextAdd.SetActive(false);
        commonCircleEffect.SetActive(false);
        commonCircleEffectAdd.SetActive(false);
        commonParticle.gameObject.SetActive(false);

        rareGetText.SetActive(false);
        rareGetTextAdd.SetActive(false);
        rareCircleEffect.SetActive(false);
        rareCircleEffectAdd.SetActive(false);
        rareParticle.gameObject.SetActive(false);

        superRareGetText.SetActive(false);
        superRareGetTextAdd.SetActive(false);
        superRareCircleEffect.SetActive(false);
        superRareCircleEffectAdd.SetActive(false);
        superRareParticle.gameObject.SetActive(false);

        megaRareGetText.SetActive(false);
        megaRareGetTextAdd.SetActive(false);
        megaRareCircleEffect.SetActive(false);
        megaRareCircleEffectAdd.SetActive(false);
        megaRareParticle.gameObject.SetActive(false);

        ultraRareGetText.SetActive(false);
        ultraRareGetTextAdd.SetActive(false);
        ultraRareCircleEffect.SetActive(false);
        ultraRareCircleEffectAdd.SetActive(false);
        ultraRareParticle.gameObject.SetActive(false);

        whiteFlash.SetActive(false);
    }
}