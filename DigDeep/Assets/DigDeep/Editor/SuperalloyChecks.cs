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
    public static class SuperalloyChecks
    {
        static int checks;
        static readonly BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        static void Check(bool value, string label) { checks++; if (!value) throw new Exception("Superalloy verification: " + label); }
        static ProgressData Data(GameSettings s, string id = "DIAMOND")
        { var d = ProgressData.Initial(s); d.picks[0].id = id; d.tutorial = TutorialProgress.Completed(); return d; }
        static void Refresh(GameManager g) { MiningFeatureChecks.Tick(g.View, "LateUpdate"); Canvas.ForceUpdateCanvases(); }
        static GameObject Spawn(GameSettings s, ProgressData data)
        {
            new ProgressStore().Reset(data);
            var root = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/DigDeep/Prefabs/DigDeepGame.prefab"));
            var g = root.GetComponent<GameManager>(); g.settings = s; MiningFeatureChecks.Tick(g, "Awake"); Refresh(g); return root;
        }
        static void Click(GameManager g, float x, float y)
        { var p = g.View.ScreenPoint(new Vector2(x, y)); g.Controls.Down(p); g.Controls.Up(p); Refresh(g); }
        static void Elapse(GameManager g, float fraction)
        {
            typeof(GameManager).GetField("swingStarted", Private).SetValue(g, Time.unscaledTime - g.CurrentStrikeInterval * fraction);
            MiningFeatureChecks.Tick(g, "Update");
        }
        public static void Run()
        {
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "-digdeep-revision-test") < 0) throw new Exception("Isolated verification flag required");
            EditorSceneManager.OpenScene(DigDeepSetup.ScenePath); checks = 0;
            var s = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameSettings>("Assets/DigDeep/GameSettings.asset"));
            try { Rules(s); Speed(s); Interface(s); }
            finally
            {
                UnityEngine.Object.DestroyImmediate(s);
                foreach (var key in new[] { "DigDeep.Progress.v1.Verification", "DigDeep.Progress.backup.v1.Verification", "DigDeep.Money.v1.Verification" }) PlayerPrefs.DeleteKey(key);
                PlayerPrefs.Save();
            }
            File.WriteAllText("Logs/MiningUpgrade/superalloy-checks.txt", "Superalloy/speed checks: " + checks + "\nIsolated editor verification.\n");
            Debug.Log("DIGDEEP_SUPERALLOY_CHECKS_PASSED " + checks);
        }
        static void Rules(GameSettings s)
        {
            Check(s.diamondUpgradeCost == 1000 && s.diamondUpgradeOre == 20 && s.diamondUpgradeEnergyTenths == 50, "diamond recipe changes only money to 1000");
            Check(s.Power("SUPERALLOY") == 5 && s.Durability("SUPERALLOY") == 150 && GameSettings.Tier("SUPERALLOY") == 5, "superalloy has power five and durability 150");
            Check(GameSettings.NextPick("DIAMOND") == "SUPERALLOY" && GameSettings.NextPick("SUPERALLOY") == null, "superalloy requires an explicit branch choice");
            var d = Data(s); d.money = 2000; d.energyTenths = 100; d.diamond = 10; d.iron = 50;
            var run = new MiningSession(s, d); int saves = 0; run.GrowthChanged += () => saves++;
            Check(run.UpgradeCost(0) == 2000 && run.UpgradeOre(0) == 50 && run.UpgradeDiamond(0) == 10 && run.UpgradeEnergy(0) == 100 && run.UpgradeCoal(0) == 0 && run.UpgradeGold(0) == 0, "all four superalloy costs are exact");
            Check(run.UpgradePick(0) && run.Money == 0 && run.Iron == 0 && run.Diamond == 0 && run.EnergyTenths == 0 && saves == 1, "one atomic upgrade consumes all four resources once");
            Check(run.Picks[0].Name == "SUPERALLOY" && run.Picks[0].Power == 5 && run.Picks[0].Remaining == 150 && run.UpgradeCost(0) == -1 && !run.UpgradePick(0), "superalloy has correct stats and requires a branch choice for further upgrading");
            for (int missing = 0; missing < 4; missing++)
            {
                d = Data(s); d.money = missing == 0 ? 1999 : 2000; d.iron = missing == 1 ? 49 : 50; d.diamond = missing == 2 ? 9 : 10; d.energyTenths = missing == 3 ? 99 : 100;
                run = new MiningSession(s, d); string before = JsonUtility.ToJson(run.Growth());
                Check(!run.UpgradePick(0) && before == JsonUtility.ToJson(run.Growth()), "resource shortage " + missing + " leaves all data untouched");
            }
            d = Data(s); d.money = 2007; d.iron = 51; d.diamond = 12; d.energyTenths = 103; d.coal = 9; d.gold = 8; d.oil = 7; d.bestDepth = 61;
            run = new MiningSession(s, d); run.Start(); Check(!run.UpgradePick(0), "no upgrading while mining"); run.Regroup();
            Check(run.UpgradePick(0) && run.Money == 7 && run.Iron == 1 && run.Diamond == 2 && run.EnergyTenths == 3 && run.Coal == 9 && run.Gold == 8 && run.Oil == 7, "surplus resources and unrelated minerals are preserved");
            Check(ProgressData.TryParse(JsonUtility.ToJson(run.Growth()), out var saved), "superalloy is accepted by save validation");
            var restored = new MiningSession(s, saved);
            Check(restored.Picks[0].Name == "SUPERALLOY" && restored.Picks[0].Remaining == 150 && restored.BestDepth == 61 && restored.Tutorial.menuStep == 3 && restored.EnergyTenths == 3, "superalloy reconnect preserves growth and prepares full durability");
            s.spawnLines = new[] { new SpawnLine { firstRow = 1, lastRow = 1, diamondPercent = 100 } };
            run = new MiningSession(s, Data(s, "SUPERALLOY"), 1); run.Start();
            for (int i = 0; i < 5; i++) run.Strike();
            Check(run.Health(new Vector2Int(2, 0)) == 5 && run.Diamond == 0 && run.Picks[0].Remaining == 145, "five high-power hits consume five durability without premature reward");
            run.Strike(); Check(run.Diamond == 1 && run.Picks[0].Remaining == 144, "six hits break 30 HP ore and consume six durability");
            Check(s.ConversionRate(Mineral.Oil) == 10 && s.ConversionRate(Mineral.Coal) == 1, "oil converts to one energy; coal remains one tenth");
            d = Data(s); d.oil = 3; d.coal = 2; run = new MiningSession(s, d);
            Check(run.Convert(Mineral.Oil, 2) && run.Oil == 1 && run.EnergyTenths == 20 && run.Coal == 2, "oil batch conversion uses integer tenths");
            Check(run.Convert(Mineral.Coal, 1) && run.EnergyTenths == 21 && run.Coal == 1, "mixed conversion preserves fractional energy");
            string snapshot = JsonUtility.ToJson(run.Growth());
            Check(!run.Convert(Mineral.Oil, 2) && snapshot == JsonUtility.ToJson(run.Growth()), "insufficient oil cannot partially convert");
            d.energyTenths = int.MaxValue - 9; run = new MiningSession(s, d);
            Check(run.MaximumConvertible(Mineral.Oil) == 0 && !run.Convert(Mineral.Oil, 1) && run.Oil == 3, "oil conversion checks overflow before spending stock");
            s.spawnLines = new SpawnLine[0];
        }
        static void Speed(GameSettings s)
        {
            string[] names = { "WOOD", "STONE", "IRON", "ALLOY", "DIAMOND", "SUPERALLOY" };
            float[] factors = { 1, 1, 1.1f, 1.2f, 1.3f, 1.5f };
            Check(s.strikeInterval == .34f && s.holdDelay == .28f, "base swing and hold recognition remain unchanged");
            for (int i = 0; i < names.Length; i++)
            {
                string id = names[i]; GameObject root = null;
                try
                {
                    Check(Mathf.Abs(s.SpeedMultiplier(id) - factors[i]) < .00001f && Mathf.Abs(s.StrikeInterval(id) - .34f / factors[i]) < .00001f, id + " speed uses division by its multiplier");
                    root = Spawn(s, Data(s, id)); var g = root.GetComponent<GameManager>(); g.Play(); g.Hold(); int before = g.Session.Picks[0].Remaining;
                    Elapse(g, .49f); Check(g.Session.Picks[0].Remaining == before && Mathf.Abs(g.SwingProgress - .49f) < .02f, id + " animation uses its own duration before impact");
                    Elapse(g, .54f); Check(g.Session.Picks[0].Remaining == before - 1, id + " impact uses scaled timing and one durability");
                    Elapse(g, .85f); Check(g.Session.Picks[0].Remaining == before - 1, id + " swing cannot deal duplicate damage");
                    Elapse(g, 1.01f); Check(g.IsSwinging && g.SwingProgress < .1f, id + " hold starts the next swing at its scaled interval");
                    Elapse(g, .54f); Check(g.Session.Picks[0].Remaining == before - 2, id + " second hold swing damages once");
                    g.ReleaseHold(); Elapse(g, 2); Check(!g.IsSwinging && g.Session.Picks[0].Remaining == before - 2, id + " release immediately stops continuous damage");
                    g.Tap(); g.Tap(); Elapse(g, .54f); Check(g.Session.Picks[0].Remaining == before - 3, id + " taps share the same single active swing");
                    Elapse(g, 1.01f); Check(!g.IsSwinging, id + " tap does not become automatic mining");
                }
                finally { if (root != null) UnityEngine.Object.DestroyImmediate(root); }
            }
            GameObject multi = null;
            try
            {
                var d = Data(s, "WOOD"); d.picks.Add(new PickProgress { id = "SUPERALLOY" }); multi = Spawn(s, d); var g = multi.GetComponent<GameManager>(); g.Play(); g.Hold();
                g.SelectPick(1); Check(!g.IsSwinging && Mathf.Abs(g.CurrentStrikeInterval - .34f / 1.5f) < .00001f, "manual swap cancels old swing and selects new speed");
                g.Tap(); Elapse(g, .54f); Check(g.Session.Picks[0].Remaining == 20 && g.Session.Picks[1].Remaining == 149, "manual swap cannot charge the previous tool");
            }
            finally { if (multi != null) UnityEngine.Object.DestroyImmediate(multi); }
        }
        static void Interface(GameSettings s)
        {
            GameObject root = null;
            try
            {
                var d = Data(s); d.money = 2000; d.iron = 50; d.diamond = 10; d.energyTenths = 100; d.oil = 2;
                root = Spawn(s, d); var g = root.GetComponent<GameManager>();
                Check(g.superalloyPick != null && g.PickSprite("SUPERALLOY") == g.superalloyPick && GameMenus.PickName("SUPERALLOY") == "초합금 곡괭이", "sprite and Korean name connect to real prefab");
                Check(Mathf.Abs(g.superalloyPick.rect.width / g.superalloyPick.rect.height - g.stonePick.rect.width / g.stonePick.rect.height) < .01f, "new sprite retains the existing pick proportions");
                var importer = (TextureImporter)AssetImporter.GetAtPath("Assets/DigDeep/Art/SuperalloyPick.png");
                Check(importer.maxTextureSize == 512 && !importer.isReadable && !importer.mipmapEnabled && importer.filterMode == FilterMode.Point, "new art has bounded runtime texture memory");
                Click(g, 420, 143); Click(g, 140, 400);
                var table = g.View.UiRoot.Find("Trading and confirmation/Confirmation content/Readable requirements");
                var rows = table.GetComponentsInChildren<Image>().Where(x => x.name.StartsWith("Requirement row")).ToArray();
                Check(rows.Length == 4 && g.View.ActionAt(new Vector2(240, 590)) == "upgrade-confirm", "all four superalloy requirements fit and confirmation enables");
                foreach (var row in rows) foreach (var text in row.GetComponentsInChildren<Text>())
                    Check(text.preferredHeight <= text.rectTransform.rect.height + .5f, "superalloy requirement text fits " + row.name + "/" + text.name);
                var speed = table.Find("Upgrade speed").GetComponent<Text>();
                Check(speed.text.Contains("1.3") && speed.text.Contains("1.5") && speed.preferredHeight <= 28, "speed change is visible below the confirm button");
                MiningFeatureChecks.Capture(g, "90-superalloy-requirements"); Click(g, 240, 590);
                Check(g.Session.Picks[0].Name == "SUPERALLOY" && new ProgressStore().Load(s).picks[0].id == "SUPERALLOY", "pointer upgrade saves new pick and deductions");
                MiningFeatureChecks.Capture(g, "91-superalloy-upgrade-list"); Click(g, 140, 400);
                Check(g.View.ActionAt(new Vector2(240, 590)) == "blocked" && !root.GetComponentsInChildren<Text>().Any(t => t.name == "Label" && t.text == "강화" && t.transform.parent.name == "confirm"), "branch selection does not expose the previous confirmation action");
                MiningFeatureChecks.Capture(g, "92-superalloy-branches"); Click(g, 240, 702); Click(g, 335, 316); Click(g, 240, 530);
                Check(g.ConversionKind == Mineral.Oil, "oil can be selected through shared touch input");
                var oilLabel = g.View.UiRoot.Find("Trading and confirmation/Refinery content/Mineral conversion/Convertible mineral list/Scrollable content/Oil/Ore information").GetComponent<Text>();
                Check(oilLabel.text.Contains("1.0") && oilLabel.preferredHeight <= oilLabel.rectTransform.rect.height, "oil one-energy ratio is readable");
                var oilCard = (RectTransform)oilLabel.transform.parent; var content = (RectTransform)oilCard.parent;
                float rowTop = -oilCard.anchoredPosition.y - content.anchoredPosition.y;
                Check(rowTop >= 0 && rowTop + oilCard.rect.height <= 104, "selected oil row is fully revealed in the conversion viewport");
                MiningFeatureChecks.Capture(g, "93-oil-energy-conversion"); Click(g, 240, 657);
                Check(g.Session.Oil == 1 && g.Session.EnergyTenths == 10 && new ProgressStore().Load(s).energyTenths == 10, "oil touch conversion saves the awarded one energy");
                Click(g, 240, 702); g.Play(); Refresh(g); MiningFeatureChecks.Capture(g, "94-superalloy-in-game");
                Check(root.GetComponentsInChildren<Image>().Any(x => x.sprite == g.superalloyPick), "superalloy image appears in active play");
            }
            finally { if (root != null) UnityEngine.Object.DestroyImmediate(root); }
        }
    }
}
