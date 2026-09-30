using System;
using System.IO;
using System.IO.Compression;
using System.Text;
using Newtonsoft.Json;
using UnityEngine;

// Data structure representing the friend's profile
[System.Serializable]
public class FriendData
{
    public string playerId;
    public string playerName;
    public int playerLevel;
    public string unitId;
    public int level;
    public int bbLevel;
    public int sbbLevel;
    public string itemId;
}

public static class FriendCodeManager
{
    public static string ExportToFriendCode(FriendData data)
    {
        try
        {
            string rawJson = JsonConvert.SerializeObject(data);
            byte[] bytes = Encoding.UTF8.GetBytes(rawJson);
            
            using (var outputStream = new MemoryStream())
            {
                using (var gZipStream = new GZipStream(outputStream, CompressionMode.Compress))
                {
                    gZipStream.Write(bytes, 0, bytes.Length);
                }
                return Convert.ToBase64String(outputStream.ToArray());
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"Error exporting FriendData to code: {e.Message}");
            return string.Empty;
        }
    }

    public static FriendData ImportFromFriendCode(string friendCode)
    {
        try
        {
            byte[] buffer = Convert.FromBase64String(friendCode);
            using (var inputStream = new MemoryStream(buffer))
            {
                using (var gZipStream = new GZipStream(inputStream, CompressionMode.Decompress))
                {
                    using (var outputStream = new MemoryStream())
                    {
                        gZipStream.CopyTo(outputStream);
                        string rawJson = Encoding.UTF8.GetString(outputStream.ToArray());
                        
                        return JsonConvert.DeserializeObject<FriendData>(rawJson);
                    }
                }
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"Pasted friend code is invalid or corrupted: {e.Message}");
            return null;
        }
    }
}
