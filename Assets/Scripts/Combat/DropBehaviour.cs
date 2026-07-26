using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class DropBehaviour : MonoBehaviour
{
    public DropType dropType;
    public GameObject target;
    public Vector3 scale = new Vector3(0.75f, 0.75f, 1);
    public int valueOfDrop;
    public UnitDropData unitDropData;

    RectTransform rect;

    void Awake()
    {
        rect = GetComponent<RectTransform>();

        var animator = GetComponent<BraveFrontierFrameAnimator>();
        if(animator != null)
            animator.InitializeCachedAnimator();
    }

    void Start()
    {
        if (target == null) return;

        rect.localScale = scale;
        rect.SetParent(BattleUI.dropsLayer, false);

        DropMoveManager.Instance.Register(
            rect,
            target,
            dropType,
            valueOfDrop,
            unitDropData,
            dropType == DropType.BC || dropType == DropType.HC || dropType == DropType.Gem ? target.GetComponent<UnitBehaviour>() : null
        );

        enabled = false; // this script does nothing after setupHook
    }
}
public enum DropType
{
    BC,
    HC,
    Zel,
    Karma,
    Item,
    Unit,
    Gem
}