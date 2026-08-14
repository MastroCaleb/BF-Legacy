using System;
using System.IO;
using System.IO.Compression;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Raw field-for-field mirror of the cocos2d-x CCParticleSystemQuad plist,
/// as converted to JSON. Field names must match the JSON keys exactly for
/// JsonUtility to deserialize it directly.
/// </summary>
[Serializable]
public class CocosParticleData
{
    public float angle;
    public float angleVariance;
    public float blendFuncDestination;
    public float blendFuncSource;
    public float duration;              // -1 = infinite emitter
    public float emitterType;           // 0 = Gravity (Mode A), 1 = Radius (Mode B)
    public float finishColorAlpha;
    public float finishColorBlue;
    public float finishColorGreen;
    public float finishColorRed;
    public float finishColorVarianceAlpha;
    public float finishColorVarianceBlue;
    public float finishColorVarianceGreen;
    public float finishColorVarianceRed;
    public float finishParticleSize;
    public float finishParticleSizeVariance;
    public float gravityx;
    public float gravityy;
    public float maxParticles;
    public float maxRadius;
    public float maxRadiusVariance;
    public float minRadius;
    public float minRadiusVariance;
    public float particleLifespan;
    public float particleLifespanVariance;
    public float radialAccelVariance;
    public float radialAcceleration;
    public float rotatePerSecond;           // Mode B only: orbital angular speed
    public float rotatePerSecondVariance;
    public float rotationEnd;               // sprite spin end angle (both modes)
    public float rotationEndVariance;
    public float rotationStart;             // sprite spin start angle (both modes)
    public float rotationStartVariance;
    public float sourcePositionVariancex;
    public float sourcePositionVariancey;
    public float sourcePositionx;
    public float sourcePositiony;
    public float speed;
    public float speedVariance;
    public float startColorAlpha;
    public float startColorBlue;
    public float startColorGreen;
    public float startColorRed;
    public float startColorVarianceAlpha;
    public float startColorVarianceBlue;
    public float startColorVarianceGreen;
    public float startColorVarianceRed;
    public float startParticleSize;
    public float startParticleSizeVariance;
    public float tangentialAccelVariance;
    public float tangentialAcceleration;
    public string textureFileName;
    public string textureImageData;     // base64(gzip(png)) embedded fallback texture
}

/// <summary>
/// Simulates and renders a cocos2d-x particle system as a Unity UI Graphic.
/// This is a direct port of CCParticleSystem::update()/initParticle(), not an
/// approximation via Shuriken modules, so timing/position/color/rotation match
/// the original 1:1 (modulo the assumptions noted below).
///
/// Setup:
///   1. Add this component to a UI GameObject under a Canvas.
///   2. Create a Material using the "UI/CocosParticleBlend" shader (see the
///      accompanying .shader file) and assign it to the Graphic's Material slot.
///   3. Call LoadFromJson(jsonText) or SetData(parsedData) to start playback.
///
/// Assumptions to verify against a known-good effect:
///   - PositionType is treated as "Free": sourcePosition is authored in cocos
///     point-space (e.g. 160,240 = center of a 320x480 reference canvas) with
///     Y-up, origin bottom-left. Set `originOffset` to (-refW/2, -refH/2) if
///     this RectTransform's pivot is centered on that same reference canvas,
///     or to (0,0) if the RectTransform's origin already matches cocos' origin.
///   - Sprite rotation sign uses cocos' clockwise-positive convention. If a
///     spinning effect looks mirrored, flip the sign in OnPopulateMesh.
///   - Radial/tangential acceleration in Mode A normalizes particle.pos
///     directly (this is the documented cocos2d-x algorithm) rather than
///     pos-relative-to-spawn; matches upstream behavior.
/// </summary>
[RequireComponent(typeof(CanvasRenderer))]
public class CocosParticleEffect : MaskableGraphic
{
    [Header("Source")]
    public TextAsset jsonSource;
    [SerializeField] private CocosParticleData data;

    [Header("Playback")]
    public bool autoPlay = true;
    public bool useEmbeddedTexture = true;
    [Tooltip("If assigned, takes priority over the embedded/decoded texture (e.g. a properly imported 015.png).")]
    public Texture2D overrideTexture;

    [Tooltip("Offset added to raw cocos sourcePosition to align with this RectTransform's local origin.")]
    public Vector2 originOffset = Vector2.zero;

    private Texture2D _decodedTexture;
    public override Texture mainTexture =>
        overrideTexture != null ? overrideTexture :
        (_decodedTexture != null ? _decodedTexture : s_WhiteTexture);

    private struct Particle
    {
        public Vector2 pos;

        // Mode A (gravity)
        public Vector2 dirModeA;
        public float radialAccel, tangentialAccel;

        // Mode B (radius)
        public float radiusB, deltaRadiusB, angleB, degreesPerSecondB;

        public Color color, deltaColor;
        public float size, deltaSize;
        public float rotation, deltaRotation;
        public float timeToLive;
    }

    private readonly List<Particle> _particles = new List<Particle>();
    private float _elapsed;
    private float _emitCounter;
    private bool _emitting;
    private float _emissionRate;
    private readonly System.Random _rng = new System.Random();

    private TextAsset _loadedFrom; // tracks which jsonSource we've already consumed

    protected override void Awake()
    {
        base.Awake();
        TryAutoLoad();
    }

    private void TryAutoLoad()
    {
        // Loads lazily rather than only in Awake, so assigning jsonSource in the
        // Inspector after entering Play Mode (or on an object that starts
        // inactive) still works instead of silently doing nothing.
        if (jsonSource != null && jsonSource != _loadedFrom)
        {
            _loadedFrom = jsonSource;
            LoadFromJson(jsonSource.text);
        }
    }

    [ContextMenu("Reload / Play")]
    public void LoadFromJson(string json)
    {
        if (string.IsNullOrEmpty(json))
        {
            Debug.LogError("CocosParticleEffect: json text is empty.", this);
            return;
        }

        CocosParticleData parsed;
        try
        {
            parsed = JsonUtility.FromJson<CocosParticleData>(json);
        }
        catch (Exception e)
        {
            Debug.LogError($"CocosParticleEffect: JSON parse failed: {e}", this);
            return;
        }

        if (parsed == null)
        {
            Debug.LogError("CocosParticleEffect: JsonUtility returned null. Check the JSON is a single valid object.", this);
            return;
        }

        Debug.Log($"CocosParticleEffect: parsed OK - maxParticles={parsed.maxParticles}, " +
                  $"lifespan={parsed.particleLifespan}, duration={parsed.duration}, " +
                  $"emitterType={parsed.emitterType}, hasTexData={!string.IsNullOrEmpty(parsed.textureImageData)}", this);

        SetData(parsed);
    }

    public void SetData(CocosParticleData d)
    {
        data = d;
        Play();
    }

    public void Play()
    {
        if (data == null) return;

        _particles.Clear();
        _elapsed = 0f;
        _emitCounter = 0f;
        _emitting = true;
        _emissionRate = data.particleLifespan > 0f
            ? data.maxParticles / data.particleLifespan
            : data.maxParticles;

        if (useEmbeddedTexture && !string.IsNullOrEmpty(data.textureImageData))
            _decodedTexture = DecodeEmbeddedTexture(data.textureImageData);

        ApplyBlendMode();
        SetVerticesDirty();
        enabled = true;
    }

    private void ApplyBlendMode()
    {
        if (material == null) return;
        // Requires the UI/CocosParticleBlend shader (exposes _SrcBlend/_DstBlend).
        if (material.HasProperty("_SrcBlend"))
            material.SetFloat("_SrcBlend", (float)GLToUnityBlend(data.blendFuncSource));
        if (material.HasProperty("_DstBlend"))
            material.SetFloat("_DstBlend", (float)GLToUnityBlend(data.blendFuncDestination));
    }

    private static UnityEngine.Rendering.BlendMode GLToUnityBlend(float glConst)
    {
        switch ((int)glConst)
        {
            case 0:   return UnityEngine.Rendering.BlendMode.Zero;              // GL_ZERO
            case 1:   return UnityEngine.Rendering.BlendMode.One;               // GL_ONE
            case 768: return UnityEngine.Rendering.BlendMode.SrcColor;          // GL_SRC_COLOR
            case 769: return UnityEngine.Rendering.BlendMode.OneMinusSrcColor;  // GL_ONE_MINUS_SRC_COLOR
            case 770: return UnityEngine.Rendering.BlendMode.SrcAlpha;          // GL_SRC_ALPHA
            case 771: return UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha;  // GL_ONE_MINUS_SRC_ALPHA
            case 772: return UnityEngine.Rendering.BlendMode.DstAlpha;          // GL_DST_ALPHA
            case 773: return UnityEngine.Rendering.BlendMode.OneMinusDstAlpha;  // GL_ONE_MINUS_DST_ALPHA
            case 774: return UnityEngine.Rendering.BlendMode.DstColor;          // GL_DST_COLOR
            case 775: return UnityEngine.Rendering.BlendMode.OneMinusDstColor;  // GL_ONE_MINUS_DST_COLOR
            default:  return UnityEngine.Rendering.BlendMode.One;
        }
    }

    public static Texture2D DecodeEmbeddedTexture(string base64Gzip)
    {
        try
        {
            byte[] compressed = Convert.FromBase64String(base64Gzip);
            using (var input = new MemoryStream(compressed))
            using (var gzip = new GZipStream(input, CompressionMode.Decompress))
            using (var output = new MemoryStream())
            {
                gzip.CopyTo(output);
                var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                tex.LoadImage(output.ToArray());
                tex.filterMode = FilterMode.Bilinear;
                return tex;
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"CocosParticleEffect: failed to decode embedded texture: {e}");
            return null;
        }
    }

    protected override void OnEnable()
    {
        base.OnEnable();
        TryAutoLoad();
    }

    private void Update()
    {
        TryAutoLoad(); // picks up jsonSource assigned/changed after Awake, e.g. mid-Play-Mode
        if (data == null) return;
        float dt = Time.deltaTime;

        // Spawn first, THEN advance elapsed/check duration - matches cocos2d-x's
        // ParticleSystem::update() order. Checking duration first can zero out
        // emission on the very frame a particle was due to spawn (e.g. when
        // maxParticles/particleLifespan/duration all line up, as in a 1-particle,
        // 1s-lifespan, 1s-duration burst).
        if (_emitting && _emissionRate > 0f)
        {
            _emitCounter += dt;
            float rate = 1f / _emissionRate;
            while (_particles.Count < (int)data.maxParticles && _emitCounter > rate)
            {
                _particles.Add(SpawnParticle());
                _emitCounter -= rate;
            }
        }

        _elapsed += dt;
        if (_emitting && data.duration >= 0f && _elapsed >= data.duration)
            _emitting = false;

        for (int i = _particles.Count - 1; i >= 0; i--)
        {
            var p = _particles[i];
            if (StepParticle(ref p, dt))
                _particles[i] = p;
            else
                _particles.RemoveAt(i);
        }

        SetVerticesDirty();
    }

    private float RandM11() => (float)(_rng.NextDouble() * 2.0 - 1.0);

    private Particle SpawnParticle()
    {
        var p = new Particle();

        float lifespan = data.particleLifespan + data.particleLifespanVariance * RandM11();
        p.timeToLive = Mathf.Max(0f, lifespan);

        p.pos = new Vector2(
            data.sourcePositionx + data.sourcePositionVariancex * RandM11(),
            data.sourcePositiony + data.sourcePositionVariancey * RandM11());

        float sr = Mathf.Clamp01(data.startColorRed + data.startColorVarianceRed * RandM11());
        float sg = Mathf.Clamp01(data.startColorGreen + data.startColorVarianceGreen * RandM11());
        float sb = Mathf.Clamp01(data.startColorBlue + data.startColorVarianceBlue * RandM11());
        float sa = Mathf.Clamp01(data.startColorAlpha + data.startColorVarianceAlpha * RandM11());
        float er = Mathf.Clamp01(data.finishColorRed + data.finishColorVarianceRed * RandM11());
        float eg = Mathf.Clamp01(data.finishColorGreen + data.finishColorVarianceGreen * RandM11());
        float eb = Mathf.Clamp01(data.finishColorBlue + data.finishColorVarianceBlue * RandM11());
        float ea = Mathf.Clamp01(data.finishColorAlpha + data.finishColorVarianceAlpha * RandM11());
        p.color = new Color(sr, sg, sb, sa);
        p.deltaColor = p.timeToLive > 0f
            ? new Color((er - sr) / p.timeToLive, (eg - sg) / p.timeToLive, (eb - sb) / p.timeToLive, (ea - sa) / p.timeToLive)
            : Color.clear;

        float startSize = Mathf.Max(0f, data.startParticleSize + data.startParticleSizeVariance * RandM11());
        p.size = startSize;
        float endSize = Mathf.Max(0f, data.finishParticleSize + data.finishParticleSizeVariance * RandM11());
        p.deltaSize = p.timeToLive > 0f ? (endSize - startSize) / p.timeToLive : 0f;

        float startAngle = data.rotationStart + data.rotationStartVariance * RandM11();
        float endAngle = data.rotationEnd + data.rotationEndVariance * RandM11();
        p.rotation = startAngle;
        p.deltaRotation = p.timeToLive > 0f ? (endAngle - startAngle) / p.timeToLive : 0f;

        if (data.emitterType < 0.5f) // Mode A: Gravity
        {
            float a = (data.angle + data.angleVariance * RandM11()) * Mathf.Deg2Rad;
            float sp = data.speed + data.speedVariance * RandM11();
            p.dirModeA = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * sp;
            p.radialAccel = data.radialAcceleration + data.radialAccelVariance * RandM11();
            p.tangentialAccel = data.tangentialAcceleration + data.tangentialAccelVariance * RandM11();
        }
        else // Mode B: Radius
        {
            p.radiusB = data.maxRadius + data.maxRadiusVariance * RandM11();
            float endRadius = data.minRadius + data.minRadiusVariance * RandM11();
            p.deltaRadiusB = p.timeToLive > 0f ? (endRadius - p.radiusB) / p.timeToLive : 0f;
            p.angleB = (data.angle + data.angleVariance * RandM11()) * Mathf.Deg2Rad;
            p.degreesPerSecondB = (data.rotatePerSecond + data.rotatePerSecondVariance * RandM11()) * Mathf.Deg2Rad;
        }

        return p;
    }

    private bool StepParticle(ref Particle p, float dt)
    {
        p.timeToLive -= dt;
        if (p.timeToLive <= 0f) return false;

        if (data.emitterType < 0.5f) // Mode A: Gravity
        {
            Vector2 radial = Vector2.zero;
            if (p.pos.x != 0f || p.pos.y != 0f) radial = p.pos.normalized;
            Vector2 tangential = radial;
            radial *= p.radialAccel;

            float newY = tangential.x;
            tangential.x = -tangential.y;
            tangential.y = newY;
            tangential *= p.tangentialAccel;

            Vector2 gravity = new Vector2(data.gravityx, data.gravityy);
            p.dirModeA += (radial + tangential + gravity) * dt;
            p.pos += p.dirModeA * dt;
        }
        else // Mode B: Radius
        {
            p.angleB += p.degreesPerSecondB * dt;
            p.radiusB += p.deltaRadiusB * dt;
            p.pos.x = -Mathf.Cos(p.angleB) * p.radiusB;
            p.pos.y = -Mathf.Sin(p.angleB) * p.radiusB;
        }

        p.color += p.deltaColor * dt;
        p.size = Mathf.Max(0f, p.size + p.deltaSize * dt);
        p.rotation += p.deltaRotation * dt;
        return true;
    }

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        if (data == null) return;

        for (int i = 0; i < _particles.Count; i++)
        {
            var p = _particles[i];
            float half = p.size * 0.5f;
            Vector2 center = p.pos + originOffset;

            // cocos rotation is clockwise-positive; negate for Unity's CCW math below.
            float rad = -p.rotation * Mathf.Deg2Rad;
            float cos = Mathf.Cos(rad), sin = Mathf.Sin(rad);

            Vector2 v0 = Rotate(new Vector2(-half, -half), cos, sin) + center;
            Vector2 v1 = Rotate(new Vector2(-half, half), cos, sin) + center;
            Vector2 v2 = Rotate(new Vector2(half, half), cos, sin) + center;
            Vector2 v3 = Rotate(new Vector2(half, -half), cos, sin) + center;

            int baseIdx = vh.currentVertCount;
            vh.AddVert(v0, p.color, new Vector2(0, 0));
            vh.AddVert(v1, p.color, new Vector2(0, 1));
            vh.AddVert(v2, p.color, new Vector2(1, 1));
            vh.AddVert(v3, p.color, new Vector2(1, 0));
            vh.AddTriangle(baseIdx, baseIdx + 1, baseIdx + 2);
            vh.AddTriangle(baseIdx, baseIdx + 2, baseIdx + 3);
        }
    }

    private static Vector2 Rotate(Vector2 v, float cos, float sin)
        => new Vector2(v.x * cos - v.y * sin, v.x * sin + v.y * cos);
}