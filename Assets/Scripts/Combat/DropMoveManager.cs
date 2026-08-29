using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class DropMoveManager : MonoBehaviour
{
    public AudioClip bcSound;
    public AudioClip hcSound;
    public AudioClip zelSound;
    public AudioClip karmaSound;
    public AudioClip unitSound;

    public static bool canMove = false;
    public static DropMoveManager Instance;

    public float moveSpeed = 6f;
    
    public static readonly List<DropMoveData> activeDrops = new List<DropMoveData>(256);

    void Awake() => Instance = this;

    void Update()
    {
        if (!canMove)
            return;

        float dt = Time.deltaTime;

        for (int i = activeDrops.Count - 1; i >= 0; i--)
        {
            var d = activeDrops[i];
            if (!d.active) continue;

            if((d.type == DropType.BC || d.type == DropType.HC) && canMove){
                Vector3 pos = d.rect.position;
                Vector3 targetPos = d.unit.originalPosition.position; //d.target.transform.position
                Vector3 newPos = Vector3.Lerp(pos, targetPos, 1f - Mathf.Pow(moveSpeed, -dt));
                // minimum velocity
                newPos = Vector3.MoveTowards(newPos, targetPos, 4f * dt);
                d.rect.position = newPos;

                if ((newPos - targetPos).sqrMagnitude < 0.5f)
                {
                    ApplyDropEffect(d);

                    // Destroy the RectTransform's GameObject
                    if (d.rect != null)
                        Destroy(d.rect.gameObject);

                    // Remove from activeDrops list
                    activeDrops.RemoveAt(i);
                    continue; // skip the rest of this loop iteration
                }

                activeDrops[i] = d;
            }
            else if ((d.type == DropType.Zel || d.type == DropType.Karma || d.type == DropType.Unit || d.type == DropType.Gem) && canMove)
            {
                if (d.rect == null || d.target == null)
                {
                    // If either the rect or target is null, remove the drop from the list
                    activeDrops.RemoveAt(i);
                    continue; // skip the rest of this loop iteration
                }
                d.rect.SetParent(BattleUI.uiDropLayer.transform);
                Vector3 pos = d.rect.position;
                Vector3 targetPos = d.target.transform.position;
                Vector3 newPos = Vector3.Lerp(pos, targetPos, 1f - Mathf.Pow(moveSpeed, -dt));
                // minimum velocity
                newPos = Vector3.MoveTowards(newPos, targetPos, 3f * dt);
                d.rect.position = newPos;

                if ((newPos - targetPos).sqrMagnitude < 0.5f)
                {
                    ApplyDropEffect(d);

                    // Destroy the RectTransform's GameObject
                    if (d.rect != null)
                        Destroy(d.rect.gameObject);

                    // Remove from activeDrops list
                    activeDrops.RemoveAt(i);
                    continue; // skip the rest of this loop iteration
                }

                activeDrops[i] = d;
            }
            else if(d.type == DropType.Item && canMove)
            {
                Debug.Log("ItemDrop");
                if (d.rect == null)
                {
                    // If either the rect or target is null, remove the drop from the list
                    activeDrops.RemoveAt(i);
                    continue; // skip the rest of this loop iteration
                }

                if(d.rect.gameObject.GetComponent<DropBehaviour>().disappearCoroutine == null)
                    d.rect.gameObject.GetComponent<DropBehaviour>().disappearCoroutine = StartCoroutine(d.rect.gameObject.GetComponent<DropBehaviour>().WhiteToTransparent(1f));
                
                if (d.rect.gameObject.GetComponent<Image>().color.a == 0)
                {
                    ApplyDropEffect(d);

                    // Destroy the RectTransform's GameObject
                    if (d.rect != null)
                        Destroy(d.rect.gameObject);

                    // Remove from activeDrops list
                    activeDrops.RemoveAt(i);
                    continue; // skip the rest of this loop iteration
                }

                activeDrops[i] = d;
            }
        }
    }

    void ApplyDropEffect(DropMoveData d)
    {
        if (d.type == DropType.BC && d.target != null)
        {
            UnitBehaviour targetUnit = d.unit;
            if(targetUnit.unitData.bbAbility != null) targetUnit.bcCount += 1;
            SoundManager.Instance.PlaySound(bcSound);
            if (!targetUnit.isEnemyUnit)
                targetUnit.unitSlotUI.UpdateUI();
        }
        else if (d.type == DropType.HC && d.target != null)
        {
            UnitBehaviour targetUnit = d.unit;
            targetUnit.HealByCrystal();
            SoundManager.Instance.PlaySound(hcSound);
            targetUnit.unitSlotUI.UpdateUI();
        }
        else if(d.type == DropType.Zel && d.target != null)
        {
            BattleManager.totalZelReward += d.value;
            SoundManager.Instance.PlaySound(zelSound);
            BattleUI.UpdateText();
            StartPulse(BattleUI.zelPoint);
        }
        else if(d.type == DropType.Karma && d.target != null)
        {
            BattleManager.totalKarmaReward += d.value;
            SoundManager.Instance.PlaySound(karmaSound);
            BattleUI.UpdateText();
            StartPulse(BattleUI.karmaPoint);
        }
        else if(d.type == DropType.Unit && d.target != null)
        {
            BattleManager.unitDrops.Add(d.unitDropData);
            SoundManager.Instance.PlaySound(unitSound);
            BattleUI.UpdateText();
            StartPulse(BattleUI.unitPoint);
        }
        else if (d.type == DropType.Gem && d.target != null)
        {
            BattleManager.totalGemReward += d.value;
            SoundManager.Instance.PlaySound(hcSound);
        }
        else if (d.type == DropType.Item)
        {
            BattleManager.itemDrops.Add(d.itemDropData);
        }
    }

    readonly Dictionary<RectTransform, Coroutine> pulseCoroutines = new Dictionary<RectTransform, Coroutine>();
    readonly Dictionary<RectTransform, Vector3> pulseBaseScale = new Dictionary<RectTransform, Vector3>();

    void StartPulse(RectTransform rect)
    {
        if (!pulseBaseScale.ContainsKey(rect))
            pulseBaseScale[rect] = rect.localScale;

        if (pulseCoroutines.TryGetValue(rect, out Coroutine running) && running != null)
            StopCoroutine(running);

        pulseCoroutines[rect] = StartCoroutine(PulseScale(rect, pulseBaseScale[rect]));
    }

    IEnumerator PulseScale(RectTransform rect, Vector3 baseScale, float scaleUp = 1.5f, float duration = 0.15f)
    {
        Vector3 target = baseScale * scaleUp;

        float t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            rect.localScale = Vector3.Lerp(baseScale, target, t / duration);
            yield return null;
        }

        t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            rect.localScale = Vector3.Lerp(target, baseScale, t / duration);
            yield return null;
        }

        rect.localScale = baseScale;
    }

    public void Register(
        RectTransform rect,
        GameObject target,
        DropType type,
        int value,
        UnitDropData unitDropData,
        ItemDropData itemDropData,
        UnitBehaviour unit = null
    )
    {
        activeDrops.Add(new DropMoveData
        {
            rect = rect,
            target = target,
            type = type,
            value = value,
            unit = unit,
            unitDropData = unitDropData,
            itemDropData = itemDropData,
            active = true
        });
    }

    public struct DropMoveData
    {
        public RectTransform rect;
        public GameObject target;
        public UnitBehaviour unit;
        public DropType type;
        public UnitDropData unitDropData;
        public ItemDropData itemDropData;
        public int value;
        public bool active;
    }

}

