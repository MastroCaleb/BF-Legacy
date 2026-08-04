using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.UI;

public class BraveFrontierFrameAnimator : MonoBehaviour
{
    [Header("Rendering Settings")]
    public bool initOnStart = false;
    public Vector2 pivot = new Vector2(0.5f, 0.5f);
    public int scale = 1;
    public int pixelsPerUnit = 32;

    [Header("CGG File")]
    public TextAsset cggFile;

    [Header("Animation (CGS) Files")]
    public TextAsset idleCgsFile;
    public TextAsset moveCgsFile;
    public TextAsset attackCgsFile;

    [Header("Additional Animations")]
    public List<NamedAnimation> additionalAnimations = new List<NamedAnimation>();

    [Serializable]
    public struct NamedAnimation
    {
        public string animationName;
        public TextAsset cgsFile;
    }

    [Header("Sprite Sheets")]
    public Texture2D[] spriteSheets;

    [Header("Playback Settings")]
    public bool playOnStart = true;
    public bool loop = true;
    public float loopDelay = 0f;
    public string animationName = "Idle";

    //Animations
    List<TexWithDuration> idleAnim = new List<TexWithDuration>();
    List<TexWithDuration> moveAnim = new List<TexWithDuration>();
    List<TexWithDuration> attackAnim = new List<TexWithDuration>();
    Dictionary<string, List<TexWithDuration>> additionalAnims = new Dictionary<string, List<TexWithDuration>>();

    // Lightweight cache for feet offsets
    static Dictionary<Texture2D, int> feetOffsetCache = new Dictionary<Texture2D, int>();

    // Generated assets for UI
    Image sr;
    public List<Sprite> frameSprites = new List<Sprite>();
    List<float> frameDurations = new List<float>();
    Coroutine playCoroutine;
    private bool isPaused = false;

    struct FrameCacheKey : IEquatable<FrameCacheKey>
    {
        public TextAsset cgg;
        public TextAsset cgs;
        public int scale;
        public int pixelsPerUnit;

        public bool Equals(FrameCacheKey other)
        {
            return cgg == other.cgg && cgs == other.cgs &&
                scale == other.scale && pixelsPerUnit == other.pixelsPerUnit;
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = 17;
                hash = hash * 23 + (cgg ? cgg.GetHashCode() : 0);
                hash = hash * 23 + (cgs ? cgs.GetHashCode() : 0);
                hash = hash * 23 + scale;
                hash = hash * 23 + pixelsPerUnit;
                return hash;
            }
        }
    }

    static Dictionary<FrameCacheKey, List<TexWithDuration>> frameCache = new Dictionary<FrameCacheKey, List<TexWithDuration>>();

    List<TexWithDuration> GetOrBuildFrames(TextAsset cgg, TextAsset cgs)
    {
        var key = new FrameCacheKey { cgg = cgg, cgs = cgs, scale = scale, pixelsPerUnit = pixelsPerUnit };

        if (frameCache.TryGetValue(key, out var cached))
            return cached;

        var frames = BuildFrames(cgg, cgs, spriteSheets);
        frameCache[key] = frames;
        return frames;
    }

    // --------------------------------------------------------------------

    void Start()
    {
        if (initOnStart)
        {
            InitializeAnimator();
        }
    }

    public void InitializeAnimator()
    {
        BuildAllAnimations();

        sr = GetComponent<Image>();

        if (sr != null)
        {
            // ===== UI FIX =====
            sr.preserveAspect = true;

            var texDur = idleAnim[0];
            int feetOffset = GetFeetOffset(texDur.tex); // function below
            var sprite = Sprite.Create(
                texDur.tex,
                new Rect(0, feetOffset, texDur.tex.width, texDur.tex.height - feetOffset),
                new Vector2(0.5f, 0f), // pivot bottom-center
                pixelsPerUnit
            );
            sr.sprite = sprite;
        }

        if (playOnStart && Application.isPlaying)
        {
            BuildAndPlay();
        }
    }

    public void InitializeCachedAnimator()
    {
        var key = new AnimationCacheKey
        {
            cgg = cggFile,
            idle = idleCgsFile,
            move = moveCgsFile,
            attack = attackCgsFile,
            scale = scale,
            pixelsPerUnit = pixelsPerUnit
        };

        if (!animationCache.TryGetValue(key, out var cached))
        {
            cached = new CachedAnimations
            {
                idle = BuildCachedAnimation(cggFile, idleCgsFile),
                move = moveCgsFile ? BuildCachedAnimation(cggFile, moveCgsFile) : null,
                attack = BuildCachedAnimation(cggFile, attackCgsFile)
            };

            animationCache.Add(key, cached);
        }

        ApplyCached(cached);
    }

    CachedAnimation BuildCachedAnimation(TextAsset cgg, TextAsset cgs)
    {
        var frames = GetOrBuildFrames(cgg, cgs);

        var sprites = new List<Sprite>(frames.Count);
        var durations = new List<float>(frames.Count);

        for (int i = 0; i < frames.Count; i++)
        {
            var texDur = frames[i];
            int feetOffset = GetFeetOffset(texDur.tex);

            var sprite = Sprite.Create(
                texDur.tex,
                new Rect(0, feetOffset, texDur.tex.width, texDur.tex.height - feetOffset),
                new Vector2(0.5f, 0f),
                pixelsPerUnit
            );

            sprites.Add(sprite);
            durations.Add(texDur.duration);
        }

        return new CachedAnimation
        {
            sprites = sprites,
            durations = durations
        };
    }

    void ApplyCached(CachedAnimations cached)
    {
        sr = GetComponent<Image>();
        if (sr) sr.preserveAspect = true;

        switch (animationName)
        {
            case "Move":
                frameSprites = cached.move?.sprites ?? cached.idle.sprites;
                frameDurations = cached.move?.durations ?? cached.idle.durations;
                loop = true;
                break;

            case "Attack":
                frameSprites = cached.attack.sprites;
                frameDurations = cached.attack.durations;
                loop = false;
                break;

            default:
                frameSprites = cached.idle.sprites;
                frameDurations = cached.idle.durations;
                loop = true;
                break;
        }

        if (playOnStart && Application.isPlaying)
            PlayCached();
    }

    void PlayCached()
    {
        if (playCoroutine != null)
            StopCoroutine(playCoroutine);

        playCoroutine = StartCoroutine(PlayFramesCoroutine());
    }

    public void BuildAllAnimations()
    {
        idleAnim = GetOrBuildFrames(cggFile, idleCgsFile);
        moveAnim = moveCgsFile == null ? idleAnim : GetOrBuildFrames(cggFile, moveCgsFile);
        attackAnim = GetOrBuildFrames(cggFile, attackCgsFile);

        additionalAnims.Clear();
        foreach (var named in additionalAnimations)
        {
            if (named.cgsFile == null || string.IsNullOrEmpty(named.animationName)) continue;
            additionalAnims[named.animationName] = GetOrBuildFrames(cggFile, named.cgsFile);
        }

        foreach(var t in idleAnim)
        {
            GetFeetOffset(t.tex);
        }
        foreach(var t in moveAnim)
        {
            GetFeetOffset(t.tex);
        }
        foreach(var t in attackAnim)
        {
            GetFeetOffset(t.tex);
        }
        foreach(var anim in additionalAnims.Values)
        {
            foreach(var t in anim)
            {
                GetFeetOffset(t.tex);
            }
        }
    }

    // PUBLIC entry: builds frames + plays
    public void BuildAndPlay()
    {
        if (cggFile == null || idleCgsFile == null || attackCgsFile == null || spriteSheets == null || spriteSheets.Length == 0)
        {
            Debug.LogError("BraveFrontierFrameAnimator: missing inputs.");
            return;
        }

        List<TexWithDuration> textures;
        
        ClearGenerated();

        switch (animationName)
        {
            case "Idle":
                loop = true;
                textures = idleAnim;
                break;
            case "Move":
                loop = true;
                textures = moveAnim;
                break;
            case "Attack":
                loop = false;
                textures = attackAnim;
                break;
            default:
                if (additionalAnims.TryGetValue(animationName, out var extra))
                {
                    loop = true;
                    textures = extra;
                }
                else
                {
                    textures = idleAnim;
                }
                break;
        }

        // convert Texture2D → Sprite list
        for (int i = 0; i < textures.Count; i++)
        {
            var texDur = textures[i];
            int feetOffset = GetFeetOffset(texDur.tex); // function below

            var sprite = Sprite.Create(
                texDur.tex,
                new Rect(0, feetOffset, texDur.tex.width, texDur.tex.height - feetOffset),
                new Vector2(0.5f, 0f), // pivot bottom-center
                pixelsPerUnit
            );

            frameSprites.Add(sprite);
            frameDurations.Add(texDur.duration);
        }

        if (playCoroutine != null) StopCoroutine(playCoroutine);
        playCoroutine = StartCoroutine(PlayFramesCoroutine());
    }

    public void SetAnimation(string animName)
    {
        animationName = animName;
        BuildAndPlay();
    }

    public float GetTotalDurationFrames(string animName)
    {
        float totalFrames = 0;
        List<TexWithDuration> animTextures;

        switch (animName)
        {
            case "Idle": animTextures = idleAnim; break;
            case "Move": animTextures = moveAnim; break;
            case "Attack": animTextures = attackAnim; break;
            default:
                animTextures = additionalAnims.TryGetValue(animName, out var extra) ? extra : idleAnim;
                break;
        }

        for (int i = 0; i < animTextures.Count; i++)
            totalFrames += animTextures[i].duration * 60f;

        return totalFrames / 60f;
    }

    public void ClearGenerated()
    {
        frameSprites.Clear();
        frameDurations.Clear();
    }

    public void StopPlay()
    {
        if (playCoroutine != null) StopCoroutine(playCoroutine);
        playCoroutine = null;
        isPaused = false;
    }

    public void Pause()
    {
        if (playCoroutine != null)
        {
            StopCoroutine(playCoroutine);
            playCoroutine = null;
            isPaused = true;
        }
    }

    public void Resume()
    {
        if (isPaused && frameSprites.Count > 0)
        {
            isPaused = false;
            if (playCoroutine != null) StopCoroutine(playCoroutine);
            playCoroutine = StartCoroutine(PlayFramesCoroutine());
        }
    }

    int GetFeetOffset(Texture2D tex)
    {
        if (feetOffsetCache.TryGetValue(tex, out int offset))
            return offset;

        offset = CalculateFeetOffset(tex);
        feetOffsetCache.Add(tex, offset);

        return offset;
    }

    // --------------------------------------------------------------------
    // PLAYBACK (updated for UI)
    // --------------------------------------------------------------------

    IEnumerator PlayFramesCoroutine()
    {
        if (sr == null) sr = GetComponent<Image>();
        if (sr == null) yield break;
        if (frameSprites.Count == 0) yield break;

        RectTransform rt = sr.rectTransform;
        rt.pivot = new Vector2(0.5f, 0f); // bottom-center pivot

        do
        {
            for (int i = 0; i < frameSprites.Count; i++)
            {
                sr.sprite = frameSprites[i];

                // Keep bottom anchored
                float newHeight = frameSprites[i].rect.height;
                rt.sizeDelta = new Vector2(frameSprites[i].rect.width, newHeight);

                // Optional: keep anchoredPosition fixed to bottom
                

                float dur = Mathf.Max(0.0001f, frameDurations[i]);
                yield return new WaitForSeconds(dur);
            }

            if (loop)
                yield return new WaitForSeconds(loopDelay);

        } while (loop);
    }

    void OnEnable()
    {
        if (playCoroutine == null)
            playCoroutine = StartCoroutine(PlayFramesCoroutine());
    }

    void OnDisable()
    {
        if (playCoroutine != null)
        {
            StopCoroutine(playCoroutine);
            playCoroutine = null;
        }
        isPaused = false;
    }

    // --------------------------------------------------------------------
    // FULL FRAME BUILDING LOGIC 
    // --------------------------------------------------------------------

    private struct TexWithDuration { public Texture2D tex; public float duration; }

    private List<TexWithDuration> BuildFrames(TextAsset cggFile, TextAsset cgsFile, Texture2D[] spriteSheets)
    {
        int[][] cggParsed = ParseCggCgsLikeJava(cggFile);
        int[][] cgsParsed = ParseCggCgsLikeJava(cgsFile);

        Debug.Log($"BF Builder: parsed cgg rows={cggParsed.Length}, cgs rows={cgsParsed.Length}, sheets={spriteSheets?.Length ?? 0}");

        int dimX, dimY, lowestPoint;
        ComputeDimensionsAndLowestPoint(cgsParsed, cggParsed, out dimX, out dimY, out lowestPoint);

        // Java canvas size:
        //   width  = dimX
        //   height = dimY - (dimY/2 - lowestPoint) = dimY/2 + lowestPoint
        int finalW = Mathf.Max(dimX, 32);
        int finalH = Mathf.Max(dimY, 32);

        if (cgsFile.name.Contains("crystal"))
            Debug.Log($"Crystal dims: dimX={dimX} dimY={dimY} lowestPoint={lowestPoint} finalW={finalW} finalH={finalH}");

        Debug.Log($"BF Builder: finalW={finalW} finalH={finalH} lowestPoint={lowestPoint} dimY={dimY}");

        var outputs = new List<TexWithDuration>();

        for (int i = 0; i < cgsParsed.Length; i++)
        {
            var seq = cgsParsed[i];
            if (seq.Length < 1) continue;

            int frameIndex = seq[0];
            float duration = seq.Length > 3 ? seq[3] / 60f : 1f / 60f;

            if (frameIndex < 0 || frameIndex >= cggParsed.Length)
                continue;

            int[] frameLine = cggParsed[frameIndex];
            int numParts = frameLine.Length > 1 ? frameLine[1] : 0;

            Texture2D frameTex = CreateTexture(finalW, finalH);

            for (int p = 0; p < numParts; p++)
            {
                int partIndex = numParts - 1 - p;
                int baseIdx = 2 + partIndex * 11;
                if (baseIdx + 10 >= frameLine.Length) continue;

                int   frameX    = frameLine[baseIdx + 0];
                int   frameY    = frameLine[baseIdx + 1];
                int   flip      = frameLine[baseIdx + 2];
                int   blendMode = frameLine[baseIdx + 3];
                float opacity   = frameLine[baseIdx + 4] / 100f;
                int   rotate    = frameLine[baseIdx + 5];
                int   spriteX   = frameLine[baseIdx + 6];
                int   spriteY   = frameLine[baseIdx + 7];
                int   width     = frameLine[baseIdx + 8];
                int   height    = frameLine[baseIdx + 9];
                int   page_id   = frameLine[baseIdx + 10];

                if (page_id < 0 || page_id >= spriteSheets.Length) continue;
                Texture2D sheet = spriteSheets[page_id];
                if (sheet == null) continue;

                int actualW, actualH;
                Color[] partPixels = GetSubTexturePixels(sheet, spriteX, spriteY, width, height, out actualW, out actualH);
                if (actualW <= 0 || actualH <= 0) continue;

                ApplyColorTransform(partPixels, opacity, blendMode);

                Texture2D partTex = new Texture2D(actualW, actualH, TextureFormat.RGBA32, false);
                partTex.SetPixels(partPixels);
                partTex.Apply();

                Texture2D transformed = ApplyRotationAndFlip(partTex, rotate, flip);

                // After the crop math cancels out, Java's Y placement simplifies to:
                //   partStartYJava = dimY/2 + frameY
                // X is just:
                //   partStartX = dimX/2 + frameX
                int partStartX     = dimX / 2 + frameX;
                int partStartYJava = dimY / 2 + frameY;

                if (rotate % 180 != 0)
                {
                    partStartX     += width  / 2 - height / 2;
                    partStartYJava += height / 2 - width  / 2;
                }

                // Effective height for Y-extent (rotated parts swap width/height)
                int effectiveH = (rotate % 180 != 0) ? width : height;

                // Convert Java top-left Y-down → Unity bottom-left Y-up
                int partStartY = finalH - partStartYJava - effectiveH;

                BlitOnto(frameTex, transformed, partStartX, partStartY, blendMode);

                SafeDestroy(partTex);
                if (!ReferenceEquals(transformed, partTex)) SafeDestroy(transformed);
            }

            Texture2D finalTex;
            if (scale > 1)
            {
                finalTex = UpsampleTexture(frameTex, scale);
                finalTex.filterMode = FilterMode.Point;
                SafeDestroy(frameTex);
            }
            else
            {
                finalTex = frameTex;
                finalTex.filterMode = FilterMode.Point;
            }

            outputs.Add(new TexWithDuration { tex = finalTex, duration = Mathf.Max(1f / 60f, duration) });
        }

        Debug.Log($"BF Frame Builder: generated {outputs.Count} frames.");
        return outputs;
    }

    // --------------------------------------------------------------------
    // Your helper methods (unchanged)
    // --------------------------------------------------------------------

    void SafeDestroy(UnityEngine.Object obj)
    {
        if (obj == null) return;
        if (Application.isPlaying) Destroy(obj);
        else DestroyImmediate(obj, true);
    }

    private string[] GetLinesLikeJava(TextAsset txt)
    {
        if (txt == null) return new string[0];
        string[] lines;
        using (var reader = new StringReader(txt.text))
        {
            var list = new List<string>();
            string line;
            while ((line = reader.ReadLine()) != null)
                list.Add(line);
            lines = list.ToArray();
        }
        return lines;
    }

    private int[][] ParseCggCgsLikeJava(TextAsset txt)
    {
        string[] lines = GetLinesLikeJava(txt);
        int[][] cggParsed = new int[lines.Length][];
        for (int i = 0; i < lines.Length; i++)
        {
            string[] temp = lines[i].Split(',');
            cggParsed[i] = ConvertToInt(temp);
        }
        return cggParsed;
    }

    private int[] ConvertToInt(string[] input)
    {
        int[] frame = new int[input.Length];
        for (int i = 0; i < input.Length; i++)
        {
            int val;
            if (!int.TryParse(input[i].Trim(), out val))
                val = 0;
            frame[i] = val;
        }
        return frame;
    }

    private Texture2D ApplyRotationAndFlip(Texture2D src, int rotate, int flip)
    {
        Texture2D tex = src;
        int r = ((rotate % 360) + 360) % 360;

        if (r == 90) tex = Rotate90(tex);
        else if (r == 180) tex = Flip(tex, true, true);
        else if (r == 270)
        {
            tex = Rotate90(tex);
            tex = Rotate90(tex);
            tex = Rotate90(tex);
        }

        bool flipH = (flip == 1 || flip == 3);
        bool flipV = (flip == 2 || flip == 3);
        if (flipH || flipV) tex = Flip(tex, flipH, flipV);

        return tex;
    }

    private void ApplyColorTransform(Color[] pix, float opacity, int blendMode)
    {
        for (int i = 0; i < pix.Length; i++)
        {
            Color c = pix[i];

            float a = c.a * opacity;

            // keep only truly invisible pixels
            if (a <= 0.001f)
            {
                pix[i] = Color.clear;
                continue;
            }

            // IMPORTANT FIX:
            // DO NOT divide by alpha (this causes transparency loss & color blowouts)

            float r = c.r;
            float g = c.g;
            float b = c.b;

            // detect likely premultiplied pixels (very dark but non-zero alpha)
            // and gently correct instead of full division
            float alphaSafe = Mathf.Max(c.a, 0.0001f);
            float correction = 1.0f;

            if (c.a < 1f && (r + g + b) / alphaSafe > 1.2f)
            {
                // mild correction only (prevents darkening artifacts)
                correction = 1.0f / Mathf.Sqrt(alphaSafe);
            }

            r = Mathf.Clamp01(r * correction);
            g = Mathf.Clamp01(g * correction);
            b = Mathf.Clamp01(b * correction);

            // blend mode handling
            if (blendMode == 1)
            {
                // additive-safe
                pix[i] = new Color(r, g, b, a);
                continue;
            }

            pix[i] = new Color(r, g, b, a);
        }
    }


    private void ComputeDimensionsAndLowestPoint(int[][] orderedFrames, int[][] cggRows,
        out int maxX, out int maxY, out int lowestPoint)
    {
        maxX = 65; maxY = 65; lowestPoint = -500;

        for (int i = 0; i < orderedFrames.Length; i++)
        {
            if (orderedFrames[i].Length == 0) continue;

            int currFrame = orderedFrames[i][0];
            if (currFrame < 0 || currFrame >= cggRows.Length) continue;

            var row = cggRows[currFrame];
            if (row.Length < 2) continue;

            int numParts = row[1];
            for (int j = 0; j < numParts; j++)
            {
                int baseIdx = 2 + j * 11;
                if (baseIdx + 9 >= row.Length) continue;

                int currX      = row[baseIdx + 0];
                int currY      = row[baseIdx + 1];
                int currWidth  = row[baseIdx + 8];
                int currHeight = row[baseIdx + 9];

                // Check all four corners of the part (unrotated)
                if (Math.Abs(currX)             > maxX) maxX = Math.Abs(currX);
                if (Math.Abs(currY)             > maxY) maxY = Math.Abs(currY);
                if (Math.Abs(currX + currWidth)  > maxX) maxX = Math.Abs(currX + currWidth);
                if (Math.Abs(currY + currHeight) > maxY) maxY = Math.Abs(currY + currHeight);

                // Also check worst-case rotated extents (90/270 swaps width and height)
                if (Math.Abs(currX + currHeight) > maxX) maxX = Math.Abs(currX + currHeight);
                if (Math.Abs(currY + currWidth)  > maxY) maxY = Math.Abs(currY + currWidth);

                if (currY > lowestPoint) lowestPoint = currY;
            }
        }

        // Round lowestPoint up to next multiple of 10
        int diff = lowestPoint % 10;
        lowestPoint += 10;
        if (diff != 0) lowestPoint += (10 - diff);

        // Round maxX up to next multiple of 10 then double
        diff  = maxX % 10;
        maxX += 10;
        if (diff != 0) maxX += (10 - diff);
        maxX *= 2;

        // Round maxY up to next multiple of 10 then double (was missing the round before doubling)
        diff  = maxY % 10;
        maxY += 10;
        if (diff != 0) maxY += (10 - diff);
        maxY *= 2;
    }

    private Color[] GetSubTexturePixels(Texture2D src, int sx, int sy, int w, int h, out int actualW, out int actualH)
    {
        actualW = Mathf.Clamp(w, 0, src.width);
        actualH = Mathf.Clamp(h, 0, src.height);
        int safeX = Mathf.Clamp(sx, 0, src.width - 1);
        
        int requestedTopY = sy;
        int safeY = src.height - requestedTopY - actualH;
        if (safeY < 0) safeY = 0;

        if (safeX + actualW > src.width) actualW = src.width - safeX;
        if (safeY + actualH > src.height) actualH = src.height - safeY;

        try { return src.GetPixels(safeX, safeY, actualW, actualH); }
        catch { return new Color[actualW * actualH]; }
    }

    private Texture2D CreateTexture(int w, int h)
    {
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.SetPixels32(new Color32[w * h]); // zero-initialized array == fully transparent
        tex.Apply();
        return tex;
    }

    private Texture2D Rotate90(Texture2D src)
    {
        int w = src.width, h = src.height;
        Texture2D dst = CreateTexture(h, w);
        var srcPixels = src.GetPixels();
        var dstPixels = new Color[dst.width * dst.height];

        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
                dstPixels[(x) * dst.width + (h - 1 - y)] = srcPixels[y * w + x];

        dst.SetPixels(dstPixels);
        dst.Apply();
        return dst;
    }

    private Texture2D Flip(Texture2D src, bool horiz, bool vert)
    {
        int w = src.width, h = src.height;
        Texture2D dst = CreateTexture(w, h);
        var srcPixels = src.GetPixels();
        var dstPixels = new Color[w * h];

        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                int sx = horiz ? (w - 1 - x) : x;
                int sy = vert ? (h - 1 - y) : y;
                dstPixels[y * w + x] = srcPixels[sy * w + sx];
            }

        dst.SetPixels(dstPixels);
        dst.Apply();
        return dst;
    }

    private void ApplyOpacity(Texture2D tex, float opacity)
    {
        var pix = tex.GetPixels();
        for (int i = 0; i < pix.Length; i++)
        {
            var c = pix[i];
            c.a *= Mathf.Clamp01(opacity);
            pix[i] = c;
        }
        tex.SetPixels(pix);
        tex.Apply();
    }

    private void ApplyAdditiveBlend(Texture2D tex, float opacity)
    {
        var pix = tex.GetPixels();
        for (int i = 0; i < pix.Length; i++)
        {
            Color c = pix[i];
            int r = Mathf.RoundToInt(c.r * 255);
            int g = Mathf.RoundToInt(c.g * 255);
            int b = Mathf.RoundToInt(c.b * 255);
            int a = Mathf.RoundToInt(c.a * 255);

            if (r + g + b < 50) { pix[i] = Color.clear; continue; }

            if (a > 0)
            {
                double multiplier = 1.0 + (a / 255.0);
                r = Mathf.Min(255, (int)(r * multiplier));
                g = Mathf.Min(255, (int)(g * multiplier));
                b = Mathf.Min(255, (int)(b * multiplier));

                int pixval = (r + g + b) / 3;
                int targetAlpha = (int)(pixval * opacity); // always apply opacity

                pix[i] = new Color(r / 255f, g / 255f, b / 255f, targetAlpha / 255f);
            }
        }
        tex.SetPixels(pix);
        tex.Apply();
    }

    private void PremultiplyAlpha(Texture2D tex)
    {
        var pixels = tex.GetPixels();
        for (int i = 0; i < pixels.Length; i++)
        {
            Color c = pixels[i];
            c.r *= c.a;
            c.g *= c.a;
            c.b *= c.a;
            pixels[i] = c;
        }
        tex.SetPixels(pixels);
        tex.Apply();
    }


    private void BlitOnto(Texture2D dest, Texture2D src, int destX, int destY, int blendMode = 0)
    {
        int sW = src.width, sH = src.height;
        int dW = dest.width, dH = dest.height;

        Color[] srcPix = src.GetPixels();
        Color[] dstPix = dest.GetPixels();

        for (int sy = 0; sy < sH; sy++)
        {
            int dy = destY + sy;
            if (dy < 0 || dy >= dH) continue;

            for (int sx = 0; sx < sW; sx++)
            {
                int dx = destX + sx;
                if (dx < 0 || dx >= dW) continue;

                int sIdx = sy * sW + sx;
                int dIdx = dy * dW + dx;

                Color sC = srcPix[sIdx];
                Color dC = dstPix[dIdx];

                float sa = sC.a;
                if (sa <= 0f) continue;

                if (blendMode == 1)
                {
                    // ADDITIVE (safe version)
                    dstPix[dIdx] = new Color(
                        dC.r + sC.r * sa,
                        dC.g + sC.g * sa,
                        dC.b + sC.b * sa,
                        Mathf.Max(dC.a, sa)
                    );
                }
                else
                {
                    // NORMAL alpha blend (straight alpha Porter-Duff)
                    float da = dC.a;

                    float outA = sa + da * (1f - sa);
                    if (outA < 0.00001f)
                    {
                        dstPix[dIdx] = Color.clear;
                        continue;
                    }

                    dstPix[dIdx] = new Color(
                        (sC.r * sa + dC.r * da * (1f - sa)) / outA,
                        (sC.g * sa + dC.g * da * (1f - sa)) / outA,
                        (sC.b * sa + dC.b * da * (1f - sa)) / outA,
                        outA
                    );
                }
            }
        }

        dest.SetPixels(dstPix);
        dest.Apply();
    }

    private Texture2D UpsampleTexture(Texture2D src, int scale)
    {
        if (scale <= 1) return src;

        int newW = src.width * scale;
        int newH = src.height * scale;

        Texture2D dst = CreateTexture(newW, newH);
        var srcPixels = src.GetPixels();
        var dstPixels = new Color[newW * newH];

        for (int y = 0; y < src.height; y++)
        {
            for (int x = 0; x < src.width; x++)
            {
                Color pixel = srcPixels[y * src.width + x];

                for (int dy = 0; dy < scale; dy++)
                {
                    for (int dx = 0; dx < scale; dx++)
                    {
                        dstPixels[(y * scale + dy) * newW + (x * scale + dx)] = pixel;
                    }
                }
            }
        }

        dst.SetPixels(dstPixels);
        dst.Apply();
        return dst;
    }

    private int CalculateFeetOffset(Texture2D tex)
    {
        Color[] pixels = tex.GetPixels();
        int width = tex.width;
        int height = tex.height;

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                if (pixels[y * width + x].a > 0f)
                    return y; // first row with visible pixels
            }
        }

        return 0; // fully transparent, no offset
    }

    struct AnimationCacheKey : IEquatable<AnimationCacheKey>
    {
        public TextAsset cgg;
        public TextAsset idle;
        public TextAsset move;
        public TextAsset attack;
        public int scale;
        public int pixelsPerUnit;

        public bool Equals(AnimationCacheKey other)
        {
            return cgg == other.cgg &&
                idle == other.idle &&
                move == other.move &&
                attack == other.attack &&
                scale == other.scale &&
                pixelsPerUnit == other.pixelsPerUnit;
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = 17;
                hash = hash * 23 + (cgg ? cgg.GetHashCode() : 0);
                hash = hash * 23 + (idle ? idle.GetHashCode() : 0);
                hash = hash * 23 + (move ? move.GetHashCode() : 0);
                hash = hash * 23 + (attack ? attack.GetHashCode() : 0);
                hash = hash * 23 + scale;
                hash = hash * 23 + pixelsPerUnit;
                return hash;
            }
        }
    }

    static Dictionary<AnimationCacheKey, CachedAnimations> animationCache
    = new Dictionary<AnimationCacheKey, CachedAnimations>();

    class CachedAnimation
    {
        public List<Sprite> sprites;
        public List<float> durations;
    }

    class CachedAnimations
    {
        public CachedAnimation idle;
        public CachedAnimation move;
        public CachedAnimation attack;
    }

}