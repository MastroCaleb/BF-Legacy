using UnityEngine;
using TMPro;
using UnityEngine.UI;

public class ScrollingTMPText : MonoBehaviour
{
    public float speed = 100f;
    [TextArea(3, 10)]
    public string text;
    public float padding = 20f;
    private RectTransform textA;
    private RectTransform textB;
    private float textWidth;
    private bool initialized;

    void Start() => Setup();

    public void SetText(string newText)
    {
        text = newText;
        Setup();
    }

    private void Setup()
    {
        textA = transform.GetChild(0).GetComponent<RectTransform>();
        textB = transform.GetChild(1).GetComponent<RectTransform>();

        TextMeshProUGUI tmpA = textA.GetComponent<TextMeshProUGUI>();
        TextMeshProUGUI tmpB = textB.GetComponent<TextMeshProUGUI>();

        tmpA.SetText(text);
        tmpB.SetText(text);

        textWidth = gameObject.GetComponent<RectTransform>().rect.width;

        LayoutRebuilder.ForceRebuildLayoutImmediate(textA);
        LayoutRebuilder.ForceRebuildLayoutImmediate(textB);

        textWidth = textWidth >= tmpA.preferredWidth ? textWidth : tmpA.preferredWidth;

        tmpA.rectTransform.sizeDelta = new Vector2(textWidth, tmpA.rectTransform.sizeDelta.y);
        tmpB.rectTransform.sizeDelta = new Vector2(textWidth, tmpB.rectTransform.sizeDelta.y);

        textA.anchoredPosition = new Vector2(0, textA.anchoredPosition.y);
        textB.anchoredPosition = new Vector2(textWidth + padding, textB.anchoredPosition.y);

        float containerWidth = gameObject.GetComponent<RectTransform>().rect.width;
        initialized = tmpA.preferredWidth > containerWidth;
    }

    void Update()
    {
        if (!initialized) return;
        
        float move = speed * Time.deltaTime;

        textA.anchoredPosition += Vector2.left * move;
        textB.anchoredPosition += Vector2.left * move;

        if (textA.anchoredPosition.x + textWidth <= 0)
            textA.anchoredPosition = new Vector2(textB.anchoredPosition.x  + padding  + textWidth, textA.anchoredPosition.y);

        if (textB.anchoredPosition.x + textWidth <= 0)
            textB.anchoredPosition = new Vector2(textA.anchoredPosition.x  + padding  + textWidth, textB.anchoredPosition.y);
    }
}