using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Newtonsoft.Json;

// ---------- SAM JSON STRUCTURES ----------
[System.Serializable]
public class SamAnimation
{
    public int mAnimRate;
    public int mX, mY;
    public int mWidth, mHeight;
    public int mStartFrameNum, mEndFrameNum;
    public SamImage[] mImageVector;
    public SamFrame[] mFrames;
    public SamLabel[] mLabels;
}

[System.Serializable]
public class SamLabel
{
    public string mLabelName;
    public int mStartFrameNum, mEndFrameNum;
}

[System.Serializable]
public class SamImage
{
    public string mImageName;
    public int mWidth, mHeight;
    public SamTransform mTransform;
}

[System.Serializable]
public class SamFrame
{
    public SamObject[] mObjectVector;
}

[System.Serializable]
public class SamObject
{
    public int mObjectNum;
    public int mResNum;
    public SamTransform mTransform;
    public SamColor mColor;
}

[System.Serializable]
public class SamTransform
{
    public SamMatrix mMatrix;
}

[System.Serializable]
public class SamMatrix
{
    public float[][] m;
}

[System.Serializable]
public class SamColor
{
    public int mRed, mGreen, mBlue, mAlpha;
}

// ---------- CUSTOM GRAPHIC ----------
[RequireComponent(typeof(CanvasRenderer))]
public class SamUIQuadGraphic : Graphic
{
    public Vector2 pivotOffset = Vector2.zero;
    public Texture2D texture;
    public Matrix4x4 localMatrix = Matrix4x4.identity;

protected override void OnPopulateMesh(VertexHelper vh)
{
    vh.Clear();
    if (texture == null) return;

    float w = texture.width;
    float h = texture.height;

    UIVertex vert = UIVertex.simpleVert;
    vert.color = color;

    Vector3[] corners = new Vector3[]
    {
        new Vector3(0, 0, 0),
        new Vector3(0, h, 0),
        new Vector3(w, h, 0),
        new Vector3(w, 0, 0),
    };

    Vector2[] uvs = new Vector2[]
    {
        new Vector2(0f, 1f),
        new Vector2(0f, 0f),
        new Vector2(1f, 0f),
        new Vector2(1f, 1f),
    };

    for (int i = 0; i < 4; i++)
    {
        Vector3 p = localMatrix.MultiplyPoint3x4(corners[i]);
        vert.position = new Vector3(p.x, -p.y, 0f); // Y-flip: SAM Y-down → Unity Y-up
        vert.uv0 = uvs[i];
        vh.AddVert(vert);
    }

    vh.AddTriangle(0, 1, 2);
    vh.AddTriangle(2, 3, 0);
}
    public override Texture mainTexture => texture != null ? texture : s_WhiteTexture;
}


// ---------- SAM ANIMATOR ----------
public class SamAnimator : MonoBehaviour
{
    public TextAsset jsonFile;
    public bool isEffect = false;
    public string mainPath = "Sams";
    public string effectPath = "Effect_SAMS/sam";
    public string unitPath = "Unit_SAMS/unit_sam";

    [Header("Pivot")]
    public Vector2 pivotOffset = new Vector2(0.5f, 0.5f);

    [Header("Animation Settings")]
    public string defaultAnimation = "Idle";
    public bool playOnStart = true;
    public bool loop = true;
    public bool playWhenTimeScaleIsZero = false;
    public bool isCachedInDatabase = false;
    public Material blendMaterial;
    

    [Header("Timing")]
    public float timeOffset = 0f;
    private float startDelayTimer;
    private bool waitingToStart;

    private SamAnimation animData;
    private Dictionary<string, SamLabel> animationLabels = new Dictionary<string, SamLabel>();
    private Vector2 animationCenter;
    private bool isInitialized = false;

    public List<SamUIQuadGraphic> graphicPool = new List<SamUIQuadGraphic>();
    private Dictionary<int, Texture2D> texturesByRes = new Dictionary<int, Texture2D>();
    private Dictionary<int, SamImage> imagesByRes = new Dictionary<int, SamImage>();

    private float frameTime;
    private float timer;
    private int frameStart, frameEnd;
    private int currentFrame;
    private string currentAnimation;
    private bool isPlaying;

    void Awake()
    {
        if (!jsonFile)
        {
            enabled = false;
            return;
        }

        if (isCachedInDatabase)
            InitializeAnimatorFromDatabase();
        else
            InitializeAnimator();
    }

    public void InitializeAnimatorFromDatabase()
    {
        if (SamJsonDatabase.Instance == null)
        {
            Debug.LogError("No SamJsonDatabase found in scene!");
            enabled = false;
            return;
        }

        animData = SamJsonDatabase.Instance.GetAnimation(jsonFile);
        animationLabels = SamJsonDatabase.Instance.GetLabels(jsonFile);

        var rt = transform as RectTransform;
        if (rt != null && animData != null)
        {
            rt.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, animData.mWidth);
            rt.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, animData.mHeight);
        }

        if (animData == null || animationLabels == null)
        {
            Debug.LogError("Failed to load SAM animation from cache!");
            enabled = false;
            return;
        }

        frameTime = 1f / Mathf.Max(1, animData.mAnimRate);

        LoadGraphics();
        ComputeAnimationCenter();

        if (!playOnStart) return;

        SetAnimation(defaultAnimation, loop);
        isPlaying = true;
    }

    public void InitializeAnimator()
    {
        if (isInitialized) return;

        isInitialized = true;
        animData = JsonConvert.DeserializeObject<SamAnimation>(jsonFile.text);

        var rt = transform as RectTransform;
        if (rt != null && animData != null)
        {
            rt.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, animData.mWidth);
            rt.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, animData.mHeight);
        }

        animationLabels.Clear();
        if (animData.mLabels != null)
        {
            foreach (var label in animData.mLabels)
            {
                if (!string.IsNullOrEmpty(label.mLabelName))
                    animationLabels[label.mLabelName] = label;
            }
        }

        frameTime = 1f / Mathf.Max(1, animData.mAnimRate);

        LoadGraphics();
        ComputeAnimationCenter();

        if (playOnStart)
            SetAnimation(defaultAnimation, loop);
    }

    public void Reinitialize()
    {
        // Clear old pool
        foreach (var g in graphicPool)
        {
            if (g != null)
                Destroy(g.gameObject);
        }
        graphicPool.Clear();
        texturesByRes.Clear();
        imagesByRes.Clear();
        animationLabels.Clear();

        // Reset flags so InitializeAnimator runs fresh
        isInitialized = false;
        isPlaying = false;
        waitingToStart = false;

        if(jsonFile != null) InitializeAnimator();
    }

    string GetTexturePath()
    {
        return isEffect ? $"{mainPath}/{effectPath}" : $"{mainPath}/{unitPath}";
    }

    void LoadGraphics()
    {
        texturesByRes.Clear();
        imagesByRes.Clear();

        string folderName = System.IO.Path.GetFileNameWithoutExtension(jsonFile.name).Replace("_anime", "");

        for (int i = 0; i < animData.mImageVector.Length; i++)
        {
            var img = animData.mImageVector[i];
            string nameWithoutExt = System.IO.Path.GetFileNameWithoutExtension(img.mImageName);
            Texture2D tex = GetCachedTexture($"{GetTexturePath()}/{folderName}/{nameWithoutExt}");

            if (!tex)
            {
                Debug.LogError($"Missing texture: {GetTexturePath()}/{folderName}/{nameWithoutExt}");
                continue;
            }

            texturesByRes[i] = tex;
            imagesByRes[i] = img;
        }

        int maxObjectsPerFrame = 0;
        foreach (var frame in animData.mFrames)
        {
            if (frame?.mObjectVector != null)
                maxObjectsPerFrame = Mathf.Max(maxObjectsPerFrame, frame.mObjectVector.Length);
        }

        Material sharedMat = blendMaterial != null ? blendMaterial : GetOrCreateDefaultMaterial();

        for (int i = 0; i < maxObjectsPerFrame + 20; i++)
        {
            GameObject go = new GameObject($"SamGraphic_{i}", typeof(RectTransform));
            go.transform.SetParent(transform, false);
            var graphic = go.AddComponent<SamUIQuadGraphic>();

            graphic.material = sharedMat;
            graphic.raycastTarget = false;
            graphic.gameObject.SetActive(false);
            graphicPool.Add(graphic);
        }
    }

    private static Material s_defaultBlendMaterial;
    private static Material GetOrCreateDefaultMaterial()
    {
        if (s_defaultBlendMaterial == null)
        {
            s_defaultBlendMaterial = new Material(Shader.Find("UI/Default"));
            s_defaultBlendMaterial.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            s_defaultBlendMaterial.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            s_defaultBlendMaterial.DisableKeyword("UNITY_UI_PREMULTIPLIED_ALPHA");
        }
        return s_defaultBlendMaterial;
    }

    Texture2D GetCachedTexture(string path)
    {
        return SamTextureProvider.Load(path);
    }

    void ComputeAnimationCenter()
    {
        animationCenter = new Vector2(-animData.mX + animData.mWidth * 0.5f, -animData.mY + animData.mHeight * 0.5f);

        if (!isEffect)
        {
            var rt = transform as RectTransform;
            if (rt != null)
                rt.pivot = new Vector2(0.5f, 0f);
        }
    }

    void Update()
    {
        float delta = playWhenTimeScaleIsZero ? Time.unscaledDeltaTime : Time.deltaTime;
        delta = Mathf.Min(delta, 0.1f);

        if (waitingToStart)
        {
            startDelayTimer -= delta;
            if (startDelayTimer <= 0f)
            {
                waitingToStart = false;
                isPlaying = true;
                currentFrame = frameStart;
                timer = 0f;

                foreach (var g in graphicPool)
                    g.gameObject.SetActive(true);

                DisplayFrame(currentFrame);
            }
            return;
        }

        if (!isPlaying) return;

        timer += delta;

        if (timer >= frameTime)
        {
            timer -= frameTime;
            currentFrame++;

            if (currentFrame > frameEnd)
            {
                if (loop)
                    currentFrame = frameStart;
                else
                {
                    currentFrame = frameEnd;
                    isPlaying = false;
                }
            }

            DisplayFrame(currentFrame);
        }
    }

    void DisplayFrame(int frameIndex)
    {
        if (animData.mFrames == null || frameIndex < 0 || frameIndex >= animData.mFrames.Length)
            return;

        var frame = animData.mFrames[frameIndex];
        if (frame?.mObjectVector == null) return;

        foreach (var g in graphicPool)
            g.gameObject.SetActive(false);

        for (int i = 0; i < frame.mObjectVector.Length; i++)
        {
            var obj = frame.mObjectVector[i];

            if (!texturesByRes.TryGetValue(obj.mResNum, out var tex))
                continue;

            if (i >= graphicPool.Count)
            {
                GameObject go = new GameObject($"SamGraphic_{i}", typeof(RectTransform));
                go.transform.SetParent(transform, false);
                var newGraphic = go.AddComponent<SamUIQuadGraphic>();
                newGraphic.raycastTarget = false;
                newGraphic.material = blendMaterial != null ? blendMaterial : graphicPool[0].material;
                newGraphic.localMatrix = Matrix4x4.identity;
                graphicPool.Add(newGraphic);
            }

            var graphic = graphicPool[i];
            graphic.texture = tex;

            // Get the image registration transform (pivot offset baked into mImageVector)
            SamImage imageInfo = imagesByRes.TryGetValue(obj.mResNum, out var img) ? img : null;
            float[][] imgMatrix = imageInfo?.mTransform?.mMatrix?.m;
            float[][] objMatrix = obj.mTransform?.mMatrix?.m;

            ApplyTransform(graphic.rectTransform, objMatrix, imgMatrix, graphic, tex);

            if (obj.mColor != null)
            {
                float r = obj.mColor.mRed   / 255f;
                float g = obj.mColor.mGreen / 255f;
                float b = obj.mColor.mBlue  / 255f;
                float a = obj.mColor.mAlpha / 255f;

                // SAM stores straight (non-premultiplied) RGBA — use as-is.
                // The old divide-by-alpha was incorrect and caused blown-out colors.
                graphic.color = new Color(r, g, b, a);
            }
            else
            {
                graphic.color = Color.white;
            }

            graphic.SetVerticesDirty();
            graphic.gameObject.SetActive(true);
            graphic.transform.SetSiblingIndex(i);
        }
    }

    // Build a Matrix4x4 from a SAM 3-row float[][] (row-major, Y-flipped for Unity)
    Matrix4x4 BuildMatrix(float[][] m)
    {
        if (m == null || m.Length < 2) return Matrix4x4.identity;

        float a  = m[0][0], b  = m[0][1], tx = m[0][2];
        float c  = m[1][0], d  = m[1][1], ty = m[1][2];

        Matrix4x4 mat = Matrix4x4.identity;
        mat.SetColumn(0, new Vector4(a, c, 0f, 0f));
        mat.SetColumn(1, new Vector4(b, d, 0f, 0f));
        mat.SetColumn(2, new Vector4(0f, 0f, 1f, 0f));
        mat.SetColumn(3, new Vector4(tx, ty, 0f, 1f));

        return mat;
    }

    void ApplyTransform(RectTransform rect, float[][] objMatrix, float[][] imgMatrix,
                    SamUIQuadGraphic graphic, Texture2D tex)
    {
        if (objMatrix == null || objMatrix.Length < 2) return;

        float oa = objMatrix[0][0], ob = objMatrix[0][1], otx = objMatrix[0][2];
        float oc = objMatrix[1][0], od = objMatrix[1][1], oty = objMatrix[1][2];

        float ia = 1f, ib = 0f, itx = 0f;
        float ic = 0f, id = 1f,  ity = 0f;
        if (imgMatrix != null && imgMatrix.Length >= 2)
        {
            ia = imgMatrix[0][0]; ib = imgMatrix[0][1]; itx = imgMatrix[0][2];
            ic = imgMatrix[1][0]; id = imgMatrix[1][1]; ity = imgMatrix[1][2];
        }

        float tx = oa*itx + ob*ity + otx;
        float ty = oc*itx + od*ity + oty;

        rect.anchoredPosition = Vector2.zero;
        rect.localRotation = Quaternion.identity;
        rect.localScale = Vector3.one;

        graphic.pivotOffset = Vector2.zero;

        Vector2 rightObj = new Vector2(oa, oc);
        Vector2 upObj    = new Vector2(ob, od);

        Vector2 rightImg = new Vector2(ia, ic);
        Vector2 upImg    = new Vector2(ib, id);

        Vector2 right = new Vector2(
            rightObj.x * rightImg.x + upObj.x * rightImg.y,
            rightObj.y * rightImg.x + upObj.y * rightImg.y
        );

        Vector2 up = new Vector2(
            rightObj.x * upImg.x + upObj.x * upImg.y,
            rightObj.y * upImg.x + upObj.y * upImg.y
        );

        graphic.localMatrix = new Matrix4x4(
            new Vector4(right.x, right.y, 0, 0),
            new Vector4(up.x,    up.y,    0, 0),
            new Vector4(0,       0,       1, 0),
            new Vector4(
                tx - animationCenter.x + (isEffect ? 0 : pivotOffset.x),
                ty - animationCenter.y + (isEffect ? 0 : pivotOffset.y),
                0, 1)
        );
        graphic.SetVerticesDirty();
    }

    public void SetAnimation(string animationName, bool shouldLoop = true)
    {
        InitializeAnimator();
        
        if (!animationLabels.TryGetValue(animationName, out var label))
        {
            Debug.Log($"Animation '{animationName}' not found! Fallback to Idle");
            animationName = "Idle";
            label = animationLabels[animationName];
        }

        frameStart = label.mStartFrameNum;
        frameEnd = label.mEndFrameNum;
        loop = shouldLoop;

        currentFrame = frameStart;
        timer = 0f;

        startDelayTimer = Mathf.Max(0f, timeOffset);
        waitingToStart = startDelayTimer > 0f;

        currentAnimation = animationName;
        isPlaying = !waitingToStart;

        if (waitingToStart)
        {
            foreach (var g in graphicPool)
                g.gameObject.SetActive(false);
        }
        else
        {
            DisplayFrame(currentFrame);
        }
    }

    public float GetTotalDurationFrames(string animName)
    {
        if (!animationLabels.TryGetValue(animName, out var label))
        {
            // Same fallback behavior as SetAnimation
            if (!animationLabels.TryGetValue("Idle", out label))
            {
                Debug.LogWarning($"Animation '{animName}' not found, and Idle animation is also missing.");
                return 0;
            }
        }

        int totalFrames = label.mEndFrameNum - label.mStartFrameNum + 1;

        return totalFrames / (float)Mathf.Max(1, animData.mAnimRate);
    }

    public bool IsPlaying() => isPlaying;
    public string GetCurrentAnimation() => currentAnimation;
    public void Play() => isPlaying = true;
    public void Stop() => isPlaying = false;
    public void Pause() => isPlaying = false;
    public void Replay()
    {
        currentFrame = frameStart;
        timer = 0f;
        isPlaying = true;
        DisplayFrame(currentFrame);
    }
}