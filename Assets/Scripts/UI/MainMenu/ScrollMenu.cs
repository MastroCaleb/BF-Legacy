using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using System.Collections.Generic;

public class ScrollMenu : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    [Header("Menu Items")]
    public List<GameObject> menuItems;
    public RectTransform leftPoint;
    public RectTransform centerPoint;
    public RectTransform rightPoint;

    [Header("Dot Indicators")]
    public List<Image> dots;
    public Sprite activeDotSprite;
    public Sprite inactiveDotSprite;

    [Header("Settings")]
    public float transitionSpeed = 5f;
    public Vector3 selectedScale = new Vector3(1f, 1f, 1f);
    public Vector3 sideScale = new Vector3(0.75f, 0.75f, 1f);
    public Vector3 furtherSideScale = new Vector3(0.6f, 0.6f, 1f);
    public float furtherOffset = 200f;
    public float swipeThreshold = 50f;

    private int centerIndex = 0;
    private Vector2 dragStartPos;

    private Vector3 offscreenLeft;
    private Vector3 offscreenRight;

    void Start()
    {
        offscreenLeft = new Vector3(-860, centerPoint.position.y, 0);
        offscreenRight = new Vector3(860, centerPoint.position.y, 0);

        UpdateMenuPositions(true);
        UpdateDots();
    }

    void Update()
    {
        SmoothMoveItems();
    }

    public void ResetToStart()
    {
        ResetToIndex(0, instant: true);
    }

    public void ResetToIndex(int index, bool instant = true)
    {
        if (menuItems == null || menuItems.Count == 0) return;

        centerIndex = Mathf.Clamp(index, 0, menuItems.Count - 1);
        UpdateMenuPositions(instant);
        UpdateDots();
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        dragStartPos = eventData.position;
    }

    public void OnDrag(PointerEventData eventData) { }

    public void OnEndDrag(PointerEventData eventData)
    {
        float deltaX = eventData.position.x - dragStartPos.x;

        if (Mathf.Abs(deltaX) > swipeThreshold)
        {
            if (deltaX < 0) MoveRight();
            else MoveLeft();
        }
    }

    public void MoveRight()
    {
        centerIndex = (centerIndex + 1) % menuItems.Count;
        UpdateMenuPositions();
        UpdateDots();
    }

    public void MoveLeft()
    {
        centerIndex = (centerIndex - 1 + menuItems.Count) % menuItems.Count;
        UpdateMenuPositions();
        UpdateDots();
    }

    private void UpdateMenuPositions(bool instant = false)
    {
        int leftIndex        = (centerIndex - 1 + menuItems.Count) % menuItems.Count;
        int rightIndex       = (centerIndex + 1) % menuItems.Count;
        int furtherLeftIndex = (centerIndex - 2 + menuItems.Count) % menuItems.Count;
        int furtherRightIndex= (centerIndex + 2) % menuItems.Count;

        for (int i = 0; i < menuItems.Count; i++)
        {
            RectTransform rt = menuItems[i].GetComponent<RectTransform>();
            Vector3 targetPos;
            Vector3 targetScale = sideScale;
            bool active = false;

            if (i == centerIndex)
            {
                targetPos  = centerPoint.localPosition;
                targetScale = selectedScale;
                active = true;
            }
            else if (i == leftIndex)
            {
                targetPos  = leftPoint.localPosition;
                targetScale = sideScale;
                active = true;
            }
            else if (i == rightIndex)
            {
                targetPos  = rightPoint.localPosition;
                targetScale = sideScale;
                active = true;
            }
            else if (i == furtherLeftIndex)
            {
                targetPos = offscreenLeft;
            }
            else if (i == furtherRightIndex)
            {
                targetPos = offscreenRight;
            }
            else
            {
                targetPos = (i < centerIndex) ? offscreenLeft : offscreenRight;
            }

            if (instant || !active)
            {
                rt.localPosition = targetPos;
                rt.localScale    = targetScale;
            }
            else
            {
                rt.localPosition = Vector3.Lerp(rt.localPosition, targetPos, Time.deltaTime * transitionSpeed);
                rt.localScale    = Vector3.Lerp(rt.localScale,    targetScale, Time.deltaTime * transitionSpeed);
            }

            rt.gameObject.SetActive(active || i == centerIndex);
        }
    }

    private void SmoothMoveItems()
    {
        int leftIndex        = (centerIndex - 1 + menuItems.Count) % menuItems.Count;
        int rightIndex       = (centerIndex + 1) % menuItems.Count;
        int furtherLeftIndex = (centerIndex - 2 + menuItems.Count) % menuItems.Count;
        int furtherRightIndex= (centerIndex + 2) % menuItems.Count;

        for (int i = 0; i < menuItems.Count; i++)
        {
            if (!menuItems[i].gameObject.activeSelf) continue;

            RectTransform rt = menuItems[i].GetComponent<RectTransform>();
            Vector3 targetPos;
            Vector3 targetScale;

            if (i == centerIndex)
            {
                targetPos   = centerPoint.position;
                targetScale = selectedScale;
            }
            else if (i == leftIndex)
            {
                targetPos   = leftPoint.position;
                targetScale = sideScale;
            }
            else if (i == rightIndex)
            {
                targetPos   = rightPoint.position;
                targetScale = sideScale;
            }
            else if (i == furtherLeftIndex)
            {
                targetPos   = leftPoint.position + Vector3.left * furtherOffset;
                targetScale = furtherSideScale;
            }
            else if (i == furtherRightIndex)
            {
                targetPos   = rightPoint.position + Vector3.right * furtherOffset;
                targetScale = furtherSideScale;
            }
            else continue;

            rt.position   = Vector3.Lerp(rt.position,    targetPos,   Time.deltaTime * transitionSpeed);
            rt.localScale = Vector3.Lerp(rt.localScale,  targetScale, Time.deltaTime * transitionSpeed);
        }
    }

    private void UpdateDots()
    {
        for (int i = 0; i < dots.Count; i++)
            dots[i].sprite = (i == centerIndex) ? activeDotSprite : inactiveDotSprite;
    }

    void OnEnable()
    {
        ResetToStart();
    }
}