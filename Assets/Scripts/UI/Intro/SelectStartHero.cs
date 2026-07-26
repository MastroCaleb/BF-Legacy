using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class SelectStartHero : MonoBehaviour
{
    [Header("Data")]
    public List<Unit> startingHeroes;

    [Header("UI References")]
    public List<Image> heroViews;      // Assign your hero card prefab instances here
    public Button leftButton;
    public Button rightButton;

    [Header("Layout Settings")]
    public float itemWidth = 200f;          // Width of each hero card
    public float itemSpacing = 30f;         // Gap between cards
    public float animationDuration = 0.3f;  // Scroll animation time

    int currentSelectedIndex = 0;
    bool isAnimating = false;

    float SlotWidth => itemWidth + itemSpacing;

    // ─────────────────────────────────────────────────────────
    //  Lifecycle
    // ─────────────────────────────────────────────────────────

    void Start()
    {
        if (startingHeroes == null || startingHeroes.Count == 0)
        {
            Debug.LogError("SelectStartHero: startingHeroes list is empty!", this);
            return;
        }

        leftButton?.onClick.AddListener(Left);
        rightButton?.onClick.AddListener(Right);
        InitializePositions();
        UpdateVisuals();
    }

    // ─────────────────────────────────────────────────────────
    //  Initialisation
    // ─────────────────────────────────────────────────────────

    void InitializePositions()
    {
        for (int i = 0; i < heroViews.Count; i++)
        {
            RectTransform rt = heroViews[i].GetComponent<RectTransform>();
            rt.anchoredPosition = new Vector2(i * SlotWidth, 0f);
        }
    }

    // ─────────────────────────────────────────────────────────
    //  Navigation
    // ─────────────────────────────────────────────────────────

    public void Right()
    {
        if (isAnimating || startingHeroes.Count == 0) return;
        currentSelectedIndex = (currentSelectedIndex + 1) % startingHeroes.Count;
        StartCoroutine(ScrollTo(-SlotWidth));
    }

    public void Left()
    {
        if (isAnimating || startingHeroes.Count == 0) return;
        currentSelectedIndex = (currentSelectedIndex - 1 + startingHeroes.Count) % startingHeroes.Count;
        StartCoroutine(ScrollTo(SlotWidth));
    }

    // ─────────────────────────────────────────────────────────
    //  Animation
    // ─────────────────────────────────────────────────────────

    IEnumerator ScrollTo(float deltaX)
    {
        isAnimating = true;

        // Capture start positions for every card
        Vector2[] startPositions = new Vector2[heroViews.Count];
        for (int i = 0; i < heroViews.Count; i++)
            startPositions[i] = heroViews[i].GetComponent<RectTransform>().anchoredPosition;

        float elapsed = 0f;
        float halfWidth = heroViews.Count * SlotWidth * 0.5f;

        while (elapsed < animationDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, elapsed / animationDuration);

            for (int i = 0; i < heroViews.Count; i++)
            {
                RectTransform rt = heroViews[i].GetComponent<RectTransform>();
                float newX = startPositions[i].x + deltaX * t;

                // Wrap cards that slide too far left or right
                newX = WrapPosition(newX, halfWidth);

                rt.anchoredPosition = new Vector2(newX, rt.anchoredPosition.y);
            }

            yield return null;
        }

        // Snap to exact final positions
        SnapPositions(deltaX, startPositions);
        UpdateVisuals();

        isAnimating = false;
    }

    float WrapPosition(float x, float halfWidth)
    {
        float totalWidth = heroViews.Count * SlotWidth;
        if (x > halfWidth)  x -= totalWidth;
        if (x < -halfWidth) x += totalWidth;
        return x;
    }

    void SnapPositions(float deltaX, Vector2[] startPositions)
    {
        float halfWidth = heroViews.Count * SlotWidth * 0.5f;
        for (int i = 0; i < heroViews.Count; i++)
        {
            RectTransform rt = heroViews[i].GetComponent<RectTransform>();
            float snappedX = WrapPosition(startPositions[i].x + deltaX, halfWidth);
            rt.anchoredPosition = new Vector2(snappedX, rt.anchoredPosition.y);
        }
    }

    // ─────────────────────────────────────────────────────────
    //  Visuals
    // ─────────────────────────────────────────────────────────

    void UpdateVisuals()
    {
        for (int i = 0; i < heroViews.Count; i++)
        {
            heroViews[i].sprite = startingHeroes[i].unitFullArt;
        }
    }

    // ─────────────────────────────────────────────────────────
    //  Public accessor
    // ─────────────────────────────────────────────────────────

    public Unit GetSelectedHero() => startingHeroes[currentSelectedIndex];
}