using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Newtonsoft.Json;

// ---------- OldSAM JSON STRUCTURES ----------
[System.Serializable]
public class OldSamAnimation
{
    public int mAnimRate;
    public int mX, mY;
    public int mWidth, mHeight;
    public int mStartFrameNum, mEndFrameNum;
    public OldSamImage[] mImageVector;
    public OldSamFrame[] mFrames;
    public OldSamLabel[] mLabels;
}

[System.Serializable]
public class OldSamLabel
{
    public string mLabelName;
    public int mStartFrameNum, mEndFrameNum;
}

[System.Serializable]
public class OldSamImage
{
    public string mImageName;
    public int mWidth, mHeight;
    public OldSamTransform mTransform;
}

[System.Serializable]
public class OldSamFrame
{
    public OldSamObject[] mObjectVector;
}

[System.Serializable]
public class OldSamObject
{
    public int mObjectNum;
    public int mResNum;
    public OldSamTransform mTransform;
    public OldSamColor mColor;
}

[System.Serializable]
public class OldSamTransform
{
    public OldSamMatrix mMatrix;
}

[System.Serializable]
public class OldSamMatrix
{
    public float[][] m;
}

[System.Serializable]
public class OldSamColor
{
    public int mRed, mGreen, mBlue, mAlpha;
}

// ---------- CUSTOM GRAPHIC ----------
[RequireComponent(typeof(CanvasRenderer))]
public class OldSamUIQuadGraphic : Graphic
{
    public Vector2 pivotOffset = Vector2.zero;
    public Texture2D texture;
    public Matrix4x4 localMatrix = Matrix4x4.identity;

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        if (texture == null)
            return;

        float w = texture.width;
        float h = texture.height;

        UIVertex vert = UIVertex.simpleVert;
        vert.color = color;

        Vector3[] corners = new Vector3[]
        {
            new Vector3(pivotOffset.x,         -(pivotOffset.y),        0f),
            new Vector3(pivotOffset.x,         -(pivotOffset.y + h),    0f),
            new Vector3(pivotOffset.x + w,     -(pivotOffset.y + h),    0f),
            new Vector3(pivotOffset.x + w,     -(pivotOffset.y),        0f),
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
            vert.position = localMatrix.MultiplyPoint3x4(corners[i]);
            vert.uv0 = uvs[i];
            vh.AddVert(vert);
        }

        vh.AddTriangle(0, 1, 2);
        vh.AddTriangle(2, 3, 0);
    }

    public override Texture mainTexture => texture != null ? texture : s_WhiteTexture;
}


// ---------- OldSAM ANIMATOR ----------
public class OldSamAnimator : MonoBehaviour
{
    public TextAsset jsonFile;
    public bool isEffect = false;
    public string mainPath = "Sams";
    public string effectPath = "Effect_SAMS/sam";
    public string unitPath = "Unit_SAMS/unit_sam";

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

    private OldSamAnimation animData;
    private Dictionary<string, OldSamLabel> animationLabels = new Dictionary<string, OldSamLabel>();
    private Vector2 animationCenter;
    private bool isInitialized = false;

    private List<OldSamUIQuadGraphic> graphicPool = new List<OldSamUIQuadGraphic>();
    private Dictionary<int, Texture2D> texturesByRes = new Dictionary<int, Texture2D>();
    private Dictionary<int, OldSamImage> imagesByRes = new Dictionary<int, OldSamImage>();

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

        InitializeAnimator();
    }

    public void InitializeAnimator()
    {
        if (isInitialized) return;

        isInitialized = true;
        animData = JsonConvert.DeserializeObject<OldSamAnimation>(jsonFile.text);

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

    string GetTexturePath()
    {
        return isEffect ? $"{mainPath}/{effectPath}" : $"{mainPath}/{unitPath}";
    }

    private static Material s_defaultBlendMaterial;
    private static Material GetOrCreateDefaultMaterial()
    {
        if (s_defaultBlendMaterial == null)
        {
            s_defaultBlendMaterial = new Material(Shader.Find("UI/Default"));
            s_defaultBlendMaterial.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            s_defaultBlendMaterial.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        }
        return s_defaultBlendMaterial;
    }

    Texture2D GetCachedTexture(string path)
    {
        return SamTextureProvider.Load(path);
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
            GameObject go = new GameObject($"OldSamGraphic_{i}", typeof(RectTransform));
            go.transform.SetParent(transform, false);
            var graphic = go.AddComponent<OldSamUIQuadGraphic>();

            graphic.material = sharedMat;
            graphic.raycastTarget = false;
            graphic.gameObject.SetActive(false);
            graphicPool.Add(graphic);
        }
    }

    void ComputeAnimationCenter()
    {
        animationCenter = new Vector2(-animData.mX + animData.mWidth * 0.5f, -animData.mY + animData.mHeight * 0.5f);
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
                GameObject go = new GameObject($"OldSamGraphic_{i}", typeof(RectTransform));
                go.transform.SetParent(transform, false);
                var newGraphic = go.AddComponent<OldSamUIQuadGraphic>();
                newGraphic.raycastTarget = false;
                graphicPool.Add(newGraphic);
            }

            var graphic = graphicPool[i];
            graphic.texture = tex;

            // Get the image registration transform (pivot offset baked into mImageVector)
            OldSamImage imageInfo = imagesByRes.TryGetValue(obj.mResNum, out var img) ? img : null;
            float[][] imgMatrix = imageInfo?.mTransform?.mMatrix?.m;
            float[][] objMatrix = obj.mTransform?.mMatrix?.m;

            ApplyTransform(graphic.rectTransform, objMatrix, imgMatrix, graphic, tex);

            if (obj.mColor != null)
            {
                float r = obj.mColor.mRed / 255f;
                float g = obj.mColor.mGreen / 255f;
                float b = obj.mColor.mBlue / 255f;
                float a = obj.mColor.mAlpha / 255f;

                if (a > 0.001f)
                    graphic.color = new Color(r / a, g / a, b / a, a);
                else
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

    // Build a Matrix4x4 from a OldSAM 3-row float[][] (row-major, Y-flipped for Unity)
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

    void ApplyTransform(RectTransform rect, float[][] objMatrix, float[][] imgMatrix, OldSamUIQuadGraphic graphic, Texture2D tex)
    {
        if (objMatrix == null || objMatrix.Length < 2) return;

        float a = objMatrix[0][0], b = objMatrix[0][1], tx = objMatrix[0][2];
        float c = objMatrix[1][0], d = objMatrix[1][1], ty = objMatrix[1][2];


        Matrix4x4 imgMat = (imgMatrix != null && imgMatrix.Length >= 2) ? BuildMatrix(imgMatrix) : Matrix4x4.identity;

        float posX = tx - animationCenter.x;
        float posY = -(ty - animationCenter.y);

        if (!isEffect)
            posY += animData.mHeight * 0.5f;

        rect.anchoredPosition = new Vector2(posX, posY);
        rect.localRotation = Quaternion.identity;
        rect.localScale = Vector3.one;
        
        graphic.pivotOffset = new Vector2(imgMat.m03, imgMat.m13);
        graphic.localMatrix = new Matrix4x4(
            new Vector4( a,  c, 0, 0),
            new Vector4( b,  d, 0, 0),
            new Vector4( 0,  0, 1, 0),
            new Vector4( 0,  0, 0, 1)
        );
        graphic.SetVerticesDirty();
    }

    public void SetAnimation(string animationName, bool shouldLoop = true)
    {
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