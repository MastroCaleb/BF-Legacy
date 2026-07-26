using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class ImageToFont : MonoBehaviour
{
    public enum HorizontalAlign { Left, Center, Right }
    public enum VerticalAlign { Top, Center, Bottom }

    [Header("Alignment")]
    public HorizontalAlign horizontalAlignment = HorizontalAlign.Center;
    public VerticalAlign verticalAlignment = VerticalAlign.Center;
    public Vector2 padding = Vector2.zero; // x = horizontal padding, y = vertical padding

    [Header("Font Settings")]
    public List<Font> font;
    public string text;
    public Color color = Color.white;
    public float fontDivider = 1.5f;

    private Dictionary<char, Font> fontLookup;
    private List<Image> renderedImages = new List<Image>();

    void Start()
    {
        BuildFontLookup();
        transform.localScale = Vector3.one;
        SpriteAdder();
    }

    void Update()
    {
        if(renderedImages.Count == 0) return;
        foreach (var img in renderedImages)
        {
            if(img != null) img.color = color;
        }
    }

    public void SetText(string textToSet)
    {
        text = textToSet;
        // Rebuild font lookup and re-render text
        BuildFontLookup();
        SpriteAdder();
    }

    private void BuildFontLookup()
    {
        fontLookup = new Dictionary<char, Font>();
        foreach (var f in font)
        {
            if (!fontLookup.ContainsKey(f.fontChar))
                fontLookup.Add(f.fontChar, f);
        }
    }

    public void SpriteAdder()
    {
        // Clear previous children (optional, helpful when re-running in editor)
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            DestroyImmediate(transform.GetChild(i).gameObject);
        }

        if (string.IsNullOrEmpty(text)) return;

        // Calculate widths and heights per character
        float totalWidth = 0f;
        float maxHeight = 0f;
        List<float> charWidths = new List<float>();
        List<float> charHeights = new List<float>();

        for (int i = 0; i < text.Length; i++)
        {
            if (!fontLookup.TryGetValue(text[i], out Font toRender))
            {
                Debug.LogWarning($"Character '{text[i]}' not found in font list.");
                charWidths.Add(0f);
                charHeights.Add(0f);
                continue;
            }

            Sprite sprite = toRender.fontSprite;
            float w = sprite.rect.width / fontDivider;
            float h = sprite.rect.height / fontDivider;
            charWidths.Add(w);
            charHeights.Add(h);

            totalWidth += w;
            if (h > maxHeight) maxHeight = h;
        }

        // Get parent RectTransform dimensions (the rect of the object this script is on)
        RectTransform parentRT = GetComponent<RectTransform>();
        float parentWidth = 0f;
        float parentHeight = 0f;
        if (parentRT != null)
        {
            parentWidth = parentRT.rect.width;
            parentHeight = parentRT.rect.height;
        }
        else
        {
            // If we don't have a RectTransform, default to centering behaviour
            Debug.LogWarning("ImageToFont: No RectTransform found on parent. Using centered alignment relative to local origin.");
        }

        // Compute block center (C) relative to parent's center (which is (0,0) for children)
        float blockCenterX = 0f;
        float blockCenterY = 0f;

        // Horizontal: left edge is (-parentWidth/2 + padding.x), right edge is (parentWidth/2 - padding.x)
        if (parentRT != null)
        {
            switch (horizontalAlignment)
            {
                case HorizontalAlign.Left:
                    // place block so its left edge aligns with parent's left + padding
                    blockCenterX = (-parentWidth / 2f) + padding.x + (totalWidth / 2f);
                    break;
                case HorizontalAlign.Center:
                    blockCenterX = 0f;
                    break;
                case HorizontalAlign.Right:
                    // place block so its right edge aligns with parent's right - padding
                    blockCenterX = (parentWidth / 2f) - padding.x - (totalWidth / 2f);
                    break;
            }
        }
        else
        {
            // fallback: center
            blockCenterX = 0f;
        }

        // Vertical: use maxHeight to compute block vertical center for top/bottom alignment
        if (parentRT != null)
        {
            switch (verticalAlignment)
            {
                case VerticalAlign.Top:
                    // top edge is parentHeight/2 - padding.y, so center = topEdge - blockHeight/2
                    blockCenterY = (parentHeight / 2f) - padding.y - (maxHeight / 2f);
                    break;
                case VerticalAlign.Center:
                    blockCenterY = 0f;
                    break;
                case VerticalAlign.Bottom:
                    blockCenterY = (-parentHeight / 2f) + padding.y + (maxHeight / 2f);
                    break;
            }
        }
        else
        {
            blockCenterY = 0f;
        }

        // Start cursor at the left edge of the block: leftEdge = blockCenterX - totalWidth/2
        float cursor = blockCenterX - (totalWidth / 2f);

        // Render each character; we pass the computed y position per-character so we can account for varying heights
        for (int i = 0; i < text.Length; i++)
        {
            if (!fontLookup.TryGetValue(text[i], out Font toRender)) continue;

            float charWidth = charWidths[i];
            float charHeight = charHeights[i];

            // move cursor by half width -> this will position the character center correctly
            cursor += charWidth / 2f;

            // For vertical placement, align each character center relative to blockCenterY.
            // If you want different per-character vertical alignment (e.g., baseline), you can adjust here.
            float yPos = blockCenterY;

            // Create char at (cursor, yPos)
            TextRenderer(toRender, cursor, yPos);

            // move cursor by remaining half to reach next char start
            cursor += charWidth / 2f;
        }
    }

    void TextRenderer(Font f, float xPos, float yPos)
    {
        GameObject go = new GameObject("Char", typeof(RectTransform), typeof(Image));
        go.transform.SetParent(transform, false); // keep local coordinates

        RectTransform rt = go.GetComponent<RectTransform>();
        rt.localPosition = new Vector3(xPos, yPos, 0f);

        Image img = go.GetComponent<Image>();
        renderedImages.Add(img);
        img.color = color;
        img.sprite = f.fontSprite;
        img.SetNativeSize();
        img.rectTransform.sizeDelta = new Vector2(img.rectTransform.sizeDelta.x / fontDivider, img.rectTransform.sizeDelta.y / fontDivider);
    }
}

[System.Serializable]
public class Font
{
    public Sprite fontSprite;
    public char fontChar;
}
