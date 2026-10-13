using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
namespace DigDeep.Editor
{
    public static class GoldAlloyFeatureChecks
    {
        static int checks;
        static void Check(bool valid, string message) { checks++; if (!valid) throw new Exception("Gold/alloy verification: " + message); }
        public static void ApplyAndVerify()
        {
            TutorialFeatureChecks.ApplyAndVerify();
            checks = 0;
            var settings = AssetDatabase.LoadAssetAtPath<GameSettings>("Assets/DigDeep/GameSettings.asset");
            var config = UnityEngine.Object.Instantiate(settings);
            try { Rules(config); SpawnRules(settings); Ui(config); }
            finally { UnityEngine.Object.DestroyImmediate(config); }
            File.WriteAllText("Logs/MiningUpgrade/gold-alloy-checks.txt", "Existing checks: 136\nGold/alloy checks: " + checks + "\nNo player build or deployment.\n");
            Debug.Log("DIGDEEP_GOLD_ALLOY_PASSED " + checks + " NO_BUILD");
        }
        static ProgressData AlloyData(GameSettings s)
        { var d = ProgressData.Initial(s); d.picks[0].id = "ALLOY"; d.tutorial = TutorialProgress.Completed(); return d; }
        static void Rules(GameSettings s)
        {
            s.spawnLines = new[] { new SpawnLine { firstRow = 1, lastRow = 45, goldPercent = 100 } };
            var run = new MiningSession(s, AlloyData(s), 91); float lastDamage = 0;
            run.Impact += (cell, damage, broken) => lastDamage = damage;
            Check(run.Picks[0].Power == 2.5f && run.Picks[0].Maximum == 80, "alloy power and durability");
            run.Start(); run.Strike();
            Check(run.Health(new Vector2Int(2, 0)) == 12.5f && lastDamage == 2.5f && run.Gold == 0, "fractional HP survives first hit");
            for (int hit = 1; hit < 5; hit++) run.Strike();
            Check(run.Health(new Vector2Int(2, 0)) == 2.5f && run.Gold == 0 && !run.IsDiscovered(Mineral.Gold), "no partial reward or discovery");
            run.Strike();
            Check(run.Gold == 1 && run.RunGold == 1 && run.Money == 0 && run.Picks[0].Remaining == 74 && run.IsDiscovered(Mineral.Gold), "six hits award one gold and consume six durability");
            Check(!run.Sell(Mineral.Gold, 1), "gold sale prohibited during mining");
            run.Regroup(); Check(run.Gold == 1 && run.Picks[0].Remaining == 80 && run.RunGold == 0, "regroup restores tool and keeps gold");
            Check(s.SellPrice(Mineral.Gold) == 40 && s.SellPrice(Mineral.Iron) == 18, "gold fixed price overrides health formula");
            Check(run.Sell(Mineral.Gold, 1) && run.Money == 40 && run.Gold == 0 && run.IsDiscovered(Mineral.Gold), "gold sale and permanent discovery");
            Check(ProgressData.TryParse(JsonUtility.ToJson(run.Growth()), out var restored) && new MiningSession(s, restored).IsDiscovered(Mineral.Gold), "gold discovery survives zero-stock reconnect");
            s.spawnLines = new SpawnLine[0]; run = new MiningSession(s, AlloyData(s)); run.Impact += (c, damage, b) => lastDamage = damage; run.Start(); run.Strike();
            Check(lastDamage == 1 && run.Money == 1 && run.Picks[0].Remaining == 79, "overkill caps damage but costs one strike");
            s.spawnLines = new[] { new SpawnLine { firstRow = 1, lastRow = 45, coalPercent = 100 } };
            run = new MiningSession(s, AlloyData(s)); run.Start(); run.Strike(); run.Strike();
            Check(run.Coal == 1 && run.Picks[0].Remaining == 78, "alloy breaks coal in two strikes");
            for (int missing = 0; missing < 4; missing++)
            {
                var data = ProgressData.Initial(s, 500); data.picks[0].id = "IRON"; data.coal = 30; data.iron = 10; data.gold = 5;
                if (missing == 0) data.money--; else if (missing == 1) data.coal--; else if (missing == 2) data.iron--; else data.gold--;
                run = new MiningSession(s, data); string before = JsonUtility.ToJson(run.Growth());
                Check(!run.UpgradePick(0) && before == JsonUtility.ToJson(run.Growth()), "missing material " + missing + " leaves entire growth intact");
            }
            var upgrade = ProgressData.Initial(s, 500); upgrade.picks[0].id = "IRON"; upgrade.coal = 30; upgrade.iron = 10; upgrade.gold = 5;
            run = new MiningSession(s, upgrade);
            Check(run.UpgradePick(0) && run.Picks[0].Name == "ALLOY" && run.Money == 0 && run.Coal == 0 && run.Iron == 0 && run.Gold == 0, "alloy recipe consumes exactly specified resources");
            Check(run.Picks[0].Power == 2.5f && run.Picks[0].Maximum == 80 && !run.UpgradePick(0) && run.UpgradeCost(0) == -1, "alloy is final upgrade stage");
            Check(ProgressData.TryParse(JsonUtility.ToJson(run.Growth()), out restored) && restored.version == 5 && restored.picks[0].power == 2.5f && new MiningSession(s, restored).Picks[0].Name == "ALLOY", "alloy save round trip");
            var legacy = ProgressData.Initial(s, 123); legacy.version = 4; legacy.picks[0].id = "IRON"; legacy.coal = 6; legacy.iron = 7; legacy.tutorial.controlsStep = 3; legacy.tutorial.menuStep = 1;
            Check(ProgressData.TryParse(JsonUtility.ToJson(legacy), out restored) && restored.version == 5 && restored.money == 123 && restored.coal == 6 && restored.iron == 7 && restored.gold == 0 && restored.tutorial.controlsStep == 3 && restored.tutorial.menuStep == 1, "version four preserves growth and tutorial checkpoints");
            legacy.version = 5; legacy.gold = -1;
            Check(!ProgressData.TryParse(JsonUtility.ToJson(legacy), out _), "negative gold rejected");
            legacy.gold = 5; legacy.discovered.Clear();
            Check(ProgressData.TryParse(JsonUtility.ToJson(legacy), out restored) && restored.discovered.Contains("Gold"), "owned gold proves discovery");
        }
        static void SpawnRules(GameSettings source)
        {
            var lines = source.spawnLines;
            Check(lines.Length == 3 && lines[0].firstRow == 2 && lines[0].lastRow == 10 && lines[0].RowCount == 9, "line one rows two to ten: nine rows");
            Check(lines[1].firstRow == 11 && lines[1].lastRow == 25 && lines[1].RowCount == 15, "line two: fifteen rows");
            Check(lines[2].firstRow == 26 && lines[2].lastRow == 45 && lines[2].RowCount == 20, "line three: twenty rows");
            Check(lines[0].coalPercent == 5 && lines[1].coalPercent == 5 && lines[1].ironPercent == 5 && lines[2].coalPercent == 1 && lines[2].ironPercent == 7 && lines[2].goldPercent == .8f, "approved candidate probabilities");
            var s = UnityEngine.Object.Instantiate(source);
            try
            {
                s.spawnLines = new[] { new SpawnLine { firstRow = 2, lastRow = 10, coalPercent = 100 }, new SpawnLine { firstRow = 11, lastRow = 25, ironPercent = 100 }, new SpawnLine { firstRow = 26, lastRow = 45, goldPercent = 100 } };
                var spawn = new OreSpawnManager(s, 24);
                Check(spawn.Kind(new Vector2Int(0, 0)) == Mineral.Stone && spawn.Kind(new Vector2Int(0, 9)) == Mineral.Coal && spawn.Kind(new Vector2Int(0, 10)) == Mineral.Iron, "first row and line one boundary");
                Check(spawn.Kind(new Vector2Int(0, 24)) == Mineral.Iron && spawn.Kind(new Vector2Int(0, 25)) == Mineral.Gold && spawn.Kind(new Vector2Int(0, 44)) == Mineral.Gold && spawn.Kind(new Vector2Int(0, 45)) == Mineral.Stone, "line three inclusive boundaries and unconfigured stone");
                var random = new System.Random(25); var count = new int[4];
                for (int i = 0; i < 30000; i++) count[(int)OreSpawnManager.Choose(true, true, true, random)]++;
                Check(count[0] == 0 && count.Skip(1).All(n => Math.Abs(n - 10000) < 400), "three simultaneous candidates choose uniformly");
                s.spawnLines = new[] { new SpawnLine { firstRow = 26, lastRow = 45, coalPercent = .00001f, ironPercent = .00001f, goldPercent = .00001f } };
                bool guaranteed = true; int goldBands = 0;
                for (int seed = 0; seed < 150; seed++)
                {
                    spawn = new OreSpawnManager(s, seed); int found = 0, gold = 0;
                    for (int y = 25; y < 45; y++) for (int x = 0; x < 5; x++) { var kind = spawn.Kind(new Vector2Int(x, y)); if (kind != Mineral.Stone) found++; if (kind == Mineral.Gold) gold++; }
                    guaranteed &= found >= 1; if (gold > 0) goldBands++;
                }
                Check(guaranteed && goldBands > 0 && goldBands < 150, "guarantee is any eligible ore, not gold in every band");
            }
            finally { UnityEngine.Object.DestroyImmediate(s); }
        }
        static void Ui(GameSettings settings)
        {
            var store = new ProgressStore(); GameObject instance = null;
            try
            {
                var initial = ProgressData.Initial(settings); initial.tutorial = TutorialProgress.Completed(); store.Reset(initial);
                instance = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/DigDeep/Prefabs/DigDeepGame.prefab"));
                var fresh = instance.GetComponent<GameManager>(); fresh.settings = settings; MiningFeatureChecks.Tick(fresh, "Awake");
                fresh.HandleAction("shop"); fresh.HandleAction("shop-sell"); MiningFeatureChecks.Tick(fresh.View, "LateUpdate");
                var hiddenGold = instance.GetComponentsInChildren<RectTransform>(true).First(t => t.name == "Gold");
                Check(!hiddenGold.gameObject.activeSelf && !fresh.Session.IsDiscovered(Mineral.Gold), "unseen gold has no visible sale row");
                fresh.HandleAction("sale-gold"); Check(fresh.SaleKind == Mineral.Stone, "direct action cannot select unseen gold");
                UnityEngine.Object.DestroyImmediate(instance); instance = null;
                var data = ProgressData.Initial(settings, 500); data.tutorial = TutorialProgress.Completed(); data.picks[0].id = "IRON"; data.coal = 30; data.iron = 10; data.gold = 5; store.Reset(data);
                instance = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/DigDeep/Prefabs/DigDeepGame.prefab"));
                var game = instance.GetComponent<GameManager>(); game.settings = settings; MiningFeatureChecks.Tick(game, "Awake");
                Check(game.alloy != null && game.goldOre != null && game.BlockSprite(Mineral.Gold, 25) == game.goldOre && game.PickSprite("ALLOY") == game.alloy, "new sprites connected in prefab");
                Check(game.Tutorial.Prompt == TutorialPrompt.None, "completed tutorials are preserved");
                var sizes = new[] { game.wood.rect.size, game.stonePick.rect.size, game.iron.rect.size, game.alloy.rect.size };
                Check(sizes.All(size => size == sizes[0]), "all pickaxes use identical sprite dimensions");
                game.HandleAction("upgrade"); game.HandleAction("upgrade-pick0"); MiningFeatureChecks.Capture(game, "40-alloy-recipe");
                var requirement = instance.GetComponentsInChildren<Text>(true).First(t => t.name == "Requirements");
                Check(requirement.text.Contains("합금 곡괭이") && requirement.text.Contains("금광석 5개") && requirement.text.Contains("철광석 10개") && requirement.text.Contains("석탄 30개"), "all alloy requirements visible");
                Check(game.View.ActionAt(new Vector2(240, 590)) == "upgrade-confirm", "alloy upgrade button enabled with full recipe");
                game.HandleAction("upgrade-confirm"); MiningFeatureChecks.Capture(game, "41-alloy-owned");
                Check(game.Session.Picks[0].Name == "ALLOY" && store.Load(settings).picks[0].id == "ALLOY" && store.Load(settings).gold == 0, "UI transaction persists alloy and deducted gold together");
                game.HandleAction("close"); game.HandleAction("shop"); game.HandleAction("shop-sell"); MiningFeatureChecks.Capture(game, "42-three-ore-sale");
                var menus = instance.GetComponent<GameMenus>();
                if (menus == null) menus = instance.GetComponentInChildren<GameMenus>(true);
                Check(menus != null, "trading component exists");
                game.View.MenuWheel(new Vector2(240, 400), -1000); MiningFeatureChecks.Capture(game, "43-gold-sale-scrolled");
                Check(game.View.ActionAt(new Vector2(240, 490)) == "sale-gold", "third ore is reachable by scrolling");
                game.HandleAction("sale-gold"); game.HandleAction("sell");
                Check(game.Session.Money == 0 && game.Session.Gold == 0 && game.SaleKind == Mineral.Gold, "sold-out discovery remains selectable but cannot sell");
                game.HandleAction("close"); game.HandleAction("inventory"); game.View.MenuWheel(new Vector2(240, 400), -1000); MiningFeatureChecks.Capture(game, "44-gold-inventory");
                game.HandleAction("close");
                // Reconnect a deterministic gold board to render the fractional health and reward.
                UnityEngine.Object.DestroyImmediate(instance); instance = null;
                settings.spawnLines = new[] { new SpawnLine { firstRow = 1, lastRow = 45, goldPercent = 100 } };
                data = AlloyData(settings); store.Reset(data);
                instance = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/DigDeep/Prefabs/DigDeepGame.prefab"));
                game = instance.GetComponent<GameManager>(); game.settings = settings; MiningFeatureChecks.Tick(game, "Awake");
                game.Play(); game.Session.Strike(); MiningFeatureChecks.Capture(game, "45-fractional-health");
                for (int hit = 1; hit < 6; hit++) game.Session.Strike(); MiningFeatureChecks.Capture(game, "46-gold-reward");
                var reward = instance.GetComponentsInChildren<Text>(true).FirstOrDefault(t => t.text == "금광석 +1");
                Check(reward != null && reward.color.r > .8f && reward.color.g > .6f && reward.color.b < .3f, "gold reward uses yellow text");
            }
            finally
            {
                if (instance != null) UnityEngine.Object.DestroyImmediate(instance);
                foreach (var key in new[] { "DigDeep.Progress.v1.Verification", "DigDeep.Progress.backup.v1.Verification", "DigDeep.Money.v1.Verification" }) PlayerPrefs.DeleteKey(key);
                PlayerPrefs.Save();
            }
        }
    }
}
