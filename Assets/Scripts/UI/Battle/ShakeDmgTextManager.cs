using System.Collections.Generic;
using UnityEngine;

public class ShakeDmgTextManager : MonoBehaviour
{
    public static ShakeDmgTextManager Instance;

    [Header("Animation Settings")]
    public float shakeDuration = 0.2f;
    public float shakeMagnitude = 5f;
    public float scaleUpDuration = 0.05f;
    public float waitAfterScaleUp = 0.1f;
    public float scaleDownDuration = 0.05f;
    public float scaleMultiplier = 1.5f;
    public float fadeDuration = 0.1f;
    public float waitAfterShake = 0.1f;

    private readonly List<ShakeTextData> activeTexts = new List<ShakeTextData>(256);

    void Awake() => Instance = this;

    void Update()
    {
        float dt = Time.unscaledDeltaTime;

        for (int i = activeTexts.Count - 1; i >= 0; i--)
        {
            var d = activeTexts[i];
            if (!d.active) continue;

            d.time += dt;

            // ───── SHAKE ─────
            if (d.time < shakeDuration)
            {
                float x = Random.Range(-shakeMagnitude, shakeMagnitude);
                float y = Random.Range(-shakeMagnitude, shakeMagnitude);
                d.rect.localPosition = d.basePos + new Vector3(x, y, 0);
            }
            else
            {
                if(d.rect != null) d.rect.localPosition = d.basePos;
            }

            // ───── SCALE UP ─────
            float t;
            Vector3 targetScale = d.baseScale * scaleMultiplier;

            if (d.time < scaleUpDuration)
            {
                t = d.time / scaleUpDuration;
                d.rect.localScale = Vector3.Lerp(d.baseScale, targetScale, t);
            }
            // ───── SCALE DOWN ─────
            else if (d.time > scaleUpDuration + waitAfterScaleUp &&
                     d.time < scaleUpDuration + waitAfterScaleUp + scaleDownDuration)
            {
                t = (d.time - scaleUpDuration - waitAfterScaleUp) / scaleDownDuration;
                d.rect.localScale = Vector3.Lerp(targetScale, d.baseScale, t);
            }

            // ───── FADE ─────
            float fadeStart = shakeDuration + waitAfterShake;
            if (d.time > fadeStart)
            {
                t = (d.time - fadeStart) / fadeDuration;
                d.img.color = Color.Lerp(d.baseColor, new Color(0,0,0,0), t);

                if (t >= 1f)
                {
                    d.active = false;
                }
            }

            activeTexts[i] = d;
        }
    }

    public void Play(
        RectTransform rect,
        ImageToFont img,
        Vector3 pos,
        Vector3 scale,
        Color color
    )
    {
        activeTexts.Add(new ShakeTextData
        {
            rect = rect,
            img = img,
            basePos = pos,
            baseScale = scale,
            baseColor = color,
            time = 0f,
            active = true
        });
    }

    public struct ShakeTextData
    {
        public RectTransform rect;
        public ImageToFont img;

        public Vector3 basePos;
        public Vector3 baseScale;
        public Color baseColor;

        public float time;
        public bool active;
    }

}

