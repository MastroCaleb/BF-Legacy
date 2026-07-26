using System;

using System.IO;
using System.IO.Compression;
using System.Xml;
using UnityEngine;
using System.Globalization;

public class PlistConverterBatch : MonoBehaviour
{
    [Header("Directory containing .plist files")]
    public string plistDirectory = "";

    [Header("Output Root Folder")]
    public string outputRoot = "ConvertedParticles";

    private Type plistType = typeof(PListParticle);

    void Start()
    {
        if (!Directory.Exists(plistDirectory))
        {
            Debug.LogError("Plist directory does not exist: " + plistDirectory);
            return;
        }

        ConvertAll();
    }

    // -------------------------------------------------------------
    // MAIN LOOP
    // -------------------------------------------------------------
    void ConvertAll()
    {
        Directory.CreateDirectory(outputRoot);

        string[] plistFiles = Directory.GetFiles(plistDirectory, "*.plist", SearchOption.TopDirectoryOnly);

        if (plistFiles.Length == 0)
        {
            Debug.LogWarning("No .plist files found.");
            return;
        }

        foreach (string plistPath in plistFiles)
        {
            try
            {
                ConvertSinglePlist(plistPath);
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error converting {Path.GetFileName(plistPath)}:\n{ex}");
            }
        }

        Debug.Log("All plist files processed.");
    }

    // -------------------------------------------------------------
    // PER FILE
    // -------------------------------------------------------------
    void ConvertSinglePlist(string plistPath)
    {
        string name = Path.GetFileNameWithoutExtension(plistPath);
        string outFolder = Path.Combine(outputRoot, name);

        Directory.CreateDirectory(outFolder);

        XmlDocument xml = new XmlDocument();
        xml.Load(plistPath);

        XmlNode dictNode = xml.GetElementsByTagName("dict")[0];

        PListParticle p = new PListParticle();

        // parse dict entries
        for (int i = 0; i < dictNode.ChildNodes.Count; i += 2)
        {
            XmlNode keyNode = dictNode.ChildNodes[i];
            XmlNode valNode = dictNode.ChildNodes[i + 1];

            string key = keyNode.InnerText;

            AssignValueSafe(p, key, valNode);
        }

        // ----------------------------------
        // WRITE JSON
        // ----------------------------------
        string jsonOut = JsonUtility.ToJson(p, true);
        File.WriteAllText(Path.Combine(outFolder, name + ".json"), jsonOut);

        // ----------------------------------
        // EXTRACT PNG
        // ----------------------------------
        if (!string.IsNullOrEmpty(p.textureImageData))
        {
            string imageOut = Path.Combine(outFolder, p.textureFileName);
            SaveImageSafe(p.textureImageData, imageOut);
        }
    }

    // -------------------------------------------------------------
    // SAFE FIELD ASSIGNMENT
    // -------------------------------------------------------------
    void AssignValueSafe(PListParticle p, string key, XmlNode valueNode)
    {
        // does this field exist in the class?
        var field = plistType.GetField(key);
        if (field == null)
        {
            // not a recognized field → ignore
            return;
        }

        string text = valueNode.InnerText;

        try
        {
            switch (valueNode.Name)
            {
                case "integer":
                case "real":
                {
                    double valDouble = double.Parse(text, CultureInfo.InvariantCulture);
                    float val = (float)valDouble;
                    field.SetValue(p, val);
                    break;
                }
                case "string":
                    field.SetValue(p, text);
                    break;

                case "true":
                    field.SetValue(p, 1f);
                    break;

                case "false":
                    field.SetValue(p, 0f);
                    break;

                case "data":
                    field.SetValue(p, text);
                    break;
            }
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"Failed to assign key '{key}' with value '{text}': {ex.Message}");
        }
    }

    // -------------------------------------------------------------
    // IMAGE EXTRACTION (GZIP SAFE)
    // -------------------------------------------------------------
    void SaveImageSafe(string base64, string outPath)
    {
        byte[] bytes;

        try
        {
            bytes = Convert.FromBase64String(base64);
        }
        catch
        {
            Debug.LogWarning($"Invalid base64 image: {outPath}");
            return;
        }

        // Try decompress if gzip
        try
        {
            using var input = new MemoryStream(bytes);
            using var gzip = new GZipStream(input, CompressionMode.Decompress);
            using var output = new MemoryStream();

            gzip.CopyTo(output);
            bytes = output.ToArray();
        }
        catch
        {
            // not gzip → raw png → fine
        }

        File.WriteAllBytes(outPath, bytes);
        Debug.Log("Extracted texture: " + outPath);
    }
}
[Serializable]
public class PListParticle
{
    public float angle;
    public float angleVariance;
    public float blendFuncDestination;
    public float blendFuncSource;
    public float duration;
    public float emitterType;
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
    public float rotatePerSecond;
    public float rotatePerSecondVariance;
    public float rotationEnd;
    public float rotationEndVariance;
    public float rotationStart;
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
    public string textureImageData;
}