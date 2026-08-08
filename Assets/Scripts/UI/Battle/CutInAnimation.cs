using System.Collections;
using System.Collections.Generic;
using TMPro;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.UI;

public class CutInAnimation : MonoBehaviour
{
    [Header("Cut-In Elements")]
    public Image cutInBackground;
    public Image cutInCharacter;
    public GameObject skillNameDisplay;
    public GameObject windEffect;
    public GameObject samEffect;
    public GameObject samEffectAdd;
    //SBB
    public GameObject samEffectSBB;
    public GameObject samEffectAddSBB;


    [Header("Animation Settings")]
    //Background Animation Settings
    public float backgroundTransparency = 0.75f;
    public float backgroundFadeDuration = 0.3f;
    public float backgroundHoldDuration = 0.5f;
    private Coroutine backgroundCoroutine;
    //Character Animation Settings
    public Vector3 characterStartPos;
    public float characterStartSpeed;
    public Vector3 characterMidPos;
    public float characterMidSpeed;
    public float characterMidHoldDuration = 0.2f;
    public Vector3 characterEndPos;
    public float characterEndSpeed;
    private Coroutine characterCoroutine;
    //Skill Name Display Settings
    public Vector3 skillNameStartPos;
    public Vector3 skillNameEndPos;
    public float skillNameSpeed;
    public float skillNameHoldDuration = 0.8f;
    public float skillNameFadeDuration = 0.25f;
    private Coroutine skillNameCoroutine;
    private Coroutine cutInCoroutine;

    // Wind effect settings (moved/controlled by cut-in)
    public Vector3 windStartPos = new Vector3(-20f, 0f, 0f);
    public Vector3 windEndPos = new Vector3(20f, 0f, 0f);
    public float windSpeed = 100f; // units per second
    public bool windLoop = true; // whether the wind effect resets to start and continues
    public bool deactivateWindAfterFinish = true; // whether to deactivate after finish
    private Coroutine windCoroutine;
    public static bool isPlaying;
    CutInType currentCutInType = CutInType.Normal;


    void Start()
    {
        // Optionally start cut-in on start for testing
        //PlayCutIn();
    }

    private Queue<(Sprite sprite, string skillName, CutInType cutInType)> cutInQueue = new Queue<(Sprite, string, CutInType)>();
    private bool isProcessingQueue = false;

    public void PlayCutIn(Sprite illustrationSprite, string skillNameText, CutInType cutInType)
    {
        cutInQueue.Enqueue((illustrationSprite, skillNameText, cutInType));
        if (!isProcessingQueue)
            StartCoroutine(ProcessCutInQueue());
    }

    private IEnumerator ProcessCutInQueue()
    {
        isProcessingQueue = true;

        while (cutInQueue.Count > 0)
        {
            var (sprite, skillName, cutInType) = cutInQueue.Dequeue();

            isPlaying = true;
            cutInCharacter.sprite = sprite;
            currentCutInType = cutInType;
            skillNameDisplay.GetComponentInChildren<TextMeshProUGUI>().text = skillName;

            Time.timeScale = 0f;
            cutInCoroutine = StartCoroutine(FullCutInRoutine());

            yield return new WaitForSeconds(0.25f);
        }

        isProcessingQueue = false;
    }

    void BackgroundAnimation()
    {
        if (backgroundCoroutine != null)
            StopCoroutine(backgroundCoroutine);
        backgroundCoroutine = StartCoroutine(BackgroundFadeRoutine());
    }

    void StartWind()
    {
        if (windEffect == null)
            return;

        if (windCoroutine != null)
            StopCoroutine(windCoroutine);
        windCoroutine = StartCoroutine(WindRoutine());
    }

    private IEnumerator WindRoutine()
    {
        RectTransform wrt = windEffect.GetComponent<RectTransform>();
        bool isUI = wrt != null;

        // Ensure active
        windEffect.SetActive(true);

        // Use the user-specified positions directly in local space.
        // This prevents accidental magnitude inflation caused by dividing by a small lossyScale.
        Vector3 startLocal = windStartPos;
        Vector3 targetLocal = windEndPos;
        float epsilon = 0.01f;

        if (isUI)
        {
            // Use anchoredPosition for UI elements (stable with anchors and layouts)
            wrt.anchoredPosition = new Vector2(startLocal.x, startLocal.y);
        }
        else
        {
            windEffect.transform.localPosition = startLocal;
        }

        while (true)
        {
            float step = windSpeed * Time.unscaledDeltaTime;

            if (isUI)
            {
                Vector2 cur = wrt.anchoredPosition;
                Vector2 target2 = new Vector2(targetLocal.x, targetLocal.y);
                Vector2 next = Vector2.MoveTowards(cur, target2, step);
                wrt.anchoredPosition = next;

                if (Vector2.Distance(next, target2) <= epsilon)
                {
                    if (windLoop)
                        wrt.anchoredPosition = new Vector2(startLocal.x, startLocal.y);
                    else
                    {
                        wrt.anchoredPosition = target2; // ensure exact final pos
                        break;
                    }
                }
            }
            else
            {
                Vector3 cur = windEffect.transform.localPosition;
                Vector3 next = Vector3.MoveTowards(cur, targetLocal, step);
                windEffect.transform.localPosition = next;

                if (Vector3.Distance(next, targetLocal) <= epsilon)
                {
                    if (windLoop)
                        windEffect.transform.localPosition = startLocal;
                    else
                    {
                        windEffect.transform.localPosition = targetLocal; // ensure exact final pos
                        break;
                    }
                }
            }

            yield return null;
        }

        if (deactivateWindAfterFinish)
            windEffect.SetActive(false);

        windCoroutine = null;
    }

    private IEnumerator BackgroundFadeRoutine()
    {
        // Attempt to use CanvasGroup first for whole-group alpha control
        CanvasGroup cg = cutInBackground.GetComponent<CanvasGroup>();
        Image[] images = cutInBackground.GetComponentsInChildren<Image>(true);
        SpriteRenderer[] srs = cutInBackground.GetComponentsInChildren<SpriteRenderer>(true);

        if (cg == null && (images == null || images.Length == 0) && (srs == null || srs.Length == 0))
        {
            Debug.LogWarning("CutInAnimation: No CanvasGroup, Image or SpriteRenderer found on cutInBackground to animate alpha.");
            yield break;
        }

        // Ensure start is fully transparent
        SetBackgroundAlpha(0f);

        // Fade in
        float elapsed = 0f;
        while (elapsed < backgroundFadeDuration)
        {
            float a = Mathf.Lerp(0f, backgroundTransparency, backgroundFadeDuration > 0f ? elapsed / backgroundFadeDuration : 1f);
            SetBackgroundAlpha(a);
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }
        SetBackgroundAlpha(backgroundTransparency);

        // Hold
        yield return new WaitForSecondsRealtime(backgroundHoldDuration);

        // Fade out
        elapsed = 0f;
        while (elapsed < backgroundFadeDuration)
        {
            float a = Mathf.Lerp(backgroundTransparency, 0f, backgroundFadeDuration > 0f ? elapsed / backgroundFadeDuration : 1f);
            SetBackgroundAlpha(a);
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }
        SetBackgroundAlpha(0f);

        backgroundCoroutine = null;
    }

    private void SetBackgroundAlpha(float a)
    {
        CanvasGroup cg = cutInBackground.GetComponent<CanvasGroup>();
        if (cg != null)
        {
            cg.alpha = a;
            return;
        }

        Image[] images = cutInBackground.GetComponentsInChildren<Image>(true);
        foreach (var img in images)
        {
            Color c = img.color;
            c.a = a;
            img.color = c;
        }

        SpriteRenderer[] srs = cutInBackground.GetComponentsInChildren<SpriteRenderer>(true);
        foreach (var sr in srs)
        {
            Color c = sr.color;
            c.a = a;
            sr.color = c;
        }
    }

    void CutInCharacterAnimation()
    {
        if (characterCoroutine != null)
            StopCoroutine(characterCoroutine);
        characterCoroutine = StartCoroutine(CutInCharacterRoutine());
    }

    private IEnumerator CutInCharacterRoutine()
    {
        if (cutInCharacter == null)
        {
            Debug.LogWarning("CutInAnimation: cutInCharacter is null.");
            yield break;
        }

        RectTransform rt = cutInCharacter.rectTransform;
        Transform t = rt != null ? (Transform)rt : (Transform)cutInCharacter.transform;

        // Set start position
        t.localPosition = characterStartPos;
        Vector3 current = t.localPosition;

        // Phase 1: move to mid position at start speed
        if (characterStartSpeed <= 0f)
        {
            t.localPosition = characterMidPos;
            current = characterMidPos;
        }
        else
        {
            while (Vector3.Distance(current, characterMidPos) > 0.01f)
            {
                current = Vector3.MoveTowards(current, characterMidPos, characterStartSpeed * Time.unscaledDeltaTime);
                t.localPosition = current;
                yield return null;
            }
        }

        // Phase 2: for the hold duration, move toward the end using the mid (slow) speed
        float timer = 0f;
        while (timer < characterMidHoldDuration)
        {
            if (characterMidSpeed <= 0f)
            {
                // Stay where we are
                // (if mid speed is 0 or negative, we just hold current position)
            }
            else
            {
                current = Vector3.MoveTowards(current, characterEndPos, characterMidSpeed * Time.unscaledDeltaTime);
                t.localPosition = current;
            }
            timer += Time.unscaledDeltaTime;
            yield return null;
        }

        // After the slow period we continue toward the end at the end speed
        // (do not snap back to the mid position)

        // Phase 3: move to end position at end speed
        if (characterEndSpeed <= 0f)
        {
            t.localPosition = characterEndPos;
            current = characterEndPos;
        }
        else
        {
            while (Vector3.Distance(current, characterEndPos) > 0.01f)
            {
                current = Vector3.MoveTowards(current, characterEndPos, characterEndSpeed * Time.unscaledDeltaTime);
                t.localPosition = current;
                yield return null;
            }
        }

        // Finalize
        t.localPosition = characterEndPos;
        characterCoroutine = null;
    }

    void SkillNameAnimation()
    {
        if (skillNameCoroutine != null)
            StopCoroutine(skillNameCoroutine);
        skillNameCoroutine = StartCoroutine(SkillNameRoutine());
    }

    private IEnumerator SkillNameRoutine()
    {
        if (skillNameDisplay == null)
        {
            Debug.LogWarning("CutInAnimation: skillNameDisplay is null.");
            yield break;
        }

        RectTransform rt = skillNameDisplay.GetComponent<RectTransform>();
        Transform t = rt != null ? (Transform)rt : (Transform)skillNameDisplay.transform;

        // Set start position and ensure visible
        t.localPosition = skillNameStartPos;
        Vector3 current = t.localPosition;
        Color col = skillNameDisplay.GetComponentInChildren<TextMeshProUGUI>().color;
        col.a = 1f;
        skillNameDisplay.GetComponentInChildren<TextMeshProUGUI>().color = col;

        // Move to end position at constant speed
        if (skillNameSpeed <= 0f)
        {
            t.localPosition = skillNameEndPos;
            current = skillNameEndPos;
        }
        else
        {
            while (Vector3.Distance(current, skillNameEndPos) > 0.01f)
            {
                current = Vector3.MoveTowards(current, skillNameEndPos, skillNameSpeed * Time.unscaledDeltaTime);
                t.localPosition = current;
                yield return null;
            }
        }

        // Ensure exact end position
        t.localPosition = skillNameEndPos;

        // Hold before fading
        if (skillNameHoldDuration > 0f)
            yield return new WaitForSecondsRealtime(skillNameHoldDuration);

        // Fade out the text alpha
        float elapsed = 0f;
        while (elapsed < skillNameFadeDuration)
        {
            float a = Mathf.Lerp(1f, 0f, skillNameFadeDuration > 0f ? elapsed / skillNameFadeDuration : 1f);
            Color c2 = skillNameDisplay.GetComponentInChildren<TextMeshProUGUI>().color;
            c2.a = a;
            skillNameDisplay.GetComponentInChildren<TextMeshProUGUI>().color = c2;
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        Color cFinal = skillNameDisplay.GetComponentInChildren<TextMeshProUGUI>().color;
        cFinal.a = 0f;
        skillNameDisplay.GetComponentInChildren<TextMeshProUGUI>().color = cFinal;

        skillNameCoroutine = null;
    }

    private void ResetCutInState()
    {
        if (backgroundCoroutine != null)
        {
            StopCoroutine(backgroundCoroutine);
            backgroundCoroutine = null;
        }

        if (characterCoroutine != null)
        {
            StopCoroutine(characterCoroutine);
            characterCoroutine = null;
        }

        if (skillNameCoroutine != null)
        {
            StopCoroutine(skillNameCoroutine);
            skillNameCoroutine = null;
        }

        // Reset visuals
        if (cutInBackground != null)
            SetBackgroundAlpha(0f);

        if (cutInCharacter != null)
        {
            RectTransform crt = cutInCharacter.rectTransform;
            if (crt != null) crt.localPosition = characterStartPos;
            else cutInCharacter.transform.localPosition = characterStartPos;
        }

        if (skillNameDisplay != null)
        {
            RectTransform srt = skillNameDisplay.GetComponent<RectTransform>();
            if (srt != null) srt.localPosition = skillNameStartPos;
            else skillNameDisplay.transform.localPosition = skillNameStartPos;
            Color c = skillNameDisplay.GetComponentInChildren<TextMeshProUGUI>().color;
            c.a = 1f;
            skillNameDisplay.GetComponentInChildren<TextMeshProUGUI>().color = c;
        }

        if (samEffect != null)
            samEffect.SetActive(false);

        if (samEffectAdd != null)
            samEffectAdd.SetActive(false);

        if (samEffectSBB != null)
            samEffectSBB.SetActive(false);

        if (samEffectAddSBB != null)
            samEffectAddSBB.SetActive(false);

        if (windCoroutine != null)
        {
            StopCoroutine(windCoroutine);
            windCoroutine = null;
        }

        if (windEffect != null)
        {
            RectTransform wrt = windEffect.GetComponent<RectTransform>();
            if (wrt != null)
                wrt.anchoredPosition = new Vector2(windStartPos.x, windStartPos.y);
            else
                windEffect.transform.localPosition = windStartPos;

            if (deactivateWindAfterFinish)
                windEffect.SetActive(false);
        }
    }

    private IEnumerator FullCutInRoutine()
    {
        // Activate all pieces
        if (cutInBackground != null) cutInBackground.gameObject.SetActive(true);
        if (cutInCharacter != null) cutInCharacter.gameObject.SetActive(true);
        if (skillNameDisplay != null) skillNameDisplay.gameObject.SetActive(true);

        if(currentCutInType == CutInType.Normal)
        {
            if (samEffect != null) samEffect.SetActive(true);
            if (samEffectAdd != null) samEffectAdd.SetActive(true);
            
            samEffect.GetComponent<SamAnimator>()?.InitializeAnimator();
            samEffectAdd.GetComponent<SamAnimator>()?.InitializeAnimator();

            samEffect.GetComponent<SamAnimator>()?.SetAnimation("start", false);
            samEffectAdd.GetComponent<SamAnimator>()?.SetAnimation("start", false);
        }
        else if(currentCutInType == CutInType.SBB)
        {
            if (samEffectSBB != null) samEffectSBB.SetActive(true);
            if (samEffectAddSBB != null) samEffectAddSBB.SetActive(true);

            samEffectSBB.GetComponent<SamAnimator>()?.InitializeAnimator();
            samEffectAddSBB.GetComponent<SamAnimator>()?.InitializeAnimator();

            samEffectSBB.GetComponent<SamAnimator>()?.SetAnimation("start", false);
            samEffectAddSBB.GetComponent<SamAnimator>()?.SetAnimation("start", false);
        }

        windEffect.GetComponent<WindParticlePlacer>()?.CreateWind();

        // Reset states
        SetBackgroundAlpha(0f);
        if (cutInCharacter != null)
        {
            RectTransform crt = cutInCharacter.rectTransform;
            if (crt != null) crt.localPosition = characterStartPos;
            else cutInCharacter.transform.localPosition = characterStartPos;
        }

        if (skillNameDisplay != null)
        {
            RectTransform srt = skillNameDisplay.GetComponent<RectTransform>();
            if (srt != null) srt.localPosition = skillNameStartPos;
            else skillNameDisplay.transform.localPosition = skillNameStartPos;
            Color c = skillNameDisplay.GetComponentInChildren<TextMeshProUGUI>().color;
            c.a = 1f;
            skillNameDisplay.GetComponentInChildren<TextMeshProUGUI>().color = c;
        }

        // Start parts
        BackgroundAnimation();
        CutInCharacterAnimation();
        SkillNameAnimation();
        StartWind();

        // Wait for all parts to finish
        while (backgroundCoroutine != null || characterCoroutine != null || skillNameCoroutine != null)
            yield return null;

        // Optionally cleanup/deactivate after finish (kept visible by default)

        cutInCoroutine = null;

        Time.timeScale = SpeedUpBattleButtonUI.currentSpeed;
        isPlaying = false;
    }
    
}
public enum CutInType
{
    Normal,
    SBB
}