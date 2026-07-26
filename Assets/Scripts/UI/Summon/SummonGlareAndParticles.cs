using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class SummonGlareAndParticles : MonoBehaviour
{
    [Header("Parents")]
    public Transform glareParent;
    public Transform starParent;

    [Header("Sprites")]
    public Sprite glareSprite;
    public Sprite starSprite;

    [Header("Star Settings")]
    public int startStarSpawners = 3;
    public int maxStars = 30;
    public float starSpawnInterval = 0.05f;
    public float starSpawnIntervalOffset = 0.05f;
    public float starMinSpeed = 600f;
    public float starSpeed = 1000f;
    public float starLifetime = 2f;
    public float starScaleDuration = 0.4f;
    public float starStartScale = 0.1f;
    public float starEndScale = 0.5f;
    public float starRotationSpeed = 360f;

    [Header("Star Color")]
    public bool starRainbow = false;
    public Color[] starColors;
    [Range(0f, 1f)] public float starAlpha = 1f;
    public float starRainbowSpeed = 1f;

    [Header("Glare Settings")]
    public int startGlareSpawners = 3;
    public int glareCount = 4;
    public float glareSpawnInterval = 0.15f;
    public float glareLifetime = 1.2f;
    public float glareScaleSpeed = 2f; // ← NEW (units per second)
    public float glareStartScale = 0.1f;
    public float glareEndScale = 1.5f;
    public float glareMaxRotation = 15f;

    [Header("Glare Color")]
    public bool glareRainbow = false;
    public Color[] glareColors;
    [Range(0f, 1f)] public float glareAlpha = 1f;
    public float glareRainbowSpeed = 0.5f;

    [Header("Rendering")]
    public Material additiveMaterial;

    public void StartStarsAnimation()
    {
        for (int i = 0; i < startStarSpawners; i++)
            StartCoroutine(SpawnStars());
    }

    public void DestroyAllParticles()
    {
        StopAllCoroutines();
        foreach (Transform child in starParent)
            Destroy(child.gameObject);
        foreach (Transform child in glareParent)
            Destroy(child.gameObject);
    }

    public void StartGlaresAnimation()
    {
        StartCoroutine(StartCoroutines());
    }

    public IEnumerator StartCoroutines()
    {
        for (int i = 0; i < startGlareSpawners; i++)
        {
            StartCoroutine(SpawnGlares());
            yield return new WaitForSeconds(glareSpawnInterval);
        }
    }

    IEnumerator SpawnStars()
    {
        for (int i = 0; i < maxStars; i++)
        {
            Image img = CreateImage("Star", starSprite);
            RectTransform rt = img.rectTransform;
            rt.SetParent(starParent, false);

            Vector2 dir = Random.insideUnitCircle.normalized;
            float rotDir = Random.value > 0.5f ? 1f : -1f;
            float hueOffset = Random.value;
            //img.material = additiveMaterial;

            Color baseColor = GetRandomColor(starColors, starAlpha);

            StartCoroutine(AnimateStar(rt, img, dir, rotDir, hueOffset, baseColor));
            yield return new WaitForSeconds(Random.Range(starSpawnInterval - starSpawnIntervalOffset, starSpawnInterval + starSpawnIntervalOffset));
        }
    }

    IEnumerator AnimateStar(
        RectTransform rt,
        Image img,
        Vector2 direction,
        float rotationDir,
        float hueOffset,
        Color baseColor)
    {
        float time = 0f;
        Vector3 startScale = Vector3.one * starStartScale;
        Vector3 endScale = Vector3.one * starEndScale;
        var speed = Random.Range(starMinSpeed, starSpeed);

        while (time < starLifetime)
        {
            rt.anchoredPosition += direction * speed * Time.deltaTime;
            rt.Rotate(0, 0, starRotationSpeed * rotationDir * Time.deltaTime);

            if (time < starScaleDuration)
                rt.localScale = Vector3.Lerp(startScale, endScale, time / starScaleDuration);
            else
                rt.localScale = endScale;

            img.color = starRainbow
                ? GetRainbowColor(baseColor, starAlpha, starRainbowSpeed, hueOffset, time)
                : baseColor;

            time += Time.deltaTime;
            yield return null;
        }

        Destroy(rt.gameObject);
    }

    IEnumerator SpawnGlares()
    {
        for (int i = 0; i < glareCount; i++)
        {
            Image img = CreateImage("Glare", glareSprite);
            RectTransform rt = img.rectTransform;
            rt.SetParent(glareParent, false);

            float targetRotation = Random.Range(-glareMaxRotation, glareMaxRotation);
            float hueOffset = Random.value;

            Color baseColor = GetRandomColor(glareColors, glareAlpha);

            StartCoroutine(AnimateGlare(rt, img, targetRotation, hueOffset, baseColor));
            yield return new WaitForSeconds(glareSpawnInterval);
        }
    }

    IEnumerator AnimateGlare(
        RectTransform rt,
        Image img,
        float targetRotation,
        float hueOffset,
        Color baseColor)
    {
        float time = 0f;
        float scale = glareStartScale;

        rt.localScale = Vector3.one * scale;

        while (time < glareLifetime)
        {
            // NO LERP — linear scale growth
            scale += glareScaleSpeed * Time.deltaTime;
            scale = Mathf.Min(scale, glareEndScale);
            rt.localScale = Vector3.one * scale;

            rt.localRotation = Quaternion.Lerp(
                Quaternion.identity,
                Quaternion.Euler(0, 0, targetRotation),
                time / glareLifetime
            );

            img.color = glareRainbow
                ? GetRainbowColor(baseColor,glareAlpha, glareRainbowSpeed, hueOffset, time)
                : baseColor;

            time += Time.deltaTime;
            yield return null;
        }

        Destroy(rt.gameObject);
    }

    Image CreateImage(string name, Sprite sprite)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(transform, false);

        Image img = go.AddComponent<Image>();
        img.sprite = sprite;
        img.raycastTarget = false;
        img.material = additiveMaterial;


        RectTransform rt = img.rectTransform;
        rt.anchoredPosition = Vector2.zero;
        rt.localScale = Vector3.one * 0.01f;

        return img;
    }

    Color GetRandomColor(Color[] colors, float alpha)
    {
        if (colors == null || colors.Length == 0)
            return new Color(1f, 1f, 1f, alpha);

        Color c = colors[Random.Range(0, colors.Length)];
        c.a = alpha;
        return c;
    }

    Color GetRainbowColor(Color baseColor,float alpha, float speed, float hueOffset, float time, float minSaturation = 0.85f)
    {
        float h, s, v;
        Color.RGBToHSV(baseColor, out h, out s, out v);

        // Force saturation so white can become colorful
        s = Mathf.Max(s, minSaturation);

        h = Mathf.Repeat(h + time * speed + hueOffset, 1f);

        Color c = Color.HSVToRGB(h, s, v);
        c.a = alpha;
        return c;
    }
}
