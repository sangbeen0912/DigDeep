using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
namespace DigDeep.Editor
{
    public static class EquipmentChecks
    {
        static int checks;
        static readonly BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        static void Check(bool value, string label) { checks++; if (!value) throw new Exception("Equipment verification: " + label); }
        static ProgressData Data(GameSettings s, params string[] names)
        {
            var d = ProgressData.Initial(s); d.tutorial = TutorialProgress.Completed();
            if (names.Length > 0) { d.picks.Clear(); foreach (var name in names) d.picks.Add(new PickProgress { id = name }); }
            return d;
        }
        static void Refresh(GameManager g) { MiningFeatureChecks.Tick(g.View, "LateUpdate"); Canvas.ForceUpdateCanvases(); }
        static GameObject Spawn(GameSettings s, ProgressData d)
        {
            new ProgressStore().Reset(d);
            var root = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/DigDeep/Prefabs/DigDeepGame.prefab"));
            var g = root.GetComponent<GameManager>(); g.settings = s; MiningFeatureChecks.Tick(g, "Awake");
            var helper = root.GetComponentInChildren<DigDeep.Presentation.PresentationResourcesButton>(); if (helper != null) MiningFeatureChecks.Tick(helper, "Awake");
            Refresh(g); return root;
        }
        static void Click(GameManager g, float x, float y)
        { var p = g.View.ScreenPoint(new Vector2(x, y)); g.Controls.Down(p); g.Controls.Up(p); Refresh(g); }
        static void Frame(GameManager g, float fraction)
        { typeof(GameManager).GetField("swingStarted", Private).SetValue(g, Time.unscaledTime - g.CurrentStrikeInterval * fraction); MiningFeatureChecks.Tick(g, "Update"); Refresh(g); }
        public static void Run()
        {
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "-digdeep-revision-test") < 0) throw new Exception("Isolated verification flag required");
            EditorSceneManager.OpenScene(DigDeepSetup.ScenePath); checks = 0;
            var s = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameSettings>("Assets/DigDeep/GameSettings.asset"));
            try { Recipes(s); Mining(s); Blasts(s); Interface(s); }
            finally
            {
                UnityEngine.Object.DestroyImmediate(s);
                foreach (var key in new[] { "DigDeep.Progress.v1.Verification", "DigDeep.Progress.backup.v1.Verification", "DigDeep.Money.v1.Verification" }) PlayerPrefs.DeleteKey(key);
                PlayerPrefs.Save();
            }
            File.WriteAllText("Logs/MiningUpgrade/equipment-checks.txt", "Equipment checks: " + checks + "\nIsolated editor verification.\n");
            Debug.Log("DIGDEEP_EQUIPMENT_CHECKS_PASSED " + checks);
        }
        static void Recipes(GameSettings s)
        {
            Check(s.Power("DRILL") == 3 && s.Durability("DRILL") == 60 && s.SpeedMultiplier("DRILL") == 3, "drill stats");
            Check(s.Power("TUNGSTEN") == 7 && s.Durability("TUNGSTEN") == 250 && s.SpeedMultiplier("TUNGSTEN") == 1.5f, "tungsten stats");
            Check(s.Power("CRUSHER") == 20 && s.Durability("CRUSHER") == 20 && s.SpeedMultiplier("CRUSHER") == .33f && s.ExtraDrops("CRUSHER") == 1, "crusher stats and extra drop");
            Check(s.Power("DYNAMITE") == 500 && s.Durability("DYNAMITE") == 5 && s.equipmentCost == 5000 && s.equipmentEnergyTenths == 1000, "dynamite and shared branch costs");
            foreach (var id in GameSettings.EquipmentChoices)
            {
                var d = Data(s, "SUPERALLOY"); d.money = 5000; d.energyTenths = 1000; d.iron = 1; d.diamond = 2;
                var run = new MiningSession(s, d); int saved = 0; run.GrowthChanged += () => saved++;
                Check(run.CanUpgrade(0, id) && run.UpgradePick(0, id) && run.Money == 0 && run.EnergyTenths == 0 && run.Iron == 1 && run.Diamond == 2 && saved == 1, id + " atomic shared recipe");
                Check(run.Picks[0].Name == id && run.Picks[0].Maximum == s.Durability(id) && run.Picks[0].Power == s.Power(id), id + " equipment creation");
                Check(ProgressData.TryParse(JsonUtility.ToJson(run.Growth()), out var parsed) && new MiningSession(s, parsed).Picks[0].Name == id, id + " save round trip");
                for (int missing = 0; missing < 2; missing++)
                {
                    d = Data(s, "SUPERALLOY"); d.money = missing == 0 ? 4999 : 5000; d.energyTenths = missing == 1 ? 999 : 1000;
                    run = new MiningSession(s, d); string before = JsonUtility.ToJson(run.Growth());
                    Check(!run.UpgradePick(0, id) && before == JsonUtility.ToJson(run.Growth()), id + " shortage " + missing + " preserves all resources");
                }
                d = Data(s, "SUPERALLOY", id); d.money = 99999; d.energyTenths = 9999; run = new MiningSession(s, d);
                Check(!run.CanUpgrade(0, id) && !run.UpgradePick(0, id), id + " duplicate family is blocked");
                d = Data(s, id, id); Check(!ProgressData.TryParse(JsonUtility.ToJson(d), out _), id + " duplicate family save is rejected");
                d = Data(s, "WOOD"); d.money = 5000; d.energyTenths = 1000; run = new MiningSession(s, d); Check(!run.UpgradePick(0, id), id + " requires superalloy");
            }
            var all = Data(s, "SUPERALLOY", "SUPERALLOY", "SUPERALLOY", "SUPERALLOY"); all.money = 20000; all.energyTenths = 4000;
            var combined = new MiningSession(s, all);
            for (int i = 0; i < 4; i++) Check(combined.UpgradePick(i, GameSettings.EquipmentChoices[i]), "all four unique equipment fit slot " + i);
            Check(ProgressData.TryParse(JsonUtility.ToJson(combined.Growth()), out _) && combined.Money == 0 && combined.EnergyTenths == 0, "four branch save and totals");
            for (int version = 1; version <= 7; version++) { var old = ProgressData.Initial(s, 123); old.version = version; old.bestDepth = 42; Check(ProgressData.TryParse(JsonUtility.ToJson(old), out var parsed) && parsed.money == 123 && parsed.bestDepth == 42 && parsed.picks[0].id == "WOOD", "old save " + version + " remains compatible"); }
        }
        static void Mining(GameSettings s)
        {
            foreach (var kind in new[] { Mineral.Coal, Mineral.Iron, Mineral.Gold, Mineral.Diamond, Mineral.Oil })
            {
                var line = new SpawnLine { firstRow = 1, lastRow = 1 };
                if (kind == Mineral.Coal) line.coalPercent = 100; else if (kind == Mineral.Iron) line.ironPercent = 100; else if (kind == Mineral.Gold) line.goldPercent = 100; else if (kind == Mineral.Diamond) line.diamondPercent = 100; else line.oilPercent = 100;
                s.spawnLines = new[] { line }; var run = new MiningSession(s, Data(s, "CRUSHER")); run.Start(); int hits = Mathf.CeilToInt(s.OreHealth(kind) / 20f);
                for (int hit = 1; hit < hits; hit++) { run.Strike(); Check(run.Stock(kind) == 0, kind + " no partial-hit reward"); }
                run.Strike(); Check(run.Stock(kind) == 2 && run.LastRewardAmount == 2 && run.Picks[0].Remaining == 20 - hits && run.IsDiscovered(kind), kind + " crusher awards two once and spends durability per strike");
                run.Regroup(); Check(run.Stock(kind) == 2 && run.Picks[0].Remaining == 20, kind + " regroup keeps drops and restores durability");
            }
            s.crusherExtraDrops = 3; s.spawnLines = new[] { new SpawnLine { firstRow = 1, lastRow = 1, coalPercent = 100 } };
            var future = new MiningSession(s, Data(s, "CRUSHER")); future.Start(); future.Strike(); Check(future.Coal == 4, "future extra-drop value is data driven"); s.crusherExtraDrops = 1;
            s.spawnLines = new SpawnLine[0];
            foreach (var id in new[] { "DRILL", "TUNGSTEN", "CRUSHER" })
            {
                var run = new MiningSession(s, Data(s, id)); run.Start(); run.Strike(); Check(run.Money == 1 && run.Picks[0].Remaining == s.Durability(id) - 1, id + " ordinary stone pays one money");
                Check(Mathf.Abs(s.StrikeInterval(id) - .34f / s.SpeedMultiplier(id)) < .00001f, id + " scaled strike interval");
            }
        }
        static void Blasts(GameSettings s)
        {
            s.spawnLines = new SpawnLine[0]; var run = new MiningSession(s, Data(s, "DYNAMITE")); run.Start(); int saves = 0; run.GrowthChanged += () => saves++;
            Check(!run.Strike() && !run.Blast(new Vector2Int(-1, 2)) && !run.Blast(new Vector2Int(2, -1)) && run.Picks[0].Remaining == 5, "invalid and automatic dynamite attacks consume nothing");
            Check(run.Blast(new Vector2Int(2, 4)) && run.Money == 9 && run.Picks[0].Remaining == 4 && saves == 1, "remote nine-block blast consumes one durability and saves once");
            Check(run.Position == new Vector2Int(2, -1) && run.Deepest == 0 && run.BestDepth == 0, "remote destruction cannot move equipment or inflate depth");
            Check(!run.Blast(new Vector2Int(2, 4)) && run.Picks[0].Remaining == 4, "empty target cancels without consuming durability");
            Check(run.Blast(new Vector2Int(0, 0)) && run.Money == 13 && run.Picks[0].Remaining == 3, "edge blast clips to four in-bounds blocks");
            run.Blast(new Vector2Int(2, 10)); run.Blast(new Vector2Int(2, 20)); run.Blast(new Vector2Int(2, 30));
            Check(run.Picks[0].Remaining == 0 && run.State == SessionState.GameOver && !run.Blast(new Vector2Int(2, 40)), "five uses exhaust one-only equipment and end run");
            run.Restart(); Check(run.Picks[0].Remaining == 5 && run.Health(new Vector2Int(2, 4)) == 1, "restart restores five uses and fresh terrain");
            run = new MiningSession(s, Data(s, "DYNAMITE", "DRILL")); run.Start(); for (int i = 0; i < 5; i++) run.Blast(new Vector2Int(2, i * 4));
            Check(run.State == SessionState.Playing && !run.CanMine && run.Select(1) && run.CanMine, "exhausted dynamite requires manual selection of available equipment");
            run = new MiningSession(s, Data(s, "DYNAMITE", "DRILL")); run.Start(); run.Blast(new Vector2Int(2, 0)); run.Select(1);
            Check(run.Drop(new Vector2Int(2, 1)) && run.Deepest == 2 && run.BestDepth == 2, "travelling through a blasted passage records actual position");
            s.blockHealth = 600; run = new MiningSession(s, Data(s, "DYNAMITE")); run.Start(); run.Blast(new Vector2Int(2, 4));
            Check(run.Health(new Vector2Int(2, 4)) == 100 && run.Money == 0 && run.Picks[0].Remaining == 4, "500 damage does not prematurely award future high-health blocks"); s.blockHealth = 1;
        }
        static void Interface(GameSettings s)
        {
            GameObject root = null;
            try
            {
                root = Spawn(s, Data(s, "SUPERALLOY", "SUPERALLOY", "SUPERALLOY", "SUPERALLOY")); var g = root.GetComponent<GameManager>();
                Click(g, 100, 140); Check(g.Session.Money == 100000 && g.Session.EnergyTenths == 5000 && g.Session.Picks.All(p => p.Name == "SUPERALLOY"), "test button gives resources and no equipment");
                Check(root.GetComponentsInChildren<Text>().Any(t => t.text == "시험용 재화 지급"), "button uses test wording");
                Click(g, 420, 140); Click(g, 140, 400); Check(g.Modal == GameModal.EquipmentChoice, "superalloy opens four-way branch UI");
                foreach (var text in g.View.UiRoot.Find("Trading and confirmation/Equipment branch selection").GetComponentsInChildren<Text>()) if (text.gameObject.activeInHierarchy) Check(text.preferredHeight <= text.rectTransform.rect.height + 1, "branch text fits " + text.name);
                MiningFeatureChecks.Capture(g, "100-equipment-four-branches");
                for (int i = 0; i < 4; i++)
                {
                    if (i > 0) Click(g, i % 2 == 0 ? 140 : 335, i < 2 ? 400 : 530);
                    Click(g, i % 2 == 0 ? 140 : 335, i < 2 ? 380 : 550);
                    Check(g.Modal == GameModal.UpgradeConfirm && g.UpgradeTarget == GameSettings.EquipmentChoices[i], "branch " + i + " selection through common input");
                    MiningFeatureChecks.Capture(g, "101-equipment-recipe-" + i); Click(g, 240, 590);
                    Check(g.Session.Picks[i].Name == GameSettings.EquipmentChoices[i], "branch " + i + " confirmation saves equipment");
                }
                MiningFeatureChecks.Capture(g, "102-all-equipment-owned");
                foreach (var asset in new[] { "Drill", "TungstenPick", "Crusher", "Dynamite" })
                { var importer = (TextureImporter)AssetImporter.GetAtPath("Assets/DigDeep/Art/" + asset + ".png"); Check(importer.maxTextureSize == 128 && !importer.mipmapEnabled && !importer.isReadable, asset + " bounded texture memory"); }
                Click(g, 240, 702); g.Play(); g.Hold(); Frame(g, .1f);
                var image = g.View.UiRoot.Find("Mining viewport/Scrolling terrain/Active pickaxe").GetComponent<Image>(); Vector2 before = image.rectTransform.anchoredPosition;
                Frame(g, .5f); Check(Mathf.Abs(Mathf.DeltaAngle(image.rectTransform.eulerAngles.z, 0)) < .01f && image.rectTransform.anchoredPosition != before, "drill translates vertically without rotating");
                MiningFeatureChecks.Capture(g, "103-drill-vertical-mining"); g.ReleaseHold(); int remaining = g.Session.Picks[0].Remaining; Frame(g, 2); Check(g.Session.Picks[0].Remaining == remaining, "drill stops immediately on release");
                g.SelectPick(1); g.Tap(); Frame(g, .54f); MiningFeatureChecks.Capture(g, "104-tungsten-mining");
                g.StopMining(); g.SelectPick(3); var position = g.Session.Position; int depth = g.Session.BestDepth; int objects = root.GetComponentsInChildren<Transform>(true).Length;
                var target = new Vector2Int(4, 3); var point = g.View.CellBounds(target).center; Click(g, point.x, point.y); Check(g.IsSwinging, "visible distant block starts an aimed blast"); Frame(g, .54f);
                Check(g.Session.Picks[3].Remaining == 4 && g.Session.Position == position && g.Session.BestDepth == depth, "aimed blast costs once without movement or depth gain");
                Check(g.View.UiRoot.Find("Mining viewport/Scrolling terrain/Reusable explosion").gameObject.activeSelf, "comic BOOM explosion appears");
                Check(objects == root.GetComponentsInChildren<Transform>(true).Length, "blast reuses all effects and UI objects"); MiningFeatureChecks.Capture(g, "105-dynamite-boom");
                Frame(g, 1.01f); g.AimBlast(new Vector2Int(2, 10000)); Check(!g.IsSwinging && g.Session.Picks[3].Remaining == 4, "invisible target cannot consume an attack");
                UnityEngine.Object.DestroyImmediate(root); root = null;
                s.spawnLines = new[] { new SpawnLine { firstRow = 1, lastRow = 1, coalPercent = 100 } };
                root = Spawn(s, Data(s, "CRUSHER")); g = root.GetComponent<GameManager>(); g.Play(); g.Tap(); Frame(g, .54f);
                Check(root.GetComponentsInChildren<Text>().Any(t => t.text == "석탄 +2") && g.Session.Coal == 2, "crusher floating reward and inventory match"); MiningFeatureChecks.Capture(g, "106-crusher-extra-drop");
                UnityEngine.Object.DestroyImmediate(root); root = null; s.spawnLines = new SpawnLine[0];
                root = Spawn(s, Data(s)); g = root.GetComponent<GameManager>(); Click(g, 100, 140);
                for (int i = 0; i < 3; i++) Check(g.Session.Buy(), "test resources buy slot " + i);
                for (int i = 0; i < 4; i++) { for (int tier = 0; tier < 5; tier++) Check(g.Session.UpgradePick(i), "test resources normal upgrade " + i + "/" + tier); Check(g.Session.UpgradePick(i, GameSettings.EquipmentChoices[i]), "test resources create branch " + i); }
                Check(g.Session.Picks.Select(p => p.Name).SequenceEqual(GameSettings.EquipmentChoices), "one test grant funds all four equipment through real transactions");
            }
            finally { if (root != null) UnityEngine.Object.DestroyImmediate(root); }
        }
    }
}
