using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public class CustomScrollView : MonoBehaviour, IDragHandler, IBeginDragHandler, IEndDragHandler
{
    public float scrollSpeedViewport = 1f;
    public float scrollSpeedHandle = 1.5f;
    public RectTransform content;
    public RectTransform viewport;
    public RectTransform handle;
    public RectTransform scrollbar;
    public InventoryRenderer inventoryRenderer;

    float contentHeight, viewportHeight, handleHeight, maxHandleTravel;
    private bool isDraggingHandle = false;
    private float lastHandleY;

    void Start()
    {
        ResetScroll();
    }

    public void ResetScroll()
    {
        if (handle == null || content == null) return;

        handle.anchoredPosition = Vector2.zero;
        content.anchoredPosition = Vector2.zero;
        lastHandleY = 0f;
        inventoryRenderer?.UpdateVisibility();
    }

    void LateUpdate()
    {
        viewportHeight = viewport.rect.height;
        contentHeight  = content.rect.height;

        if (contentHeight <= viewportHeight)
        {
            handle.gameObject.SetActive(false);
            scrollbar.gameObject.SetActive(false);
            content.anchoredPosition = Vector2.zero;
            return;
        }
        handle.gameObject.SetActive(true);

        float ratio = viewportHeight / contentHeight;
        handleHeight = Mathf.Clamp(scrollbar.rect.height * ratio, 20f, scrollbar.rect.height);
        handle.sizeDelta = new Vector2(handle.sizeDelta.x, handleHeight);

        maxHandleTravel = scrollbar.rect.height - handleHeight;

        float y = Mathf.Clamp(handle.anchoredPosition.y, -maxHandleTravel, 0f);
        handle.anchoredPosition = new Vector2(handle.anchoredPosition.x, y);

        float t = maxHandleTravel > 0 ? (-y / maxHandleTravel) : 0f;
        float contentMaxScroll = contentHeight - viewportHeight;
        content.anchoredPosition = new Vector2(content.anchoredPosition.x, t * contentMaxScroll);

        if (handle.anchoredPosition.y != lastHandleY)
        {
            lastHandleY = handle.anchoredPosition.y;
            inventoryRenderer?.UpdateVisibility();
        }
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        isDraggingHandle = RectTransformUtility.RectangleContainsScreenPoint(
            handle,
            eventData.position,
            eventData.pressEventCamera
        );
    }

    public void OnDrag(PointerEventData eventData)
    {
        float delta = eventData.delta.y;
        if (!isDraggingHandle) delta = -delta;
        float y = handle.anchoredPosition.y + delta * (isDraggingHandle ? scrollSpeedHandle : scrollSpeedViewport);
        handle.anchoredPosition = new Vector2(handle.anchoredPosition.x, y);
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        isDraggingHandle = false;
    }

    void Update()
    {
        float wheel = Input.mouseScrollDelta.y;
        if (wheel != 0f)
        {
            float y = handle.anchoredPosition.y + wheel * 40f;
            handle.anchoredPosition = new Vector2(handle.anchoredPosition.x, y);
        }
    }
}