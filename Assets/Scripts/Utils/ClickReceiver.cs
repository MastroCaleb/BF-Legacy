using UnityEngine;
using UnityEngine.EventSystems;

public class ClickReceiver : MonoBehaviour, IPointerClickHandler
{
    public UnitBehaviour owner;

    public void OnPointerClick(PointerEventData eventData)
    {
        owner.OnPointerClick(eventData);
    }
}
