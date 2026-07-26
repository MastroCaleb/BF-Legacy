using System.Collections.Generic;
using UnityEngine;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

[System.Serializable]
public struct UnitPositioningData
{
    public string charID;
    public Vector3 UNIT_HOME_PARTY_WINDOW_POS;
    public Vector3 UNIT_HOME_PARTY_WINDOW_SCALE;
    public Vector3 UNIT_VIEW_POS;
    public Vector3 UNIT_VIEW_SCALE;
    public Vector3 GACHA_VIEW_POS;
    public Vector3 GACHA_VIEW_SCALE;
    public Vector4 BB_Cutin_Pos;
}

public static class UnitPositioningDataLoader
{
    private static Dictionary<string, UnitPositioningData> _data;

    public static Dictionary<string, UnitPositioningData> Data
    {
        get
        {
            if (_data == null)
                Load();
            return _data;
        }
    }

    public static UnitPositioningData Get(string charID)
    {
        return Data.TryGetValue(charID, out var entry) ? entry : default;
    }

    public static bool TryGet(string charID, out UnitPositioningData entry)
    {
        return Data.TryGetValue(charID, out entry);
    }

    private static void Load()
    {
        _data = new Dictionary<string, UnitPositioningData>();

        TextAsset textAsset = Resources.Load<TextAsset>("UnitPositionExport");
        if (textAsset == null)
        {
            Debug.LogError("UnitPositionExport.json not found in Resources folder.");
            return;
        }

        JArray array = JArray.Parse(textAsset.text);

        foreach (JToken token in array)
        {
            var entry = new UnitPositioningData
            {
                charID = token["charID"].ToString(),
                UNIT_HOME_PARTY_WINDOW_POS = ReadVector3(token["UNIT_HOME_PARTY_WINDOW_POS"]),
                UNIT_HOME_PARTY_WINDOW_SCALE = ReadVector3(token["UNIT_HOME_PARTY_WINDOW_SCALE"]),
                UNIT_VIEW_POS = ReadVector3(token["UNIT_VIEW_POS"]),
                UNIT_VIEW_SCALE = ReadVector3(token["UNIT_VIEW_SCALE"]),
                GACHA_VIEW_POS = ReadVector3(token["GACHA_VIEW_POS"]),
                GACHA_VIEW_SCALE = ReadVector3(token["GACHA_VIEW_SCALE"]),
                BB_Cutin_Pos = ReadVector4(token["BB_Cutin_Pos"])
            };

            _data[entry.charID] = entry;
        }
    }

    private static Vector3 ReadVector3(JToken token)
    {
        if (token == null) return Vector3.zero;
        return new Vector3(
            token["x"].Value<float>(),
            token["y"].Value<float>(),
            token["z"].Value<float>()
        );
    }

    private static Vector4 ReadVector4(JToken token)
    {
        if (token == null) return Vector4.zero;
        return new Vector4(
            token["x"].Value<float>(),
            token["y"].Value<float>(),
            token["z"].Value<float>(),
            token["w"].Value<float>()
        );
    }
}