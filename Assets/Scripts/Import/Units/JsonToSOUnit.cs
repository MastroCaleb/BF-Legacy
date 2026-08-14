// JsonToSOUnit.cs
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;

using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

#if UNITY_EDITOR
public class JsonToSOUnit : EditorWindow
{
    [MenuItem("Tools/Import BF Units")]
    public static void ShowWindow() => GetWindow<JsonToSOUnit>("BF Unit Importer");

    private const int    MAX_BATCH_PER_RUN = 500;
    private const int    BATCH_SIZE        = 20;
    private const string PROGRESS_FILE     = "Assets/Temp/UnitImportProgress.txt";

    private string jsonFolderPath    = "JsonData/Units";
    private string cggFolderPath     = "BF_Assets/content/unit/cgg";
    private string cgsFolderPath     = "BF_Assets/content/unit/cgs";
    private string spriteFolderPath  = "BF_Assets/content/unit/img";
    private string samRootFolderPath = "Resources/Sams/Unit_SAMS/unit_sam/";

    [Header("Audio")]
    public string soundFolderPath  = "BF_Assets/content/sound"; // relative to Assets/
    public string effectGroupMst1  = "Resources/F_EFFECT_GROUP_MST_1";
    public string effectGroupMst2  = "Resources/F_EFFECT_GROUP_MST_2";
    public string skillMstFolder   = "Resources/SkillMSTs"; // folder containing 9 SKILL_MST files

    private readonly Dictionary<string, List<string>> _effectGroupSounds = new(); // groupId → mp3 names
    private readonly Dictionary<string, JObject>      _skillMstById      = new(); // bbId → skill entry

    private readonly Dictionary<string, TextAsset> textCache    = new();
    private readonly Dictionary<string, Texture2D> textureCache = new();
    private readonly Dictionary<string, Sprite>    spriteCache  = new();

    [Header("Particle Effects")]
    public string particleEffectFolder = "Resources/Particles";
    private readonly Dictionary<string, ParticleEffect> _particleEffectsById = new();

    private struct GroupParticleEntry
    {
        public int subFrame;
        public List<string> particleIds;   // was: string particleId — split on '@' for layered effects
        public int placementType;
        public int xAnchor;
        public int yAnchor;
        public float offsetX;
        public float offsetY;
    }
    private struct GroupSoundEntry { public int subFrame; public string clipName; }

    private readonly Dictionary<string, List<GroupParticleEntry>> _effectGroupParticles   = new();
    private readonly Dictionary<string, List<GroupSoundEntry>>    _effectGroupSoundFrames = new();

    private const string MISSING_ANIMATION_FILE = "Assets/Temp/MissingUnitAnimations.txt";

    private void LogMissingAnimation(string unitId, string unitName)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(MISSING_ANIMATION_FILE)!);

        File.AppendAllText(
            MISSING_ANIMATION_FILE,
            $"{unitId}\t{unitName}{Environment.NewLine}"
        );
    }

    private void OnGUI()
    {
        GUILayout.Label("Import Brave Frontier Units", EditorStyles.boldLabel);
        jsonFolderPath    = EditorGUILayout.TextField("JSON Folder",     jsonFolderPath);
        cggFolderPath     = EditorGUILayout.TextField("CGG Folder",      cggFolderPath);
        cgsFolderPath     = EditorGUILayout.TextField("CGS Folder",      cgsFolderPath);
        spriteFolderPath  = EditorGUILayout.TextField("Sprite Folder",   spriteFolderPath);
        samRootFolderPath = EditorGUILayout.TextField("SAM Root Folder", samRootFolderPath);

        if (GUILayout.Button("Import Units"))   ImportUnits();
        if (GUILayout.Button("Reset Progress") && File.Exists(PROGRESS_FILE))
            File.Delete(PROGRESS_FILE);
    }

    // ─────────────────────────────────────────────────────────────
    //  IMPORT LOOP
    // ─────────────────────────────────────────────────────────────

    private void ImportUnits()
    {
        _effectGroupSounds.Clear();
        _skillMstById.Clear();
        audioCache.Clear();

        LoadParticleEffectDatabase();
        LoadEffectGroupMst(effectGroupMst1);
        LoadEffectGroupMst(effectGroupMst2);
        LoadSkillMstFolder(skillMstFolder);

        string absPath = Application.dataPath + "/" + jsonFolderPath;
        if (!Directory.Exists(absPath))
        {
            Debug.LogError($"JSON folder not found: {absPath}");
            return;
        }

        string[] files    = Directory.GetFiles(absPath, "*.json");
        int      startIdx = LoadProgress();
        int      endIdx   = Mathf.Min(startIdx + MAX_BATCH_PER_RUN, files.Length);

        Debug.Log($"Importing {startIdx} → {endIdx - 1} of {files.Length}");

        AssetDatabase.StartAssetEditing();
        try
        {
            for (int i = startIdx; i < endIdx; i++)
            {
                JObject data = JObject.Parse(File.ReadAllText(files[i]));
                CreateUnitSO(data);
                if ((i - startIdx) % BATCH_SIZE == 0) Flush();
            }
        }
        finally
        {
            AssetDatabase.StopAssetEditing();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Flush();
        }

        SaveProgress(endIdx);

        if (endIdx >= files.Length)
        {
            if (File.Exists(PROGRESS_FILE)) File.Delete(PROGRESS_FILE);
            Debug.Log("All units imported!");
        }
        else
        {
            Debug.Log($"Batch done. Run again to continue from {endIdx}.");
        }
    }

    private int  LoadProgress() =>
        File.Exists(PROGRESS_FILE) && int.TryParse(File.ReadAllText(PROGRESS_FILE), out int n) ? n : 0;

    private void SaveProgress(int index)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(PROGRESS_FILE)!);
        File.WriteAllText(PROGRESS_FILE, index.ToString());
    }

    private void Flush()
    {
        EditorUtility.UnloadUnusedAssetsImmediate();
        GC.Collect();
    }

    private void LoadParticleEffectDatabase()
    {
        _particleEffectsById.Clear();
        string[] guids = AssetDatabase.FindAssets("t:ParticleEffect", new[] { $"Assets/{particleEffectFolder}" });
        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            ParticleEffect pe = AssetDatabase.LoadAssetAtPath<ParticleEffect>(path);
            if (pe == null || string.IsNullOrEmpty(pe.effectId)) continue;
            if (!_particleEffectsById.ContainsKey(pe.effectId))
                _particleEffectsById[pe.effectId] = pe;
        }
        Debug.Log($"[ParticleEffect] Indexed {_particleEffectsById.Count} particle effect SOs from {particleEffectFolder}");
    }

    // ─────────────────────────────────────────────────────────────
    //  UNIT
    // ─────────────────────────────────────────────────────────────

    private void CreateUnitSO(JObject data)
    {
        string unitId = data["id"]?.ToString();
        if (string.IsNullOrEmpty(unitId)) { Debug.LogWarning("Skipped: missing id"); return; }

        string unitFolder    = $"Assets/Resources/Units/unit_{unitId}";
        string abilityFolder = $"{unitFolder}/Abilities";
        EnsureFolder(unitFolder);
        EnsureFolder(abilityFolder);

        Unit unit = ScriptableObject.CreateInstance<Unit>();

        // ── Core Info ────────────────────────────────────────────
        unit.unitId      = unitId;
        unit.unitName    = data["unitName"]?.ToString();
        unit.unitNumber  = data["no"]?.ToString();
        unit.description = data["description"]?.ToString();
        unit.summonDesc  = data["summon"]?.ToString();
        unit.fusionDesc  = data["fusion"]?.ToString();
        unit.evoDesc     = data["evolution"]?.ToString();
        unit.summonCost  = ParseInt(data["cost"]);
        unit.element     = ParseElement(data["element"]?.ToString());
        unit.rarity      = ParseRarity(data["rarity"]?.ToString());
        unit.bbType      = data["bbtype"]?.ToString();

        // ── Statistics ───────────────────────────────────────────
        unit.maxLevel  = ParseInt(data["maxlv"]);
        unit.aiLevel   = ParseInt(data["ai"]);
        unit.maxHealth = ParseInt(data["hp_base"]);
        unit.atk       = ParseInt(data["atk_base"]);
        unit.def       = ParseInt(data["def_base"]);
        unit.rec       = ParseInt(data["rec_base"]);
        unit.baseExp   = ParseInt(data["basexp"]);

        // ── Movement ─────────────────────────────────────────────
        unit.moveSpeedAttack = ParseFloat(data["movespeed_attack"]);
        unit.moveSpeedSkill  = ParseFloat(data["movespeed_skill"]);
        unit.speedTypeAttack = ParseInt(data["speedtype_attack"]);
        unit.speedTypeSkill  = ParseInt(data["speedtype_skill"]);
        unit.moveTypeAttack  = ParseInt(data["movetype_attack"]);
        unit.moveTypeSkill   = ParseInt(data["movetype_skill"]);

        // ── Evolution ────────────────────────────────────────────
        unit.evoFrom    = data["evofrom"]?.ToString();
        unit.evoInto    = data["evointo"]?.ToString();
        unit.evoItem    = data["evoitem"]?.ToString();
        unit.evoZelCost = ParseInt(data["evozelcost"]);
        unit.evoMats    = new List<string>();
        for (int m = 1; m <= 5; m++)
        {
            string mat = data[$"evomats{m}"]?.ToString();
            if (!string.IsNullOrEmpty(mat))
                unit.evoMats.Add(mat);
        }

        // ── Sell ─────────────────────────────────────────────────
        unit.sellPrice   = ParseInt(data["sellPrice"]);
        unit.sellCaution = data["sellCaution"]?.Type == JTokenType.Boolean
            ? data["sellCaution"].Value<bool>()
            : ParseInt(data["sellCaution"]) != 0;

        // ── Type Statistics ───────────────────────────────────────────
        JObject stats = data["stats"] as JObject;
        if (stats != null)
        {
            unit.statsBase     = ParseUnitStats(stats["_base"]    as JObject);
            unit.statsLord     = ParseUnitStats(stats["_lord"]    as JObject);
            unit.statsAnima    = ParseUnitStats(stats["anima"]    as JObject);
            unit.statsBreaker  = ParseUnitStats(stats["breaker"]  as JObject);
            unit.statsGuardian = ParseUnitStats(stats["guardian"] as JObject);
            unit.statsOracle   = ParseUnitStats(stats["oracle"]   as JObject);
        }

        // ── Display Positions ─────────────────────────────────────
        unit.unitDisplayHomePosition         = ParseUnitDisplay(data["unitDisplayHomePosition_1W9CxaFK"]?.ToString());
        unit.unitDisplayDetailPosition       = ParseUnitDisplay(data["unitDisplayDetailPosition_6z54rgb3"]?.ToString());
        unit.unitDisplayConfirmImagePosition = ParseUnitDisplay(data["unitDisplayConfirmImagePosition_MYK1fq6c"]?.ToString());
        unit.unitDisplayCutInImagePosition   = ParseUnitDisplay(data["unitDisplayCutInImagePosition_7hLR6pDN"]?.ToString());
        unit.unitDisplaySummonPosition       = ParseUnitDisplay(data["unitDisplaySummonPosition_KC3Jk8Br"]?.ToString());
        unit.unitDisplayHpPosition           = ParseVector2(data["unitDisplayHpPosition_3BpHN6VD"]?.ToString());
        unit.unitDisplayCursorPosition       = ParseVector2(data["unitDisplayCursorPosition"]?.ToString());

        // ── Visuals & Animation ───────────────────────────────────
        AssignAnimationData(unit, unitId);

        // ── Abilities ────────────────────────────────────────────
        unit.basicAbility  = CreateNormalAbility(unit.unitName, data, abilityFolder);
        unit.bbAbility     = CreateLeveledAbility(data["bb"]  as JObject, data, "bbmultiplier",  "bbdc",  abilityFolder);
        unit.sbbAbility    = CreateLeveledAbility(data["sbb"] as JObject, data, "sbbmultiplier", "sbbdc", abilityFolder);
        unit.ubbAbility    = CreateLeveledAbility(data["ubb"] as JObject, data, "ubbmultiplier", "ubbdc", abilityFolder);
        unit.leaderAbility = CreateFlatAbility(data["leader_skill"] as JObject, abilityFolder);
        unit.extraAbility  = CreateFlatAbility(data["extra_skill"]  as JObject, abilityFolder);

        AssetDatabase.CreateAsset(unit, $"{unitFolder}/unit_{unitId}.asset");
        EditorUtility.SetDirty(unit);
    }

    // ── NEW: Display Position Parsers ─────────────────────────────────────────

    private UnitDisplay ParseUnitDisplay(string raw)
    {
        var result = new UnitDisplay();
        if (string.IsNullOrEmpty(raw)) return result;

        string[] parts = raw.Split(',');
        if (parts.Length > 0) float.TryParse(parts[0], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out result.x);
        if (parts.Length > 1) float.TryParse(parts[1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out result.y);
        if (parts.Length > 2) float.TryParse(parts[2], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out result.width);
        if (parts.Length > 3) float.TryParse(parts[3], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out result.height);
        if (parts.Length > 4) float.TryParse(parts[4], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out result.other);
        return result;
    }

    private static UnitStats ParseUnitStats(JObject o)
    {
        if (o == null) return new UnitStats();
        return new UnitStats
        {
            hp     = ParseInt(o["hp"]),
            hpMin  = ParseInt(o["hp min"]),
            hpMax  = ParseInt(o["hp max"]),
            atk    = ParseInt(o["atk"]),
            atkMin = ParseInt(o["atk min"]),
            atkMax = ParseInt(o["atk max"]),
            def    = ParseInt(o["def"]),
            defMin = ParseInt(o["def min"]),
            defMax = ParseInt(o["def max"]),
            rec    = ParseInt(o["rec"]),
            recMin = ParseInt(o["rec min"]),
            recMax = ParseInt(o["rec max"]),
        };
    }

    private Vector2 ParseVector2(string raw)
    {
        if (string.IsNullOrEmpty(raw)) return Vector2.zero;

        string[] parts = raw.Split(',');
        float x = 0f, y = 0f;
        if (parts.Length > 0) float.TryParse(parts[0], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out x);
        if (parts.Length > 1) float.TryParse(parts[1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out y);
        return new Vector2(x, y);
    }

    // ─────────────────────────────────────────────────────────────
    //  ABILITY BUILDERS
    // ─────────────────────────────────────────────────────────────

    private Ability CreateNormalAbility(string unitName, JObject data, string folder)
    {
        Ability a = GetOrCreate($"{folder}/{Sanitize(unitName)}_Normal.asset");
        a.abilityId        = data["id"]?.ToString();
        a.abilityName      = $"{unitName} Normal Attack";
        a.targetType       = TargetType.SingleEnemy;
        a.targetArea       = TargetArea.Single;
        a.dropChecksPerHit = ParseInt(data["normaldc"]);
        a.hitFrames        = ParseHitFramesFromString(
            data["normal_frames"]?.ToString(),
            data["normal_distribute"]?.ToString());

        // Effect frames from SKILL_MST
        if (_skillMstById.TryGetValue(GetAnimationId(data["id"]?.ToString()) ?? "", out JObject skillMstEntry))
        {
            string efRaw = skillMstEntry["effectFrames"]?.ToString();
            a.effectFrames = ParseEffectFrames(efRaw);
        }
        else
        {
            a.effectFrames = new List<EffectFrame>();
        }

        a.levels = new List<AbilityLevel>
        {
            new AbilityLevel { bcCost = 0, damageRate = 100, effects = new List<Effect>() }
        };
        EditorUtility.SetDirty(a);
        return a;
    }

    private Ability CreateLeveledAbility(JObject skillObj, JObject unitData,
                                         string dmgKey, string dcKey, string folder)
    {
        if (skillObj == null) return null;

        string name = skillObj["name"]?.ToString();
        if (string.IsNullOrWhiteSpace(name)) return null;

        string desc    = skillObj["desc"]?.ToString();
        int    dc      = ParseInt(unitData[dcKey]);
        int    dmgRate = ParseInt(unitData[dmgKey]);

        Ability a = GetOrCreate($"{folder}/{Sanitize(name)}.asset");
        a.abilityName      = name;
        a.abilityDesc      = desc;
        a.dropChecksPerHit = dc;
        a.abilityId        = skillObj["id"]?.ToString();

        JArray damageFrames = skillObj["damage frames"] as JArray;
        if (damageFrames?.Count > 0)
        {
            JObject first = damageFrames[0] as JObject;
            a.hitFrames = ParseHitFramesFromArrays(
                first?["frame times"]           as JArray,
                first?["hit dmg% distribution"] as JArray);
        }

        // Effect frames from SKILL_MST
        if (_skillMstById.TryGetValue(GetAnimationId(skillObj["id"]?.ToString()) ?? "", out JObject skillMstEntry))
        {
            string efRaw = skillMstEntry["effectFrames"]?.ToString();
            a.effectFrames = ParseEffectFrames(efRaw);
        }
        else
        {
            a.effectFrames = new List<EffectFrame>();
        }

        a.levels = new List<AbilityLevel>();
        JArray levels = skillObj["levels"] as JArray;
        if (levels != null)
        {
            foreach (JToken lvl in levels)
            {
                a.levels.Add(new AbilityLevel
                {
                    bcCost     = ParseInt(lvl["bc cost"]),
                    damageRate = dmgRate,
                    effects    = ParseEffects(lvl["effects"] as JArray)
                });
            }
        }

        Effect firstEffect = a.levels.LastOrDefault()?.effects?.FirstOrDefault();
        if (firstEffect != null)
        {
            a.targetType = firstEffect.targetType;
            a.targetArea = firstEffect.targetArea;
        }

        EditorUtility.SetDirty(a);
        return a;
    }

    private Ability CreateFlatAbility(JObject skillObj, string folder)
    {
        if (skillObj == null) return null;

        string name = skillObj["name"]?.ToString();
        if (string.IsNullOrWhiteSpace(name)) return null;
        string desc = skillObj["desc"]?.ToString();

        Ability a = GetOrCreate($"{folder}/{Sanitize(name)}.asset");
        a.abilityName = name;
        a.abilityDesc = desc;
        a.targetType  = TargetType.AllAllies;
        a.targetArea  = TargetArea.AOE;
        a.abilityId        = skillObj["id"]?.ToString();
        a.levels = new List<AbilityLevel>
        {
            new AbilityLevel
            {
                bcCost     = 0,
                damageRate = 0,
                effects    = ParseEffects(skillObj["effects"] as JArray)
            }
        };

        EditorUtility.SetDirty(a);
        return a;
    }

    private Ability GetOrCreate(string path)
    {
        Ability existing = AssetDatabase.LoadAssetAtPath<Ability>(path);
        if (existing != null) return existing;
        Ability a = ScriptableObject.CreateInstance<Ability>();
        AssetDatabase.CreateAsset(a, path);
        return a;
    }

    // ─────────────────────────────────────────────────────────────
    //  EFFECT PARSING
    // ─────────────────────────────────────────────────────────────

    private void LoadEffectGroupMst(string relativePath)
    {
        string abs = Application.dataPath + "/" + relativePath + ".json";
        if (!File.Exists(abs)) { Debug.LogWarning($"[EffectGroup] Not found: {abs}"); return; }

        JArray arr = JArray.Parse(File.ReadAllText(abs));
        foreach (JToken token in arr)
        {
            if (token is not JObject obj) continue;
            string groupId = obj["battleEffectGroupId"]?.ToString();
            if (string.IsNullOrEmpty(groupId)) continue;

            // "xW6TKu9G": "subFrame:clip.mp3,subFrame:clip.mp3,..."
            string soundRaw = obj["xW6TKu9G"]?.ToString() ?? "";
            var soundList = new List<GroupSoundEntry>();
            foreach (string entry in soundRaw.Split(',', StringSplitOptions.RemoveEmptyEntries))
            {
                string[] parts = entry.Split(':');
                if (parts.Length < 2) continue;
                if (!int.TryParse(parts[0].Trim(), out int sf)) continue;
                soundList.Add(new GroupSoundEntry { subFrame = sf, clipName = parts[1].Trim() });
            }
            if (!_effectGroupSoundFrames.ContainsKey(groupId))
                _effectGroupSoundFrames[groupId] = soundList;

            // "effectFrames": "subFrame:particleId:placement:xAnchor:yAnchor:offX:offY,..."
            string framesRaw = obj["effectFrames"]?.ToString() ?? "";
            var particleList = new List<GroupParticleEntry>();
            foreach (string entry in framesRaw.Split(',', StringSplitOptions.RemoveEmptyEntries))
            {
                string[] p = entry.Split(':');
                if (p.Length < 7) continue; // malformed — skip rather than guess

                if (!int.TryParse(p[0].Trim(), out int sf)) continue;

                // Some entries stack multiple particles at the same frame/position,
                // joined with '@' (e.g. "907@906" — seen on barrier-effect groups,
                // likely a base shape + an overlaid glow layer). Split unconditionally;
                // a normal single-id entry just becomes a one-element list.
                List<string> ids = p[1].Trim().Split('@').Select(s => s.Trim()).ToList();

                particleList.Add(new GroupParticleEntry
                {
                    subFrame      = sf,
                    particleIds   = ids,
                    placementType = int.TryParse(p[2], out int pt) ? pt : 1,
                    xAnchor       = int.TryParse(p[3], out int xa) ? xa : 2,
                    yAnchor       = int.TryParse(p[4], out int ya) ? ya : 2,
                    offsetX       = float.TryParse(p[5], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float ox) ? ox : 0f,
                    offsetY       = float.TryParse(p[6], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float oy) ? oy : 0f
                });
            }
            if (!_effectGroupParticles.ContainsKey(groupId))
                _effectGroupParticles[groupId] = particleList;
        }
        Debug.Log($"[EffectGroup] Loaded {_effectGroupParticles.Count} groups with particle data from {relativePath}");
    }

    private void LoadSkillMstFolder(string relativeFolderPath)
    {
        string abs = Application.dataPath + "/" + relativeFolderPath;
        if (!Directory.Exists(abs)) { Debug.LogWarning($"[SkillMST] Folder not found: {abs}"); return; }

        foreach (string file in Directory.GetFiles(abs, "*.json"))
        {
            JArray arr = JArray.Parse(File.ReadAllText(file));
            foreach (JToken token in arr)
            {
                if (token is not JObject obj) continue;
                string bbId = obj["bbId"]?.ToString();
                if (!string.IsNullOrEmpty(bbId) && !_skillMstById.ContainsKey(bbId))
                    _skillMstById[bbId] = obj;
            }
        }
        Debug.Log($"[SkillMST] Loaded {_skillMstById.Count} skill entries");
    }

    private List<EffectFrame> ParseEffectFrames(string raw)
    {
        List<EffectFrame> list = new();
        if (string.IsNullOrEmpty(raw)) return list;

        // raw = SKILL_MST's effectFrames: "skillFrame:groupId,skillFrame:groupId,..."
        foreach (string entry in raw.Split(','))
        {
            string[] parts = entry.Split(':');
            if (parts.Length < 2) continue;
            if (!int.TryParse(parts[0].Trim(), out int skillFrame)) continue;
            string groupId = parts[1].Trim();

            bool hasParticles = _effectGroupParticles.TryGetValue(groupId, out var particleEntries);
            bool hasSounds    = _effectGroupSoundFrames.TryGetValue(groupId, out var soundEntries);

            if (hasParticles && particleEntries.Count > 0)
            {
                foreach (var pe in particleEntries)
                {
                    AudioClip clip = null;
                    if (hasSounds)
                    {
                        var match = soundEntries.Find(s => s.subFrame == pe.subFrame);
                        if (match.clipName != null)
                        {
                            string clipName = Path.GetFileNameWithoutExtension(match.clipName);
                            clip = LoadAudioCached($"Assets/{soundFolderPath}/{clipName}.mp3")
                                ?? LoadAudioCached($"Assets/{soundFolderPath}/{clipName}.wav")
                                ?? LoadAudioCached($"Assets/{soundFolderPath}/{clipName}.ogg");
                        }
                    }

                    for (int i = 0; i < pe.particleIds.Count; i++)
                    {
                        string particleId = pe.particleIds[i];
                        _particleEffectsById.TryGetValue(particleId, out ParticleEffect resolved);
                        if (resolved == null)
                            Debug.LogWarning($"[EffectGroup {groupId}] No ParticleEffect SO found for id '{particleId}' — check {particleEffectFolder}.");

                        list.Add(new EffectFrame
                        {
                            frame               = skillFrame + pe.subFrame,
                            battleEffectGroupId = groupId,
                            particleEffectId    = particleId,
                            particleEffect      = resolved,
                            placementType       = pe.placementType,
                            xAnchor             = pe.xAnchor,
                            yAnchor             = pe.yAnchor,
                            offsetX             = pe.offsetX,
                            offsetY             = pe.offsetY,
                            // only the first stacked particle carries the sound —
                            // avoids the same clip firing twice for one visual moment
                            audioClip           = i == 0 ? clip : null
                        });
                    }
                }
            }

            // Sound sub-frames that didn't line up with any particle entry still need to fire
            // (groups can have more/fewer sounds than particles — e.g. "OD発動").
            if (hasSounds)
            {
                foreach (var se in soundEntries)
                {
                    bool alreadyCovered = hasParticles && particleEntries.Exists(p => p.subFrame == se.subFrame);
                    if (alreadyCovered) continue;

                    string clipName = Path.GetFileNameWithoutExtension(se.clipName);
                    AudioClip clip = LoadAudioCached($"Assets/{soundFolderPath}/{clipName}.mp3")
                        ?? LoadAudioCached($"Assets/{soundFolderPath}/{clipName}.wav")
                        ?? LoadAudioCached($"Assets/{soundFolderPath}/{clipName}.ogg");

                    list.Add(new EffectFrame
                    {
                        frame               = skillFrame + se.subFrame,
                        battleEffectGroupId = groupId,
                        audioClip           = clip
                    });
                }
            }

            if (!hasParticles && !hasSounds)
                Debug.LogWarning($"[EffectGroup] '{groupId}' referenced by SKILL_MST but not found in loaded EFFECT_GROUP_MST files.");
        }

        return list;
    }

    private readonly Dictionary<string, AudioClip> audioCache = new();
    private AudioClip LoadAudioCached(string path)
    {
        if (audioCache.TryGetValue(path, out var c)) return c;
        c = audioCache[path] = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
        return c;
    }
    private List<Effect> ParseEffects(JArray arr)
    {
        List<Effect> list = new();
        if (arr == null) return list;
        foreach (JToken t in arr)
            if (t is JObject obj) list.Add(ParseEffect(obj));
        return list;
    }

    private Effect ParseEffect(JObject obj)
    {
        bool isPassive = obj["passive id"] != null;

        Effect e = new Effect
        {
            procId           = obj["proc id"]?.ToString() ?? "",
            passiveId        = obj["passive id"]?.ToString() ?? "",
            isPassive        = isPassive,
            targetType       = ParseTargetType(obj["target type"]?.ToString()),
            targetArea       = ParseTargetArea(obj["target area"]?.ToString()),
            effectDelayFrame = obj["effect delay time(ms)/frame"]?.ToString(),
            conditions       = ParseConditions(obj["conditions"] as JArray),

            attack          = ParseAttack(obj),
            statBuff        = ParseStatBuff(obj),
            bbAtkBuff       = ParseBBAtkBuff(obj),
            heal            = ParseHeal(obj),
            gradualHeal     = ParseGradualHeal(obj),
            statDebuff      = ParseStatDebuff(obj),
            status          = ParseStatus(obj),
            damageBuff      = ParseDamageBuff(obj),
            bcFill          = ParseBCFill(obj),
            dot             = ParseDOT(obj),
            shield          = ParseShield(obj),
            revive          = ParseRevive(obj),
            conditionalBuff = ParseConditionalBuff(obj, isPassive ? obj["passive id"]?.ToString() : obj["proc id"]?.ToString()),
            ailmentInflict  = ParseAilmentInflict(obj),
            ailmentResist   = ParseAilmentResist(obj),
            extraAction     = ParseExtraAction(obj)
        };

        return e;
    }

    // ─────────────────────────────────────────────────────────────
    //  SUB-EFFECT PARSERS
    // ─────────────────────────────────────────────────────────────

    private static AttackEffect ParseAttack(JObject o) => new()
    {
        bbAtkPercent     = ParseInt(o["bb atk%"]),
        hitCount         = ParseInt(o["hits"]),
        bbFlatAtk        = ParseInt(o["bb flat atk"]),
        bbCritPercent    = ParseInt(o["bb crit%"]),
        bbBCPercent      = ParseInt(o["bb bc%"]),
        bbHCPercent      = ParseInt(o["bb hc%"]),
        randomTarget     = ParseBool(o["random attack"]),
        fixedDamage      = ParseInt(o["fixed damage"]),
        hpDamageChance   = ParseInt(o["hp% damage chance%"]),
        hpDamageHigh     = ParseInt(o["hp% damage high"]),
        hpDamageLow      = ParseInt(o["hp% damage low"]),
        hpDrainHigh      = ParseInt(o["hp drain% high"]),
        hpDrainLow       = ParseInt(o["hp drain% low"]),
        ignoresDef       = o["ignore def%"] != null,
        ignoreDefPercent = ParseInt(o["ignore def%"]),
        bbElements       = (o["bb elements"] as JArray)
                               ?.Select(t => ParseElement(t.ToString()))
                               .ToList() ?? new List<ElementalType>()
    };

    private static StatBuffEffect ParseStatBuff(JObject o) => new()
    {
        atkBuff       = ParseInt(o["atk% buff (1)"]) + ParseInt(o["atk% buff"]),
        defBuff       = ParseInt(o["def% buff (3)"]) + ParseInt(o["def% buff"]),
        recBuff       = ParseInt(o["rec% buff (5)"]) + ParseInt(o["rec% buff"]),
        hpBuff        = ParseInt(o["hp% buff"]),
        critBuff      = ParseInt(o["crit% buff (7)"]) + ParseInt(o["crit% buff"]),
        buffTurns     = ParseInt(o["buff turns"]),
        elementBuffed = o["element buffed"]?.ToString(),
        elementsBuffed= (o["elements buffed"] as JArray)
                            ?.Select(t => t.ToString()).ToList() ?? new List<string>(),
        fireResist    = ParseInt(o["fire resist%"]),
        waterResist   = ParseInt(o["water resist%"]),
        earthResist   = ParseInt(o["earth resist%"]),
        thunderResist = ParseInt(o["thunder resist%"]),
        lightResist   = ParseInt(o["light resist%"]),
        darkResist    = ParseInt(o["dark resist%"])
    };

    private static BBAtkBuffEffect ParseBBAtkBuff(JObject o) => new()
    {
        bbAtkBuff      = ParseInt(o["bb atk% buff"]),
        sbbAtkBuff     = ParseInt(o["sbb atk% buff"]),
        ubbAtkBuff     = ParseInt(o["ubb atk% buff"]),
        buffTurns      = ParseInt(o["buff turns (72)"]),
        bbBaseAtk      = ParseInt(o["bb base atk%"]),
        bbAtkIncPerUse = ParseInt(o["bb atk% inc per use"]),
        bbAtkMaxInc    = ParseInt(o["bb atk% max number of inc"]),
        bbBCPercent    = ParseInt(o["bb bc%"]),
        bbCritPercent  = ParseInt(o["bb crit%"]),
        bbFlatAtk      = ParseInt(o["bb flat atk"])
    };

    private static HealEffect ParseHeal(JObject o) => new()
    {
        healHigh        = ParseInt(o["heal high"]),
        healLow         = ParseInt(o["heal low"]),
        recAddedPercent = ParseInt(o["rec added% (from healer)"])
                        + ParseInt(o["angel idol recover hp%"])
    };

    private static GradualHealEffect ParseGradualHeal(JObject o) => new()
    {
        healHigh        = ParseInt(o["gradual heal high"]),
        healLow         = ParseInt(o["gradual heal low"]),
        turns           = ParseInt(o["gradual heal turns (8)"]),
        recAddedPercent = ParseInt(o["rec added% (from target)"])
    };

    private static StatDebuffEffect ParseStatDebuff(JObject o)
    {
        StatDebuffEffect d = new()
        {
            buffTurns             = ParseInt(o["buff turns"]),
            elementBuffed         = o["element buffed"]?.ToString(),
            inflictAtkDebuff      = ParseInt(o["inflict atk% debuff (2)"]),
            inflictAtkDebuffChance= ParseInt(o["inflict atk% debuff chance% (74)"]),
            inflictDefDebuff      = ParseInt(o["inflict def% debuff (4)"]),
            inflictDefDebuffChance= ParseInt(o["inflict def% debuff chance% (75)"]),
            inflictRecDebuff      = ParseInt(o["inflict rec% debuff (6)"]),
            inflictRecDebuffChance= ParseInt(o["inflict rec% debuff chance% (76)"]),
            statDebuffTurns       = ParseInt(o["stat% debuff turns"])
        };

        if (o["buff #1"] is JObject b1)
            d.buff1 = new StatDebuffEntry
            {
                atkBuffPercent = ParseInt(b1["atk% buff (2)"]),
                defBuffPercent = ParseInt(b1["def% buff (4)"]),
                procChance     = ParseFloat(b1["proc chance%"])
            };

        if (o["buff #2"] is JObject b2)
            d.buff2 = new StatDebuffEntry
            {
                atkBuffPercent = ParseInt(b2["atk% buff (2)"]),
                defBuffPercent = ParseInt(b2["def% buff (4)"]),
                procChance     = ParseFloat(b2["proc chance%"])
            };

        return d;
    }

    private static StatusEffect ParseStatus(JObject o) => new()
    {
        poisonChance    = ParseInt(o["poison%"])    + ParseInt(o["poison% buff"]),
        weakenChance    = ParseInt(o["weaken%"])    + ParseInt(o["weaken% buff"]),
        sickChance      = ParseInt(o["sick%"])      + ParseInt(o["sick% buff"]),
        injuryChance    = ParseInt(o["injury%"])    + ParseInt(o["injury% buff"]),
        curseChance     = ParseInt(o["curse%"])     + ParseInt(o["curse% buff"]),
        paralysisChance = ParseInt(o["paralysis%"]) + ParseInt(o["paralysis% buff"]),
        buffTurns       = ParseInt(o["buff turns"]),
        removeAll       = ParseBool(o["remove all status ailments"]),
        ailmentsCured   = (o["ailments cured"] as JArray)
                              ?.Select(t => t.ToString()).ToList() ?? new List<string>(),
        poisonResist    = ParseInt(o["resist poison% (30)"]),
        weakenResist    = ParseInt(o["resist weaken% (31)"]),
        sickResist      = ParseInt(o["resist sick% (32)"]),
        injuryResist    = ParseInt(o["resist injury% (33)"]),
        curseResist     = ParseInt(o["resist curse% (34)"]),
        paralysisResist = ParseInt(o["resist paralysis% (35)"]),
        resistTurns     = ParseInt(o["resist status ails turns"])
    };

    private static DamageBuffEffect ParseDamageBuff(JObject o)
    {
        DamageBuffEffect d = new()
        {
            sparkDmgBuff                = ParseInt(o["spark dmg% buff (40)"])
                                        + ParseInt(o["spark dmg inc% buff"]),
            sparkDmgBuffTurns           = ParseInt(o["buff turns"])
                                        + ParseInt(o["spark dmg inc buff turns (131)"]),
            sparkDmgIncChance           = ParseInt(o["spark dmg inc chance%"]),
            critMultiplier              = ParseInt(o["crit multiplier%"]),
            critBuffTurns               = ParseInt(o["buff turns (84)"]),
            elementalWeaknessMultiplier = ParseFloat(o["elemental weakness multiplier%"]),
            elementalWeaknessTurns      = ParseInt(o["elemental weakness buff turns"]),
            fireDoesExtraElementalDmg   = ParseBool(o["fire units do extra elemental weakness dmg"]),
            waterDoesExtraElementalDmg  = ParseBool(o["water units do extra elemental weakness dmg"]),
            earthDoesExtraElementalDmg  = ParseBool(o["earth units do extra elemental weakness dmg"]),
            thunderDoesExtraElementalDmg= ParseBool(o["thunder units do extra elemental weakness dmg"]),
            lightDoesExtraElementalDmg  = ParseBool(o["light units do extra elemental weakness dmg"]),
            darkDoesExtraElementalDmg   = ParseBool(o["dark units do extra elemental weakness dmg"]),
            dmgMitigationPercent        = ParseInt(o["dmg% mitigation"])
                                        + ParseInt(o["dmg% reduction"]),
            dmgMitigationTurns          = ParseInt(o["dmg% reduction turns (36)"]),
            mitigateFire                = o["mitigate fire attacks (21)"] != null
                                       || o["mitigate fire attacks"] != null,
            mitigateWater               = o["mitigate water attacks (22)"] != null
                                       || o["mitigate water attacks"] != null,
            mitigateEarth               = o["mitigate earth attacks"] != null,
            mitigateThunder             = o["mitigate thunder attacks"] != null,
            mitigateLight               = o["mitigate light attacks (25)"] != null
                                       || o["mitigate light attacks"] != null,
            mitigateDark                = o["mitigate dark attacks (26)"] != null
                                       || o["mitigate dark attacks"] != null,
            defenseIgnorePercent        = ParseInt(o["defense% ignore"]),
            defenseIgnoreTurns          = ParseInt(o["defense% ignore turns (39)"])
        };

        if (o["buff"] is JObject sub)
        {
            d.hasSubBuff        = true;
            d.hpBelowActivation = o["hp below % buff activation"] != null;
            d.hpBelowThreshold  = ParseInt(o["hp below % buff activation"]);

            if (sub["angel idol buff (12)"] != null)
                d.angelIdolSubBuff = new AngelIdolSubBuff
                {
                    buffTurns        = ParseInt(sub["buff turns (12)"]),
                    recoverHpPercent = ParseFloat(sub["angel idol recover hp%"])
                };

            if (sub["dmg reduction% buff"] != null)
                d.dmgReductionSubBuff = new DmgReductionSubBuff
                {
                    buffTurns           = ParseInt(sub["buff turns (36)"]),
                    dmgReductionPercent = ParseFloat(sub["dmg reduction% buff"])
                };

            if (sub["gradual heal high"] != null)
                d.gradualHealSubBuff = new GradualHealSubBuff
                {
                    buffTurns = ParseInt(sub["buff turns (8)"]),
                    healHigh  = ParseInt(sub["gradual heal high"]),
                    healLow   = ParseInt(sub["gradual heal low"])
                };
        }

        return d;
    }

    private static BCFillEffect ParseBCFill(JObject o) => new()
    {
        bbBCFill                   = ParseInt(o["bb bc fill"]),
        bbBCFillPercent            = ParseFloat(o["bb bc fill%"]),
        bcFillPerTurn              = ParseInt(o["bc fill per turn"]),
        bcFillOnSparkHigh          = ParseInt(o["bc fill on spark high"]),
        bcFillOnSparkLow           = ParseInt(o["bc fill on spark low"]),
        bcFillOnSparkPercent       = ParseFloat(o["bc fill on spark%"]),
        bcFillWhenAttackedHigh     = ParseInt(o["bc fill when attacked high"]),
        bcFillWhenAttackedLow      = ParseInt(o["bc fill when attacked low"]),
        bcFillWhenAttackedPercent  = ParseFloat(o["bc fill when attacked%"]),
        bcFillWhenAttackedTurns    = ParseInt(o["bc fill when attacked turns (38)"]),
        bcFillWhenAttackingHigh    = ParseInt(o["bc fill when attacking high"]),
        bcFillWhenAttackingLow     = ParseInt(o["bc fill when attacking low"]),
        bcFillWhenAttackingPercent = ParseFloat(o["bc fill when attacking%"]),
        bcFillOnEnemyDefeatHigh    = ParseInt(o["bc fill on enemy defeat high"]),
        bcFillOnEnemyDefeatLow     = ParseInt(o["bc fill on enemy defeat low"]),
        bcFillOnEnemyDefeatPercent = ParseFloat(o["bc fill on enemy defeat%"]),
        gradualBCFillTurns         = ParseInt(o["increase bb gauge gradual turns (37)"]),
        bbGaugeFillRate            = ParseFloat(o["bb gauge fill rate%"])
    };

    private static DotEffect ParseDOT(JObject o) => new()
    {
        atkPercent = ParseInt(o["dot atk%"]),
        flatAtk    = ParseInt(o["dot flat atk"]),
        element    = ParseElement(o["dot element affected"]?.ToString()),
        turns      = ParseInt(o["dot turns (71)"]),
        unitIndex  = ParseInt(o["dot unit index"])
    };

    private static ShieldEffect ParseShield(JObject o) => new()
    {
        dmgMitigationPercent             = ParseInt(o["dmg% mitigation"]),
        maxHPIncreasePercent             = ParseInt(o["max hp% increase"]),
        barrierHP                        = ParseInt(o["elemental barrier hp"]),
        barrierDef                       = ParseInt(o["elemental barrier def"]),
        barrierElement                   = ParseElement(o["elemental barrier element"]?.ToString()),
        barrierAbsorbPercent             = ParseFloat(o["elemental barrier absorb dmg%"]),
        dmgMitigationForElementalAttacks = ParseInt(o["dmg% mitigation for elemental attacks"])
    };

    private static ReviveEffect ParseRevive(JObject o) => new()
    {
        reviveChance    = ParseFloat(o["revive unit chance%"]),
        reviveHPPercent = ParseFloat(o["revive unit hp%"]),
        triggerOnBB     = ParseBool(o["trigger on bb"]),
        triggerOnSBB    = ParseBool(o["trigger on sbb"]),
        triggerOnUBB    = ParseBool(o["trigger on ubb"])
    };

    private static ConditionalBuffEffect ParseConditionalBuff(JObject o, string id)
    {
        string triggerType = id switch
        {
            "78" => "damage_received",
            "80" => "damage_dealt",
            "82" => "bc_count",
            "84" => "hc_count",
            "86" => "spark_count",
            "88" => "on_guard",
            "89" => "on_crit",
            _    => "unknown"
        };

        ConditionalSubBuff sub = new();
        if (o["buff"] is JObject buff)
        {
            sub.atkBuff             = ParseInt(buff["atk% buff (1)"]);
            sub.defBuff             = ParseInt(buff["def% buff (3)"]);
            sub.bbAtkBuff           = ParseInt(buff["bb atk% buff"]);
            sub.sbbAtkBuff          = ParseInt(buff["sbb atk% buff"]);
            sub.ubbAtkBuff          = ParseInt(buff["ubb atk% buff"]);
            sub.sparkDmgBuff        = ParseInt(buff["spark dmg% buff"]);
            sub.dmgReductionBuff    = ParseInt(buff["dmg reduction% buff"]);
            sub.gradualHealHigh     = ParseInt(buff["gradual heal high"]);
            sub.gradualHealLow      = ParseInt(buff["gradual heal low"]);
            sub.odFillRate          = ParseInt(buff["od fill rate% buff"]);
            sub.hpDrainChance       = ParseInt(buff["hp drain chance%"]);
            sub.hpDrainHigh         = ParseInt(buff["hp drain% high"]);
            sub.hpDrainLow          = ParseInt(buff["hp drain% low"]);
            sub.sparkDmgInc         = ParseInt(buff["spark dmg inc%"]);
            sub.bcFillOnSparkHigh   = ParseInt(buff["bc fill on spark high"]);
            sub.bcFillOnSparkLow    = ParseInt(buff["bc fill on spark low"]);
            sub.bcFillOnSparkPercent= ParseFloat(buff["bc fill on spark%"]);
            sub.gradualBcFill       = ParseInt(buff["increase bb gauge gradual buff"]);
            sub.elementBuffed       = buff["element buffed"]?.ToString();
            sub.buffTurns           = ParseInt(buff["buff turns (1)"])
                                    + ParseInt(buff["buff turns (3)"])
                                    + ParseInt(buff["buff turns (36)"])
                                    + ParseInt(buff["buff turns (40)"])
                                    + ParseInt(buff["buff turns (72)"]);
        }

        return new ConditionalBuffEffect
        {
            triggerType      = triggerType,
            activationChance = ParseFloat(o["on guard activation chance%"])
                             + ParseFloat(o["on crit activation chance%"]),
            buff             = sub
        };
    }

    private static AilmentInflictEffect ParseAilmentInflict(JObject o) => new()
    {
        poisonChance    = ParseInt(o["inflict poison%"]),
        weakenChance    = ParseInt(o["inflict weaken%"]),
        sickChance      = ParseInt(o["inflict sick%"]),
        injuryChance    = ParseInt(o["inflict injury%"]),
        curseChance     = ParseInt(o["inflict curse%"]),
        paralysisChance = ParseInt(o["inflict paralysis%"])
    };

    private static AilmentResistEffect ParseAilmentResist(JObject o) => new()
    {
        poisonResist      = ParseInt(o["poison resist%"]),
        weakenResist      = ParseInt(o["weaken resist%"]),
        sickResist        = ParseInt(o["sick resist%"]),
        injuryResist      = ParseInt(o["injury resist%"]),
        curseResist       = ParseInt(o["curse resist%"]),
        paralysisResist   = ParseInt(o["paralysis resist%"]),
        atkDownResist     = ParseInt(o["atk down resist% (120)"])
                          + ParseInt(o["atk down resist%"]),
        defDownResist     = ParseInt(o["def down resist% (121)"])
                          + ParseInt(o["def down resist%"]),
        recDownResist     = ParseInt(o["rec down resist% (122)"])
                          + ParseInt(o["rec down resist%"]),
        immunityBuffTurns = ParseInt(o["stat down immunity buff turns"])
    };

    private static ExtraActionEffect ParseExtraAction(JObject o) => new()
    {
        chance          = ParseFloat(o["chance% for extra action"]),
        maxExtraActions = ParseInt(o["max number of extra actions"]),
        buffTurns       = ParseInt(o["extra action buff turns (123)"])
    };

    // ─────────────────────────────────────────────────────────────
    //  CONDITIONS
    // ─────────────────────────────────────────────────────────────

    private static List<EffectCondition> ParseConditions(JArray arr)
    {
        List<EffectCondition> list = new();
        if (arr == null) return list;

        foreach (JToken t in arr)
        {
            if (t is not JObject o) continue;

            EffectCondition c = new();

            if (o["hp above % buff requirement"] != null)
            {
                c.conditionType      = ConditionType.HPAbove;
                c.hpThresholdPercent = ParseFloat(o["hp above % buff requirement"]);
            }
            else if (o["hp below % buff requirement"] != null)
            {
                c.conditionType      = ConditionType.HPBelow;
                c.hpThresholdPercent = ParseFloat(o["hp below % buff requirement"]);
            }
            else if (o["item required"] is JArray items)
            {
                c.conditionType = ConditionType.ItemRequired;
                c.itemsRequired = items.Select(i => i.ToString()).ToList();
            }
            else if (o["elements required"] is JArray elems)
            {
                c.conditionType    = ConditionType.ElementRequired;
                c.elementsRequired = elems.Select(e => e.ToString()).ToList();
            }
            else if (o["gender required"] != null)
            {
                c.conditionType  = ConditionType.GenderRequired;
                c.genderRequired = o["gender required"].ToString();
            }
            else if (o["bb gauge above % buff requirement"] != null)
            {
                c.conditionType    = ConditionType.BBGaugeAbove;
                c.bbGaugeThreshold = ParseInt(o["bb gauge above % buff requirement"]);
            }
            else
            {
                c.conditionType = ConditionType.Always;
            }

            list.Add(c);
        }

        return list;
    }

    // ─────────────────────────────────────────────────────────────
    //  ANIMATION
    // ─────────────────────────────────────────────────────────────

    private void AssignAnimationData(Unit unit, string unitId)
    {
        string anim_id     = GetAnimationId(unitId);

        string cggAbs = $"{Application.dataPath}/{cggFolderPath}/unit_cgg_{anim_id}.csv";

        if (File.Exists(cggAbs))
        {
            unit.animationType = AnimationType.CGG;
            unit.cggFile       = LoadTextCached($"Assets/{cggFolderPath}/unit_cgg_{anim_id}.csv");
            unit.idleCgsFile   = LoadTextCached($"Assets/{cgsFolderPath}/unit_idle_cgs_{anim_id}.csv");
            unit.moveCgsFile   = LoadTextCached($"Assets/{cgsFolderPath}/unit_move_cgs_{anim_id}.csv");
            unit.attackCgsFile = LoadTextCached($"Assets/{cgsFolderPath}/unit_atk_cgs_{anim_id}.csv");

            Texture2D sheet = LoadTextureCached($"Assets/{spriteFolderPath}/unit_anime_{unitId}.png");
            if (sheet != null) unit.spriteSheets = new[] { sheet };
        }
        else
        {
            string samRel = $"{samRootFolderPath}/unit_{unitId}/unit_anime_{unitId}.json";
            if (File.Exists($"{Application.dataPath}/{samRel}"))
            {
                unit.animationType = AnimationType.SAM;
                unit.samFile       = AssetDatabase.LoadAssetAtPath<TextAsset>($"Assets/{samRel}");
            }
            else
            {
                Debug.LogWarning($"[Unit {unitId}] No animation data found.");

                LogMissingAnimation(unitId, unit.unitName);
            }
        }

        // Illustrations: try the plain filename first, then "_v2".."_v9" —
        // some units ship re-releases/alt versions of their art under a
        // versioned suffix instead of the base name.
        unit.unitSlotIcon = LoadSpriteWithVersions($"Assets/{spriteFolderPath}/unit_ills_thum_{unitId}");
        unit.unitIcon     = LoadSpriteWithVersions($"Assets/{spriteFolderPath}/unit_ills_battle_{unitId}");
        unit.unitFullArt  = LoadSpriteWithVersions($"Assets/{spriteFolderPath}/unit_ills_full_{unitId}");
    }

    /// <summary>
    /// Loads a sprite at "{basePathNoExt}.png", falling back to
    /// "{basePathNoExt}_v2.png" through "{basePathNoExt}_v9.png" in order if
    /// the base file isn't found. Returns null if none of them exist.
    /// </summary>
    private Sprite LoadSpriteWithVersions(string basePathNoExt)
    {
        Sprite sprite = LoadSpriteCached($"{basePathNoExt}.png");
        if (sprite != null) return sprite;

        for (int v = 2; v <= 9; v++)
        {
            sprite = LoadSpriteCached($"{basePathNoExt}__v{v}.png");
            if (sprite != null) return sprite;
        }

        return null;
    }

    // ─────────────────────────────────────────────────────────────
    //  HELPERS
    // ─────────────────────────────────────────────────────────────

    private static List<HitFrame> ParseHitFramesFromString(string frames, string dist)
    {
        List<HitFrame> list = new();
        if (string.IsNullOrEmpty(frames) || string.IsNullOrEmpty(dist)) return list;
        string[] f = frames.Split(',');
        string[] d = dist.Split(',');
        for (int i = 0; i < Mathf.Min(f.Length, d.Length); i++)
            list.Add(new HitFrame { frame = ParseInt(f[i].Trim()), damagePercent = ParseInt(d[i].Trim()) });
        return list;
    }

    private static List<HitFrame> ParseHitFramesFromArrays(JArray frames, JArray dist)
    {
        List<HitFrame> list = new();
        if (frames == null || dist == null) return list;
        int count = Mathf.Min(frames.Count, dist.Count);
        for (int i = 0; i < count; i++)
            list.Add(new HitFrame { frame = ParseInt(frames[i]), damagePercent = ParseInt(dist[i]) });
        return list;
    }

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        AssetDatabase.CreateFolder(Path.GetDirectoryName(path), Path.GetFileName(path));
    }

    private TextAsset LoadTextCached(string path)
    {
        if (textCache.TryGetValue(path, out var t)) return t;
        t = textCache[path] = AssetDatabase.LoadAssetAtPath<TextAsset>(path);
        return t;
    }

    private Texture2D LoadTextureCached(string path)
    {
        if (textureCache.TryGetValue(path, out var t)) return t;
        t = textureCache[path] = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        return t;
    }

    private Sprite LoadSpriteCached(string path)
    {
        if (spriteCache.TryGetValue(path, out var s)) return s;
        s = spriteCache[path] = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        return s;
    }

    private static int ParseInt(JToken t)
    {
        if (t == null) return 0;
        string s = t.ToString().Trim();
        if (int.TryParse(s, out int i))     return i;
        if (float.TryParse(s, out float f)) return Mathf.RoundToInt(f);
        return 0;
    }

    private static float ParseFloat(JToken t)
    {
        if (t == null) return 0f;
        float.TryParse(t.ToString().Trim(), System.Globalization.NumberStyles.Float,
                       System.Globalization.CultureInfo.InvariantCulture, out float f);
        return f;
    }

    private static bool ParseBool(JToken t)
    {
        if (t == null) return false;
        if (t.Type == JTokenType.Boolean) return t.Value<bool>();
        return t.ToString().Trim().ToLowerInvariant() == "true";
    }

    private static string Sanitize(string s)
    {
        if (string.IsNullOrEmpty(s)) return "Unnamed";
        foreach (char c in Path.GetInvalidFileNameChars()
                               .Concat(new[] { ' ', '\'', ':', ',', '.', '/' }))
            s = s.Replace(c.ToString(), "");
        return s;
    }

    private static TargetType ParseTargetType(string s) => s switch
    {
        "enemy" => TargetType.AllEnemies,
        "party" => TargetType.AllAllies,
        "self"  => TargetType.Self,
        _       => TargetType.SingleEnemy
    };

    private static TargetArea ParseTargetArea(string s) => s switch
    {
        "aoe"    => TargetArea.AOE,
        "single" => TargetArea.Single,
        "random" => TargetArea.Random,
        _        => TargetArea.AOE
    };

    private static ElementalType ParseElement(string s) => s?.ToLower() switch
    {
        "fire"    => ElementalType.Fire,
        "water"   => ElementalType.Water,
        "earth"   => ElementalType.Earth,
        "thunder" => ElementalType.Thunder,
        "light"   => ElementalType.Light,
        "dark"    => ElementalType.Dark,
        _         => ElementalType.None
    };

    private static UnitRarity ParseRarity(string r) => r switch
    {
        "Omni"      => UnitRarity.OMNI,
        "★★★★★★★" => UnitRarity.SEVEN,
        "★★★★★★"  => UnitRarity.SIX,
        "★★★★★"   => UnitRarity.FIVE,
        "★★★★"    => UnitRarity.FOUR,
        "★★★"     => UnitRarity.THREE,
        "★★"      => UnitRarity.TWO,
        "★"       => UnitRarity.ONE,
        _           => UnitRarity.ONE
    };

    private static string GetAnimationId(string unitId) => unitId switch
    {
        "10202" or "20202" or "30202" or "40202" or "50202" => "60132",
        "10203" or "20203" or "30203" or "40203" or "50203" => "60133",
        "10204" or "20204" or "30204" or "40204" or "50204" => "60134",
        _ => unitId
    };
}
#endif