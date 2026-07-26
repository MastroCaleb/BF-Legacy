using UnityEngine;
using UnityEngine.EventSystems;
using System.Collections;

public class HoldToActivate : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
{
    [Tooltip("GameObject to enable while holding, disable when released.")]
    [SerializeField] private GameObject target;

    [Tooltip("Delay in seconds before the target activates after pressing down.")]
    [SerializeField] private float activateDelay = 0.05f;

    public HoldMenu holdMenu;

    private Coroutine activateRoutine;

    public void OnPointerDown(PointerEventData eventData)
    {
        if (activateRoutine != null) StopCoroutine(activateRoutine);
        activateRoutine = StartCoroutine(ActivateAfterDelay());
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        if (activateRoutine != null)
        {
            StopCoroutine(activateRoutine);
            activateRoutine = null;
        }

        if (target != null) target.SetActive(false);
    }

    private IEnumerator ActivateAfterDelay()
    {
        yield return new WaitForSeconds(activateDelay);

        SetActiveUnit();
        if (target != null && AbilityDetailsUI.unit !=null) target.SetActive(true);

        activateRoutine = null;
    }

    public void SetActiveUnit()
    {
        if(holdMenu == HoldMenu.UnitSummary)
        {
            AbilityDetailsUI.unit = MainUI.unitSummary.unitInventoryData.unit;
        }
        else if(holdMenu == HoldMenu.Battle)
        {
            UnitBehaviour unit = gameObject.GetComponent<UnitSlotUI>().unit;

            if(unit == null)
            {
                AbilityDetailsUI.unit = null;
                return;
            }

            AbilityDetailsUI.unit = unit.unitData;
        }
    }
}
public enum HoldMenu{
    UnitSummary,
    Battle 
}