using System;
using System.Collections.Generic;
using UnityEngine;
namespace DigDeep
{
    [Serializable] public sealed class PickProgress { public string id; public int power, maximum, upgrade; }
    [Serializable] public sealed class ProgressData
    {
        public int version = 1, money;
        public List<PickProgress> picks = new List<PickProgress>();
        public static ProgressData Initial(GameSettings s, int money = 0) => new ProgressData {
            money = Math.Max(0, money), picks = new List<PickProgress> {
                new PickProgress { id = "WOOD", power = s.woodPower, maximum = s.woodDurability },
                new PickProgress { id = "IRON", power = s.ironPower, maximum = s.ironDurability } } };
        public static bool TryParse(string json, out ProgressData data)
        {
            data = null;
            if (string.IsNullOrEmpty(json) || !json.Contains("\"version\"")) return false;
            try { data = JsonUtility.FromJson<ProgressData>(json); } catch { return false; }
            if (data == null || data.version != 1 || data.money < 0 || data.picks == null || data.picks.Count < 1 || data.picks.Count > 2) return false;
            var ids = new HashSet<string>();
            foreach (var p in data.picks)
                if (p == null || (p.id != "WOOD" && p.id != "IRON") || !ids.Add(p.id) || p.power < 1 || p.maximum < 1 || p.upgrade < 0) return false;
            return true;
        }
    }
    // Growth only: no terrain, position, session state or current durability.
    public sealed class ProgressStore
    {
        readonly string Key, Backup, legacy;
        public ProgressStore(string suffix = "")
        {
#if UNITY_EDITOR
            if (string.IsNullOrEmpty(suffix) && Array.IndexOf(Environment.GetCommandLineArgs(), "-digdeep-revision-test") >= 0) suffix = ".Verification";
#endif
            Key = "DigDeep.Progress.v1" + suffix;
            Backup = "DigDeep.Progress.backup.v1" + suffix;
            legacy = GameSettings.MoneyKey + suffix;
        }
        bool writable = true;
        public string Warning { get; private set; } = "";
        public ProgressData Load(GameSettings settings)
        {
            if (PlayerPrefs.HasKey(Key))
            {
                if (ProgressData.TryParse(PlayerPrefs.GetString(Key), out var data)) return data;
                if (ProgressData.TryParse(PlayerPrefs.GetString(Backup, ""), out var previous))
                { Warning = "이전 저장 기록을 복구했습니다."; return previous; }
                writable = false; Warning = "저장 기록을 읽지 못했습니다. 원본은 보존 중입니다.";
                return ProgressData.Initial(settings);
            }
            return ProgressData.Initial(settings, PlayerPrefs.GetInt(legacy, 0));
        }
        public bool Save(ProgressData data)
        {
            if (!writable) return false;
            string json = JsonUtility.ToJson(data);
            if (!ProgressData.TryParse(json, out _)) return false;
            try {
                string previous = PlayerPrefs.GetString(Key, "");
                if (ProgressData.TryParse(previous, out _)) PlayerPrefs.SetString(Backup, previous);
                PlayerPrefs.SetString(Key, json); PlayerPrefs.Save(); Warning = ""; return true;
            } catch (Exception ex) { Warning = "자동 저장에 실패했습니다. 저장 공간을 확인해 주세요."; Debug.LogWarning(ex.Message); return false; }
        }
        public bool Reset(ProgressData initial)
        {
            writable = true;
            try {
                string json = JsonUtility.ToJson(initial);
                PlayerPrefs.SetString(Key, json); PlayerPrefs.SetString(Backup, json);
                PlayerPrefs.DeleteKey(legacy); PlayerPrefs.Save(); Warning = ""; return true;
            } catch (Exception ex) { Warning = "초기화 저장에 실패했습니다. 다시 시도해 주세요."; Debug.LogWarning(ex.Message); return false; }
        }
    }
}
