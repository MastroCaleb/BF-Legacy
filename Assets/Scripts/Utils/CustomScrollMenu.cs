using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using System.Collections.Generic;

public class CustomScrollMenu : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    [Header("Menu Items")]
    public List<GameObject> menuItems;

    [Header("Item Layout")]
    public float itemsYPosition = 0f;
    public float itemSpacing = 300f;
    public float offscreenX = 900f;

    [Header("Dot Layout")]
    public RectTransform dotsContainer;
    public float dotsYPosition = -300f;
    public float dotSpacing = 40f;
    public Sprite activeDotSprite;
    public Sprite inactiveDotSprite;

    [Header("Scale")]
    public Vector3 selectedScale = new Vector3(1f, 1f, 1f);
    public Vector3 sideScale     = new Vector3(0.75f, 0.75f, 1f);
    public Vector3 furtherScale  = new Vector3(0.6f, 0.6f, 1f);

    [Header("Motion")]
    public float transitionSpeed = 8f;
    public float swipeThreshold = 50f;

    private int centerIndex = 0;
    private Vector2 dragStartPos;
    private List<Image> dots = new List<Image>();

    void Start()
    {
        ResetMenu();
    }

    void Update()
    {
        AnimateItems();
    }

    void OnDisable()
    {
        dots.Clear();
    }

    void OnEnable()
    {
        if (menuItems == null || menuItems.Count == 0) return;
        ResetMenu();
    }

    // ── RESET ────────────────────────────────────

    public void ResetMenu()
    {
        centerIndex = 0;
        GenerateDots();
        SnapAll();
        UpdateDots();
    }

    public void ResetToIndex(int index)
    {
        if (menuItems == null || menuItems.Count == 0) return;
        centerIndex = Mathf.Clamp(index, 0, menuItems.Count - 1);
        GenerateDots();
        SnapAll();
        UpdateDots();
    }

    // ── DOTS ─────────────────────────────────────

    void GenerateDots()
    {
        if (dotsContainer == null) return;

        foreach (Transform child in dotsContainer)
            Destroy(child.gameObject);

        dots.Clear();

        float startX = -((menuItems.Count - 1) * dotSpacing) * 0.5f;

        for (int i = 0; i < menuItems.Count; i++)
        {
            GameObject go = new GameObject($"Dot_{i}", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(dotsContainer, false);

            RectTransform rt = go.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(20f, 20f);
            rt.anchoredPosition = new Vector2(startX + i * dotSpacing, dotsYPosition);

            Image img = go.GetComponent<Image>();
            img.sprite = inactiveDotSprite;
            img.color = Color.white;
            img.SetNativeSize();

            dots.Add(img);
        }
    }

    void UpdateDots()
    {
        dots.RemoveAll(d => d == null);

        for (int i = 0; i < dots.Count; i++)
            dots[i].sprite = (i == centerIndex) ? activeDotSprite : inactiveDotSprite;
    }

    // ── POSITIONING ─────────────────────────────

    Vector3 TargetPos(int index)
    {
        return new Vector3((index - centerIndex) * itemSpacing, itemsYPosition, 0f);
    }

    Vector3 TargetScale(int offset)
    {
        if (offset == 0) return selectedScale;
        if (Mathf.Abs(offset) == 1) return sideScale;
        return furtherScale;
    }

    void SnapAll()
    {
        for (int i = 0; i < menuItems.Count; i++)
        {
            RectTransform rt = menuItems[i].GetComponent<RectTransform>();

            rt.localPosition = TargetPos(i);
            rt.localScale    = TargetScale(i - centerIndex);

            menuItems[i].SetActive(Mathf.Abs(i - centerIndex) <= 2);
        }
    }

    void AnimateItems()
    {
        float dt = Time.deltaTime * transitionSpeed;

        for (int i = 0; i < menuItems.Count; i++)
        {
            RectTransform rt = menuItems[i].GetComponent<RectTransform>();

            rt.localPosition = Vector3.Lerp(rt.localPosition, TargetPos(i), dt);
            rt.localScale    = Vector3.Lerp(rt.localScale, TargetScale(i - centerIndex), dt);

            menuItems[i].SetActive(Mathf.Abs(i - centerIndex) <= 2);
        }
    }

    // ── NAVIGATION ──────────────────────────────

    public void MoveNext()
    {
        if (centerIndex >= menuItems.Count - 1) return;
        centerIndex++;
        UpdateDots();
    }

    public void MovePrevious()
    {
        if (centerIndex <= 0) return;
        centerIndex--;
        UpdateDots();
    }

    // ── DRAG ────────────────────────────────────

    public void OnBeginDrag(PointerEventData eventData)
    {
        dragStartPos = eventData.position;
    }

    public void OnDrag(PointerEventData eventData) { }

    public void OnEndDrag(PointerEventData eventData)
    {
        float dx = eventData.position.x - dragStartPos.x;

        if (Mathf.Abs(dx) < swipeThreshold) return;

        if (dx > 0) MovePrevious();
        else MoveNext();
    }
}