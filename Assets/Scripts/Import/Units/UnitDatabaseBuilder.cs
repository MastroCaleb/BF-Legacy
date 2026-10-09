// UnitDatabaseBuilder.cs
// Tools/Data-Driven Units/5 — builds a single SQLite database at
// Assets/StreamingAssets/Data/units.db straight from the raw unit data
// (default JsonData/Units/*.json + the raw asset folders) via RawUnitParser —
// the same parser the JSON regen tool (menu 2) uses — so the DB no longer
// depends on JsonToUnitData's emitted Addressables JSON files. UnitDataDb opens
// the output read-only at runtime. Idempotent: the output file is recreated
// from scratch every run. Every inserted row is verified with an exhaustive
// DTO round-trip (parsed DTO → row → db → row → DTO) before the build is
// accepted; a single mismatch aborts and removes the output.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SQLite;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;

public static class UnitDatabaseBuilder
{
    const string OutputRelativePath = "Assets/StreamingAssets/Data/units.db";
    const int CurrentSchemaVersion = 1;
    const string MetaSchemaVersionKey = "schemaVersion";
    const string MetaDataVersionKey = "dataVersion";

    [MenuItem("Tools/Data-Driven Units/5. Build SQLite Database")]
    public static void Build()
    {
        var parser = new RawUnitParser();
        if (!parser.TryGetRawUnitFiles(out string[] files)) return;

        parser.ResetState();
        parser.LoadParticleIdIndex();
        parser.LoadTables();

        string absOutput = Path.Combine(Application.dataPath, OutputRelativePath.Substring("Assets/".Length));
        var stopwatch = Stopwatch.StartNew();
        string dataVersion = DateTime.UtcNow.ToString("yyyyMMddHHmmss");
        int unitCount = 0, abilityCount = 0;

        try
        {
            // ── Phase 1: raw data → DTOs (abilities dedupe across units) ──
            var units = new List<UnitData>(files.Length);
            for (int i = 0; i < files.Length; i++)
            {
                if (i % 25 == 0 && EditorUtility.DisplayCancelableProgressBar(
                        "Building units.db",
                        $"Parsing {Path.GetFileName(files[i])} ({i}/{files.Length})",
                        (float)i / files.Length))
                    throw new OperationCanceledException("Build cancelled.");

                units.Add(parser.ParseUnitFile(files[i]));
            }
            units.RemoveAll(u => u == null); // files skipped by the parser (e.g. missing id)

            // ── Phase 2: DTOs → SQLite, every row round-trip verified ──
            Directory.CreateDirectory(Path.GetDirectoryName(absOutput));
            if (File.Exists(absOutput)) File.Delete(absOutput);

            using (var conn = new SQLiteConnection(absOutput, SQLiteOpenFlags.ReadWrite | SQLiteOpenFlags.Create))
            {
                conn.BusyTimeout = TimeSpan.FromSeconds(1);
                conn.CreateTable<UnitDbRow>();
                conn.CreateTable<AbilityDbRow>();
                conn.CreateTable<MetaRow>();

                AbilityData[] abilities = parser.EmittedAbilities.Values
                    .OrderBy(a => a.abilityId, StringComparer.Ordinal).ToArray();
                int total = abilities.Length + units.Count;
                int done = 0;

                conn.RunInTransaction(() =>
                {
                    foreach (AbilityData ability in abilities)
                    {
                        if (++done % 25 == 0 && EditorUtility.DisplayCancelableProgressBar(
                                "Building units.db",
                                $"Inserting ability_{ability.abilityId} ({done}/{total})",
                                (float)done / total))
                            throw new OperationCanceledException("Build cancelled.");

                        InsertAndVerifyAbility(conn, ability);
                    }

                    foreach (UnitData unit in units)
                    {
                        if (++done % 25 == 0 && EditorUtility.DisplayCancelableProgressBar(
                                "Building units.db",
                                $"Inserting unit_{unit.unitId} ({done}/{total})",
                                (float)done / total))
                            throw new OperationCanceledException("Build cancelled.");

                        InsertAndVerifyUnit(conn, unit);
                    }

                    conn.InsertOrReplace(new MetaRow { Key = MetaSchemaVersionKey, Value = CurrentSchemaVersion.ToString() });
                    conn.InsertOrReplace(new MetaRow { Key = MetaDataVersionKey, Value = dataVersion });
                });

                unitCount = conn.ExecuteScalar<int>("SELECT COUNT(*) FROM units");
                abilityCount = conn.ExecuteScalar<int>("SELECT COUNT(*) FROM abilities");
            }
        }
        catch (OperationCanceledException)
        {
            RemoveOutput(absOutput);
            Debug.LogWarning(File.Exists(absOutput)
                ? "[UnitDb] Build cancelled — partial output removed."
                : "[UnitDb] Build cancelled — nothing written.");
            return;
        }
        catch (Exception e)
        {
            RemoveOutput(absOutput);
            Debug.LogError($"[UnitDb] Build failed ({e.Message}) — output removed:\n{e}");
            return;
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }

        stopwatch.Stop();
        AssetDatabase.Refresh();
        long sizeMb = new FileInfo(absOutput).Length / (1024 * 1024);
        Debug.Log(
            $"[UnitDb] Built {unitCount} units + {abilityCount} abilities from raw data " +
            $"({sizeMb} MB, dataVersion {dataVersion}) → {OutputRelativePath} in {stopwatch.Elapsed.TotalSeconds:F1}s. " +
            $"All rows round-trip verified.\n" +
            $"[UnitDb] Missing refs — sprites: {parser.MissingSprites}, particles: {parser.MissingParticles}, " +
            $"audio: {parser.MissingAudio}, animations: {parser.MissingAnimations}");
    }

    static void RemoveOutput(string absOutput)
    {
        try { if (File.Exists(absOutput)) File.Delete(absOutput); } catch { }
    }

    static void InsertAndVerifyUnit(SQLiteConnection conn, UnitData dto)
    {
        if (dto == null) throw new InvalidDataException("unit: parsed to null");
        string source = $"unit_{dto.unitId}";

        UnitDbRow row = UnitDbRow.FromDto(dto);
        conn.Insert(row);

        UnitData roundTripped = conn.Find<UnitDbRow>(row.UnitId)?.ToDto();
        VerifyRoundTrip(source, dto, roundTripped);
    }

    static void InsertAndVerifyAbility(SQLiteConnection conn, AbilityData dto)
    {
        if (dto == null) throw new InvalidDataException("ability: parsed to null");
        string source = $"ability_{dto.abilityId}";

        AbilityDbRow row = AbilityDbRow.FromDto(dto);
        conn.Insert(row);

        AbilityData roundTripped = conn.Find<AbilityDbRow>(row.AbilityId)?.ToDto();
        VerifyRoundTrip(source, dto, roundTripped);
    }

    static void VerifyRoundTrip(string source, object original, object roundTripped)
    {
        if (roundTripped == null) throw new InvalidDataException($"{source}: row missing after insert");
        string before = JsonConvert.SerializeObject(original, UnitDbJson.Compact);
        string after = JsonConvert.SerializeObject(roundTripped, UnitDbJson.Compact);
        if (before == after) return;
        throw new InvalidDataException($"{source}: round-trip mismatch at '{FirstDiffField(before, after)}'");
    }

    static string FirstDiffField(string before, string after)
    {
        try
        {
            var ja = JObject.Parse(before);
            var jb = JObject.Parse(after);
            foreach (var prop in ja.Properties())
                if (!JToken.DeepEquals(prop.Value, jb[prop.Name])) return prop.Name;
            foreach (var prop in jb.Properties())
                if (ja[prop.Name] == null) return prop.Name;
        }
        catch { }
        return "(unresolved diff)";
    }
}
