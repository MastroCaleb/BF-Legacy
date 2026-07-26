using System;
using UnityEngine;

[RequireComponent(typeof(ParticleSystem))]
public class ParticleEffectPlayer : MonoBehaviour
{
    [Header("Data")]
    public ParticleEffect particleEffect;
    public ElementalType elementalType = ElementalType.None;

    [Header("Runtime / Tuning")]
    public float scale = 1f;
    public float pxToUnit = 0.01f;
    public bool playOnStart = true;
    public bool createBurst = true;

    private ParticleSystem ps;
    private PListParticle plist;

    void Awake()
    {
        ps = GetComponent<ParticleSystem>();
    }

    void Start()
    {
        if (particleEffect == null)
        {
            Debug.LogWarning("ParticleEffect not assigned.");
            return;
        }

        if (particleEffect.plistJson != null)
        {
            try
            {
                plist = JsonUtility.FromJson<PListParticle>(particleEffect.plistJson.text);
            }
            catch (Exception e)
            {
                Debug.LogError("Failed to parse plist JSON: " + e.Message);
                plist = null;
            }
        }

        BuildFromPlist();

        if (playOnStart)
            ps.Play();
    }

    // -----------------------
    // Main Build
    // -----------------------
    public void BuildFromPlist()
    {
        if (ps == null) ps = GetComponent<ParticleSystem>();

        ps.Clear();
        ps.Stop();

        var main = ps.main;
        var emission = ps.emission;
        var shape = ps.shape;
        var sizeOverLife = ps.sizeOverLifetime;
        var rotationOverLife = ps.rotationOverLifetime;
        var colorOverLife = ps.colorOverLifetime;
        var velocityOverLife = ps.velocityOverLifetime;
        var forceOverLife = ps.forceOverLifetime;
        var textureSheet = ps.textureSheetAnimation;

        if (plist == null)
        {
            // default fallback
            main.duration = 1f;
            main.loop = true;
            main.startLifetime = 1f;
            main.startSpeed = 5f;
            main.startSize = 1f;
            emission.rateOverTime = 10f;
            return;
        }

        // -----------------------
        // Main settings
        // -----------------------
        float duration = Mathf.Approximately(plist.duration, 0f) ? 0.01f : plist.duration;
        main.duration = Mathf.Max(0.01f, duration);
        main.loop = plist.duration < 0f;

        main.maxParticles = Mathf.Clamp((int)Mathf.Max(1f, plist.maxParticles), 1, 20000);

        // -----------------------
        // Lifetime
        // -----------------------
        float lifespan = Mathf.Max(0.01f, plist.particleLifespan);
        float lifeVar = Mathf.Max(0f, plist.particleLifespanVariance);
        float lifeMin = Mathf.Max(0.01f, lifespan - lifeVar * 0.5f);
        float lifeMax = Mathf.Max(0.01f, lifespan + lifeVar * 0.5f);
        main.startLifetime = new ParticleSystem.MinMaxCurve(lifeMin, lifeMax);

        // -----------------------
        // Size
        // -----------------------
        float startSize = Mathf.Max(0.01f, plist.startParticleSize * pxToUnit * scale);
        float startVar = Mathf.Max(0f, plist.startParticleSizeVariance * pxToUnit * scale);
        float sizeMin = Mathf.Max(0.01f, startSize - startVar * 0.5f);
        float sizeMax = startSize + startVar * 0.5f;
        main.startSize = new ParticleSystem.MinMaxCurve(sizeMin, sizeMax);

        // Size over lifetime (linear growth from start to finish)
        float finishSize = Mathf.Max(0.01f, plist.finishParticleSize * pxToUnit * scale);
        float finishVar = Mathf.Max(0f, plist.finishParticleSizeVariance * pxToUnit * scale);
        sizeOverLife.enabled = true;
        var sizeCurve = new AnimationCurve(
            new Keyframe(0f, 1f),
            new Keyframe(1f, (finishSize + finishVar * 0.5f) / sizeMax)
        );
        sizeOverLife.size = new ParticleSystem.MinMaxCurve(1f, sizeCurve);

        // -----------------------
        // Shape & Emission
        // -----------------------
        bool hasVariance = plist.sourcePositionVariancex > 0.01f || plist.sourcePositionVariancey > 0.01f;
        float pxToWorld = pxToUnit * scale;

        if (hasVariance)
        {
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(
                plist.sourcePositionVariancex * pxToWorld * 2f,
                plist.sourcePositionVariancey * pxToWorld * 2f,
                0f
            );
        }
        else
        {
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = 0.01f;
        }

        // -----------------------
        // Emission rate
        // -----------------------
        emission.enabled = true;
        float lifeAvg = (lifeMin + lifeMax) * 0.5f;
        float approxRate = (plist.maxParticles > 0f && lifeAvg > 0f) 
            ? (plist.maxParticles / lifeAvg) 
            : (plist.maxParticles / Mathf.Max(0.01f, duration));
        emission.rateOverTime = approxRate;

        if (createBurst)
        {
            var b = new ParticleSystem.Burst[1];
            short burstCount = (short)Mathf.Clamp(Mathf.RoundToInt(plist.maxParticles * 0.5f), 1, 3000);
            b[0] = new ParticleSystem.Burst(0f, burstCount);
            emission.SetBursts(b);
        }

        // -----------------------
        // Velocity (Radial + Tangential)
        // -----------------------
        velocityOverLife.enabled = true;
        velocityOverLife.space = ParticleSystemSimulationSpace.Local;

        float angleRad = plist.angle * Mathf.Deg2Rad;
        float speedAvg = plist.speed * pxToWorld;
        float speedVar = plist.speedVariance * pxToWorld;
        float vx = Mathf.Cos(angleRad) * speedAvg;
        float vy = Mathf.Sin(angleRad) * speedAvg;
        vx += UnityEngine.Random.Range(-speedVar, speedVar);
        vy += UnityEngine.Random.Range(-speedVar, speedVar);

        var vxCurve = new ParticleSystem.MinMaxCurve(vx, vx);
        var vyCurve = new ParticleSystem.MinMaxCurve(vy, vy);
        var vzCurve = new ParticleSystem.MinMaxCurve(0f, 0f);

        vxCurve.mode = ParticleSystemCurveMode.TwoConstants;
        vyCurve.mode = ParticleSystemCurveMode.TwoConstants;
        vzCurve.mode = ParticleSystemCurveMode.TwoConstants;

        velocityOverLife.x = vxCurve;
        velocityOverLife.y = vyCurve;
        velocityOverLife.z = vzCurve;

        // -----------------------
        // Gravity / Force
        // -----------------------
        forceOverLife.enabled = true;
        forceOverLife.space = ParticleSystemSimulationSpace.World;

        var fx = new ParticleSystem.MinMaxCurve(plist.gravityx * pxToWorld);
        var fy = new ParticleSystem.MinMaxCurve(plist.gravityy * pxToWorld);
        var fz = new ParticleSystem.MinMaxCurve(0f);

        fx.mode = ParticleSystemCurveMode.TwoConstants;
        fy.mode = ParticleSystemCurveMode.TwoConstants;
        fz.mode = ParticleSystemCurveMode.TwoConstants;

        forceOverLife.x = fx;
        forceOverLife.y = fy;
        forceOverLife.z = fz;

        // -----------------------
        // Rotation over lifetime
        // -----------------------
        rotationOverLife.enabled = true;
        rotationOverLife.separateAxes = false;
        float rotMin = (plist.rotatePerSecond - plist.rotatePerSecondVariance * 0.5f) * Mathf.Deg2Rad;
        float rotMax = (plist.rotatePerSecond + plist.rotatePerSecondVariance * 0.5f) * Mathf.Deg2Rad;
        var rotCurve = new ParticleSystem.MinMaxCurve(rotMin, rotMax);
        rotCurve.mode = ParticleSystemCurveMode.TwoConstants;
        rotationOverLife.z = rotCurve;

        main.startRotation = new ParticleSystem.MinMaxCurve(
            (plist.rotationStart - plist.rotationStartVariance * 0.5f) * Mathf.Deg2Rad,
            (plist.rotationStart + plist.rotationStartVariance * 0.5f) * Mathf.Deg2Rad
        );

        // -----------------------
        // Colors
        // -----------------------
        colorOverLife.enabled = true;
        colorOverLife.color = GradientFromStartFinishWithVariance(plist);

        // -----------------------
        // Texture / Sprite
        // -----------------------
        var renderer = ps.GetComponent<ParticleSystemRenderer>();
        if (renderer != null)
        {
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            if (particleEffect != null && particleEffect.sprite != null)
            {
                textureSheet.enabled = true;
                textureSheet.mode = ParticleSystemAnimationMode.Sprites;
                textureSheet.RemoveSprite(0);
                try { textureSheet.AddSprite(particleEffect.sprite); } catch { }
            }
        }

        ps.transform.localScale = Vector3.one * scale;
    }

    // -----------------------
    // Gradient helper
    // -----------------------
    private ParticleSystem.MinMaxGradient GradientFromStartFinishWithVariance(PListParticle p)
    {
        Color start, finish;

        start = ELEMENT_TINT[(int)elementalType];
        finish = ELEMENT_TINT[(int)elementalType];

        // Apply variance
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

        // Unity MinMaxGradient using two colors (min/max)
        Color minCol = new Color(
            (startLow.r + finishLow.r) * 0.5f,
            (startLow.g + finishLow.g) * 0.5f,
            (startLow.b + finishLow.b) * 0.5f,
            (startLow.a + finishLow.a) * 0.5f
        );

        Color maxCol = new Color(
            (startHigh.r + finishHigh.r) * 0.5f,
            (startHigh.g + finishHigh.g) * 0.5f,
            (startHigh.b + finishHigh.b) * 0.5f,
            (startHigh.a + finishHigh.a) * 0.5f
        );

        return new ParticleSystem.MinMaxGradient(minCol, maxCol);
    }

    public void Replay()
    {
        if (ps == null) ps = GetComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        ps.Play();
    }

    public static readonly Color32[] ELEMENT_TINT = new Color32[]
    {
        new Color32(255, 80, 80, 255),    // Fire
        new Color32(80, 120, 255, 255),   // Water
        new Color32(80, 255, 120, 255),   // Earth
        new Color32(255, 255, 80, 255),   // Thunder
        new Color32(255, 180, 255, 255),  // Light
        new Color32(180, 80, 255, 255),   // Dark
        new Color32(255, 255, 255, 255)   // None
    };
}
