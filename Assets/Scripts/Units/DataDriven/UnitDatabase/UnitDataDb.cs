// UnitDataDb.cs
// SQLite-backed store for the data-driven UnitData/AbilityData DTOs, replacing
// per-file Addressables JSON loads. Two lazily-opened connections:
//   s_conn     main baseline db, opened READ-ONLY and never written
//              (editor: StreamingAssets/Data/units.db directly, no staleness;
//               desktop: copy-on-version-change into persistentDataPath;
//               Android: UnityWebRequest copy — files inside the APK aren't
//               openable — kicked off at RuntimeInitializeOnLoadMethod, until
//               it lands lookups degrade to null like any missing unit)
//   s_modConn  ':memory:' overlay schema holding the external folder JSONs
//              (UnitDataPaths.ExternalRoot); overlay rows win over base rows
// NOTE: the overlay is a separate connection because a READ-ONLY SQLite
// connection cannot write to an ATTACH'd ':memory:' database.
using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using SQLite;
using UnityEngine;
using UnityEngine.Networking;

public static class UnitDataDb
{
    const string DbFileName = "units.db";
    const string DataDirName = "Data";
    const string MetaDataVersionKey = "dataVersion";
    const string PlayerPrefsVersionKey = "UnitDataDb.DataVersion";

    static SQLiteConnection s_conn;
    static SQLiteConnection s_modConn;
    static bool s_overlayActive;
    static bool s_quitHooked;
    static bool s_openFailed;

#if UNITY_ANDROID && !UNITY_EDITOR
    static bool s_copyStarted, s_copyDone, s_copyOk;
#endif

    /// <summary>True when the main baseline db connection is open.</summary>
    public static bool Available => s_conn != null;

    /// <summary>True when the mod overlay holds at least one row.</summary>
    public static bool ExternalOverridesActive
    {
        get { EnsureOpen(); return s_overlayActive; }
    }

    static void EnsureOpen()
    {
        if (s_conn == null && !s_openFailed) OpenMain();
        if (s_modConn == null) OpenOverlay();
    }

    static void OpenMain()
    {
        string path = ResolveDbPath();
        if (path == null) return; // transient (Android copy in flight) or already latched

        try
        {
            s_conn = new SQLiteConnection(path, SQLiteOpenFlags.ReadOnly | SQLiteOpenFlags.FullMutex);
            s_conn.BusyTimeout = TimeSpan.FromSeconds(1);
            HookQuit();
        }
        catch (Exception e)
        {
            Debug.LogError($"[UnitDataDb] Failed to open '{path}': {e.Message}");
            s_conn = null;
            s_openFailed = true;
        }
    }

    static void OpenOverlay()
    {
        try
        {
            s_modConn = new SQLiteConnection(":memory:",
                SQLiteOpenFlags.ReadWrite | SQLiteOpenFlags.Create | SQLiteOpenFlags.Memory);
            s_modConn.BusyTimeout = TimeSpan.FromSeconds(1);
            s_modConn.CreateTable<UnitDbRow>();
            s_modConn.CreateTable<AbilityDbRow>();
            LoadExternalJsons();
        }
        catch (Exception e)
        {
            Debug.LogError($"[UnitDataDb] Failed to prepare mod overlay: {e.Message}");
            s_modConn = null;
            s_overlayActive = false;
        }
    }

    static string ResolveDbPath()
    {
#if UNITY_EDITOR
        // Editor reads StreamingAssets directly — rebuilds are picked up instantly.
        return Path.Combine(Application.streamingAssetsPath, DataDirName, DbFileName);
#elif UNITY_ANDROID
        // Copy may still be in flight — transient null, retried on next lookup.
        string dest = Path.Combine(Application.persistentDataPath, DbFileName);
        if (!s_copyDone) return null;
        s_openFailed = !s_copyOk; // once done, a failed copy is definitive
        return s_copyOk ? dest : null;
#else
        string dest = Path.Combine(Application.persistentDataPath, DbFileName);
        string source = Path.Combine(Application.streamingAssetsPath, DataDirName, DbFileName);
        EnsureCopy(source, dest);
        if (File.Exists(dest)) return dest;
        s_openFailed = true;
        return null;
#endif
    }

    // ── Copy-on-version-change (players) ──

    static void EnsureCopy(string source, string dest)
    {
        string sourceVersion = ReadMetaVersion(source);
        string localVersion = PlayerPrefs.GetString(PlayerPrefsVersionKey, null);
        if (File.Exists(dest) && sourceVersion != null && sourceVersion == localVersion) return;

        try
        {
            File.Copy(source, dest, overwrite: true);
            PlayerPrefs.SetString(PlayerPrefsVersionKey, sourceVersion);
            PlayerPrefs.Save();
        }
        catch (Exception e)
        {
            Debug.LogError($"[UnitDataDb] Failed to copy '{source}' → '{dest}': {e.Message}");
        }
    }

    static string ReadMetaVersion(string path)
    {
        try
        {
            using (var conn = new SQLiteConnection(path, SQLiteOpenFlags.ReadOnly))
                return conn.Find<MetaRow>(MetaDataVersionKey)?.Value;
        }
        catch (Exception e)
        {
            Debug.LogError($"[UnitDataDb] Cannot read dataVersion from '{path}': {e.Message}");
            return null;
        }
    }

#if UNITY_ANDROID && !UNITY_EDITOR
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void StartAndroidCopy()
    {
        if (s_copyStarted) return;
        s_copyStarted = true;

        var go = new GameObject("UnitDataDb.AndroidCopy");
        UnityEngine.Object.DontDestroyOnLoad(go);
        go.hideFlags = HideFlags.HideAndDontSave;
        go.AddComponent<AndroidCopyDriver>().Run();
    }

    class AndroidCopyDriver : MonoBehaviour
    {
        public void Run() => StartCoroutine(CopyRoutine());

        System.Collections.IEnumerator CopyRoutine()
        {
            string dest = Path.Combine(Application.persistentDataPath, DbFileName);
            string url = $"{Application.streamingAssetsPath}/{DataDirName}/{DbFileName}";

            using (var request = UnityWebRequest.Get(url))
            {
                yield return request.SendWebRequest();

                if (request.result != UnityWebRequest.Result.Success)
                {
                    Debug.LogError($"[UnitDataDb] Failed to read '{url}': {request.error}");
                    s_copyDone = s_copyOk = File.Exists(dest);
                    yield break;
                }

                string tmp = dest + ".tmp";
                File.WriteAllBytes(tmp, request.downloadHandler.data);

                string sourceVersion = ReadMetaVersion(tmp);
                string localVersion = PlayerPrefs.GetString(PlayerPrefsVersionKey, null);
                if (!File.Exists(dest) || sourceVersion == null || sourceVersion != localVersion)
                {
                    if (File.Exists(dest)) File.Delete(dest);
                    File.Move(tmp, dest);
                    PlayerPrefs.SetString(PlayerPrefsVersionKey, sourceVersion);
                    PlayerPrefs.Save();
                }
                else
                {
                    File.Delete(tmp);
                }

                s_copyDone = s_copyOk = true;
            }
        }
    }
#endif

    // ── Mod overlay (external folder JSONs → memory) ──

    static void LoadExternalJsons()
    {
        string root = UnitDataPaths.ExternalRoot;
        if (!Directory.Exists(root)) return;

        var unitRows = new List<UnitDbRow>();
        var abilityRows = new List<AbilityDbRow>();

        foreach (string file in Directory.GetFiles(root, "*.json"))
        {
            string name = Path.GetFileName(file);
            try
            {
                string json = File.ReadAllText(file);
                if (name.StartsWith("unit_", StringComparison.Ordinal))
                {
                    var dto = JsonConvert.DeserializeObject<UnitData>(json, DataDrivenJson.Settings);
                    if (dto == null)
                        Debug.LogError($"[UnitDataDb] External file '{file}' deserialized to null — skipped.");
                    else
                        unitRows.Add(UnitDbRow.FromDto(dto));
                }
                else if (name.StartsWith("ability_", StringComparison.Ordinal))
                {
                    var dto = JsonConvert.DeserializeObject<AbilityData>(json, DataDrivenJson.Settings);
                    if (dto == null)
                        Debug.LogError($"[UnitDataDb] External file '{file}' deserialized to null — skipped.");
                    else
                        abilityRows.Add(AbilityDbRow.FromDto(dto));
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[UnitDataDb] Malformed external file '{file}': {e.Message} — skipped.");
            }
        }

        if (unitRows.Count == 0 && abilityRows.Count == 0) return;

        s_modConn.RunInTransaction(() =>
        {
            foreach (var row in unitRows) s_modConn.InsertOrReplace(row);
            foreach (var row in abilityRows) s_modConn.InsertOrReplace(row);
        });

        s_overlayActive = true;
        Debug.Log($"[UnitDataDb] Mod overlay loaded from '{root}': {unitRows.Count} units, {abilityRows.Count} abilities.");
    }

    // ── Lookups (overlay first, then base) ──

    public static UnitData GetUnitData(string unitId)
    {
        if (string.IsNullOrEmpty(unitId)) return null;
        EnsureOpen();

        UnitDbRow row = QueryOverlayRow<UnitDbRow>(unitId)
                        ?? (s_conn != null ? s_conn.Find<UnitDbRow>(unitId) : null);
        return row?.ToDto();
    }

    public static AbilityData GetAbilityData(string abilityId)
    {
        if (string.IsNullOrEmpty(abilityId)) return null;
        EnsureOpen();

        AbilityDbRow row = QueryOverlayRow<AbilityDbRow>(abilityId)
                           ?? (s_conn != null ? s_conn.Find<AbilityDbRow>(abilityId) : null);
        return row?.ToDto();
    }

    static T QueryOverlayRow<T>(string id) where T : new()
    {
        if (!s_overlayActive) return default;
        return s_modConn.Find<T>(id);
    }

    static void HookQuit()
    {
        if (s_quitHooked) return;
        s_quitHooked = true;
        Application.quitting += Close;
    }

    public static void Close()
    {
        try { s_conn?.Close(); } catch { /* read-only close is best-effort */ }
        try { s_modConn?.Close(); } catch { }
        s_conn = null;
        s_modConn = null;
        s_overlayActive = false;
        s_openFailed = false;
    }
}
