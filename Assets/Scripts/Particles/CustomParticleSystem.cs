using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(RectTransform))]
public class CustomParticleSystem : MonoBehaviour
{
    [Header("Particle Data")]
    public ParticleEffect particleEffect;
    public ElementalType elementalType = ElementalType.None;

    [Header("Tuning")]
    public float scale = 1f;      // global scale
    public float pxToUnit = 0.01f; // pixels -> Canvas units
    public bool playOnStart = true;

    private PListParticle plist;
    private RectTransform rt;
    private List<Particle> particles = new List<Particle>();
    private float spritePPU = 100f; // will be overwritten if sprite available

    private class Particle
    {
        public Vector2 position;
        public Vector2 velocity;
        public float lifetime;
        public float maxLifetime;
        public float startSize;
        public float endSize;
        public float rotation;         // current rotation
        public float rotationSpeed;    // deg/sec
        public float rotationStart;
        public float rotationEnd;
        public Color startColor;
        public Color endColor;
        public Image image;
        public float uiStartSize;
        public float uiEndSize;
    }

    private void Awake()
    {
        rt = GetComponent<RectTransform>();
    }

    private void Start()
    {
        if (particleEffect?.plistJson != null)
        {
            plist = JsonUtility.FromJson<PListParticle>(particleEffect.plistJson.text);
        }
        else
        {
            Debug.LogError("No plist JSON assigned.");
            return;
        }

        if (particleEffect.sprite != null) 
            spritePPU = Mathf.Max(1f, particleEffect.sprite.pixelsPerUnit); 
        else 
            spritePPU = 100f; // fallback

        if (playOnStart)
            EmitParticles();
    }

    private void EmitParticles()
    {
        int count = Mathf.RoundToInt(plist.maxParticles);
        for (int i = 0; i < count; i++)
        {
            Particle p = new Particle();

            // -----------------------
            // Lifetime
            // -----------------------
            float life = plist.particleLifespan;
            if (Mathf.Approximately(life, 0f) && plist.particleLifespanVariance > 0f)
                life = plist.particleLifespanVariance;
            float lifeVar = plist.particleLifespanVariance;
            p.maxLifetime = Mathf.Max(0.01f, UnityEngine.Random.Range(life - lifeVar * 0.5f, life + lifeVar * 0.5f));
            p.lifetime = 0f;

            // -----------------------
            // Position & Velocity
            // -----------------------
            float posX = UnityEngine.Random.Range(-plist.sourcePositionVariancex, plist.sourcePositionVariancex);
            float posY = UnityEngine.Random.Range(-plist.sourcePositionVariancey, plist.sourcePositionVariancey);
            p.position = new Vector2(posX, posY) * pxToUnit * scale;

            float angle = plist.angle + UnityEngine.Random.Range(-plist.angleVariance * 0.5f, plist.angleVariance * 0.5f);
            float speed = plist.speed + UnityEngine.Random.Range(-plist.speedVariance * 0.5f, plist.speedVariance * 0.5f);
            float rad = angle * Mathf.Deg2Rad;
            p.velocity = new Vector2(Mathf.Cos(rad), Mathf.Sin(rad)) * speed * pxToUnit * scale;

            // Gravity and acceleration
            Vector2 gravity = new Vector2(plist.gravityx, plist.gravityy) * pxToUnit * scale;
            p.velocity += gravity * 0.5f; // initial bias

            // -----------------------
            // Size - FIXED
            // -----------------------
            
            // RAW plist sizes (in pixels)
            float start = plist.startParticleSize 
                        + UnityEngine.Random.Range(-plist.startParticleSizeVariance * 0.5f, plist.startParticleSizeVariance * 0.5f);

            float end = plist.finishParticleSize 
                        + UnityEngine.Random.Range(-plist.finishParticleSizeVariance * 0.5f, plist.finishParticleSizeVariance * 0.5f);

            p.startSize = Mathf.Max(0.01f, start);
            p.endSize   = Mathf.Max(0.01f, end);

            // Convert from pixels to UI units and apply scale
            // The key fix: use pxToUnit for conversion, then apply scale
            p.uiStartSize = p.startSize * pxToUnit * scale;
            p.uiEndSize = p.endSize * pxToUnit * scale;

            // -----------------------
            // Rotation
            // -----------------------
            float startRot = plist.rotationStart + UnityEngine.Random.Range(-plist.rotationStartVariance * 0.5f, plist.rotationStartVariance * 0.5f);
            float endRot = startRot + plist.rotatePerSecond * p.maxLifetime + UnityEngine.Random.Range(0f, plist.rotationEndVariance);
            p.rotationStart = startRot;
            p.rotationEnd = endRot;

            // -----------------------
            // Color
            // -----------------------
            ParticleSystem.MinMaxGradient grad = GradientFromStartFinishWithVariance(plist);
            p.startColor = grad.colorMin;
            p.endColor = grad.colorMax;

            // -----------------------
            // Image
            // -----------------------
            GameObject go = new GameObject("Particle", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(transform, false);
            p.image = go.GetComponent<Image>();
            p.image.sprite = particleEffect.sprite;
            p.image.color = p.startColor;
            go.GetComponent<RectTransform>().anchoredPosition = p.position;
            go.GetComponent<RectTransform>().sizeDelta = Vector2.one * p.uiStartSize;

            particles.Add(p);
        }
    }

    private void Update()
    {
        float dt = Time.deltaTime;

        for (int i = particles.Count - 1; i >= 0; i--)
        {
            Particle p = particles[i];
            p.lifetime += dt;

            if (p.lifetime >= p.maxLifetime)
            {
                Destroy(p.image.gameObject);
                particles.RemoveAt(i);
                continue;
            }

            float t = p.lifetime / p.maxLifetime;

            // -----------------------
            // Update position
            // -----------------------
            p.position += p.velocity * dt;
            p.image.rectTransform.anchoredPosition = p.position;

            // -----------------------
            // Update size (absolute units!)
            // -----------------------
            float size = Mathf.Lerp(p.uiStartSize, p.uiEndSize, t);
            p.image.rectTransform.sizeDelta = new Vector2(size, size);

            // -----------------------
            // Update rotation
            // -----------------------
            float rot = Mathf.Lerp(p.rotationStart, p.rotationEnd, t);
            p.image.rectTransform.localEulerAngles = new Vector3(0f, 0f, rot);

            // -----------------------
            // Update color
            // -----------------------
            p.image.color = Color.Lerp(p.startColor, p.endColor, t);
        }
    }


    private ParticleSystem.MinMaxGradient GradientFromStartFinishWithVariance(PListParticle p)
    {
        Color start = ELEMENT_TINT[(int)elementalType];
        Color finish = ELEMENT_TINT[(int)elementalType];

        Color startLow = new Color(
            Mathf.Clamp01(start.r - p.startColorVarianceRed),
            Mathf.Clamp01(start.g - p.startColorVarianceGreen),
            Mathf.Clamp01(start.b - p.startColorVarianceBlue),
            Mathf.Clamp01(start.a - p.startColorVarianceAlpha)
        );
        Color startHigh = new Color(
            Mathf.Clamp01(start.r + p.startColorVarianceRed),
            Mathf.Clamp01(start.g + p.startColorVarianceGreen),
            Mathf.Clamp01(start.b + p.startColorVarianceBlue),
            Mathf.Clamp01(start.a + p.startColorVarianceAlpha)
        );

        Color finishLow = new Color(
            Mathf.Clamp01(finish.r - p.finishColorVarianceRed),
            Mathf.Clamp01(finish.g - p.finishColorVarianceGreen),
            Mathf.Clamp01(finish.b - p.finishColorVarianceBlue),
            Mathf.Clamp01(finish.a - p.finishColorVarianceAlpha)
        );
        Color finishHigh = new Color(
            Mathf.Clamp01(finish.r + p.finishColorVarianceRed),
            Mathf.Clamp01(finish.g + p.finishColorVarianceGreen),
            Mathf.Clamp01(finish.b + p.finishColorVarianceBlue),
            Mathf.Clamp01(finish.a + p.finishColorVarianceAlpha)
        );

        Color minCol = new Color((startLow.r + finishLow.r) * 0.5f, (startLow.g + finishLow.g) * 0.5f, (startLow.b + finishLow.b) * 0.5f, (startLow.a + finishLow.a) * 0.5f);
        Color maxCol = new Color((startHigh.r + finishHigh.r) * 0.5f, (startHigh.g + finishHigh.g) * 0.5f, (startHigh.b + finishHigh.b) * 0.5f, (startHigh.a + finishHigh.a) * 0.5f);

        return new ParticleSystem.MinMaxGradient(minCol, maxCol);
    }

    public static readonly Color[] ELEMENT_TINT = new Color[]
    {
        new Color(1f, 0.31f, 0.31f), // Fire
        new Color(0.31f, 0.47f, 1f), // Water
        new Color(0.31f, 1f, 0.47f), // Earth
        new Color(1f, 1f, 0.31f),    // Thunder
        new Color(1f, 0.71f, 1f),    // Light
        new Color(0.71f, 0.31f, 1f), // Dark
        new Color(1f, 1f, 1f)        // None
    };
}

