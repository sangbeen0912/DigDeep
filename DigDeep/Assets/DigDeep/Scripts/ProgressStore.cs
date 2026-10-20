using System;
using System.Collections.Generic;
using UnityEngine;
namespace DigDeep
{
    [Serializable] public sealed class PickProgress { public string id; public float power; public int maximum, upgrade; }
    [Serializable] public sealed class ProgressData
    {
        public int version = 7, money, coal, iron, gold, diamond, oil, energyTenths, bestDepth;
        public TutorialProgress tutorial = new TutorialProgress();
        public List<string> discovered = new List<string>();
        public List<PickProgress> picks = new List<PickProgress>();
        public static ProgressData Initial(GameSettings settings, int money = 0) => new ProgressData {
            money = Math.Max(0, money), picks = new List<PickProgress> {
                new PickProgress { id = "WOOD", power = settings.woodPower, maximum = settings.woodDurability } } };
        public static bool TryParse(string json, out ProgressData data)
        {
            data = null;
            if (string.IsNullOrEmpty(json) || !json.Contains("\"version\"")) return false;
            try { data = JsonUtility.FromJson<ProgressData>(json); } catch { return false; }
            if (data == null || data.version < 1 || data.version > 7 || data.money < 0 || data.coal < 0 || data.iron < 0 || data.gold < 0 || data.diamond < 0 || data.oil < 0 || data.energyTenths < 0 || data.bestDepth < 0 ||
                data.picks == null || data.picks.Count < 1 || data.picks.Count > GameSettings.MaximumPicks) return false;
            foreach (var p in data.picks)
                if (p == null || (p.id != "WOOD" && p.id != "STONE" && p.id != "IRON" && p.id != "ALLOY" && p.id != "DIAMOND") || p.upgrade < 0) return false;
            // Duplicate WOOD entries represent distinct owned pickaxes.
            if (data.discovered == null) data.discovered = new List<string>();
            foreach (var id in data.discovered) if (OreCatalog.FromId(id) == null) return false;
            // Old snapshots did not record discoveries. Owned ore proves prior acquisition.
            if (data.coal > 0 && !data.discovered.Contains("Coal")) data.discovered.Add("Coal");
            if (data.iron > 0 && !data.discovered.Contains("Iron")) data.discovered.Add("Iron");
            if (data.gold > 0 && !data.discovered.Contains("Gold")) data.discovered.Add("Gold");
            if (data.diamond > 0 && !data.discovered.Contains("Diamond")) data.discovered.Add("Diamond");
            if (data.oil > 0 && !data.discovered.Contains("Oil")) data.discovered.Add("Oil");
            if (data.tutorial == null) data.tutorial = new TutorialProgress();
            if (!data.tutorial.Valid) return false;
            data.version = 7; return true;
        }
    }
    public sealed class ProgressStore
    {
        readonly string key, backup, legacy;
        bool writable = true;
        public string Warning { get; private set; } = "";
        public ProgressStore(string suffix = "")
        {
#if UNITY_EDITOR
            if (string.IsNullOrEmpty(suffix) && Array.IndexOf(Environment.GetCommandLineArgs(), "-digdeep-revision-test") >= 0) suffix = ".Verification";
#endif
            if (string.IsNullOrEmpty(suffix) && Array.IndexOf(Environment.GetCommandLineArgs(), "-digdeep-smoke") >= 0) suffix = ".Smoke";
            key = "DigDeep.Progress.v1" + suffix; backup = "DigDeep.Progress.backup.v1" + suffix; legacy = GameSettings.MoneyKey + suffix;
        }
        public ProgressData Load(GameSettings settings)
        {
            if (PlayerPrefs.HasKey(key))
            {
                if (ProgressData.TryParse(PlayerPrefs.GetString(key), out var data)) return data;
                if (ProgressData.TryParse(PlayerPrefs.GetString(backup, ""), out var previous)) { Warning = "이전 저장 기록을 복구했습니다."; return previous; }
                writable = false; Warning = "저장 기록을 읽지 못했습니다. 원본은 보존 중입니다."; return ProgressData.Initial(settings);
            }
            return ProgressData.Initial(settings, PlayerPrefs.GetInt(legacy, 0));
        }
        public bool Save(ProgressData data)
        {
            if (!writable) return false;
            string json = JsonUtility.ToJson(data); if (!ProgressData.TryParse(json, out _)) return false;
            try {
                string previous = PlayerPrefs.GetString(key, "");
                if (previous == json) return true;
                if (ProgressData.TryParse(previous, out _)) PlayerPrefs.SetString(backup, previous);
                PlayerPrefs.SetString(key, json); PlayerPrefs.Save(); Warning = ""; return true;
            } catch (Exception ex) { Warning = "자동 저장에 실패했습니다. 저장 공간을 확인해 주세요."; Debug.LogWarning(ex.Message); return false; }
        }
        public bool Reset(ProgressData initial)
        {
            writable = true;
            try {
                string json = JsonUtility.ToJson(initial); PlayerPrefs.SetString(key, json); PlayerPrefs.SetString(backup, json);
                PlayerPrefs.DeleteKey(legacy); PlayerPrefs.Save(); Warning = ""; return true;
            } catch (Exception ex) { Warning = "초기화 저장에 실패했습니다. 다시 시도해 주세요."; Debug.LogWarning(ex.Message); return false; }
        }
    }
}
