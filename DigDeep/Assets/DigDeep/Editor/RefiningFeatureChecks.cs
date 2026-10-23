using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
namespace DigDeep.Editor
{
    public static class RefiningFeatureChecks
    {
        static int checks;
        static void Check(bool value, string label) { checks++; if (!value) throw new Exception("Refinery verification: " + label); }
        static void Refresh(GameManager game) => MiningFeatureChecks.Tick(game.View, "LateUpdate");
        static ProgressData Data(GameSettings s, string pick = "WOOD") { var d = ProgressData.Initial(s); d.picks[0].id = pick; d.tutorial = TutorialProgress.Completed(); return d; }
        public static void Run()
        {
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "-digdeep-revision-test") < 0) throw new Exception("Isolated save flag required");
            checks = 0;
            EditorSceneManager.OpenScene(DigDeepSetup.ScenePath);
            var source = AssetDatabase.LoadAssetAtPath<GameSettings>("Assets/DigDeep/GameSettings.asset");
            var settings = UnityEngine.Object.Instantiate(source);
            try { Spawn(source); Rules(settings); ConversionRules(settings); UpgradeRules(settings); Movement(settings); Interface(settings); }
            finally
            {
                UnityEngine.Object.DestroyImmediate(settings);
                foreach (var key in new[] { "DigDeep.Progress.v1.Verification", "DigDeep.Progress.backup.v1.Verification", "DigDeep.Money.v1.Verification" }) PlayerPrefs.DeleteKey(key);
                PlayerPrefs.Save();
            }
            File.WriteAllText("Logs/MiningUpgrade/refining-checks.txt", "Existing checks: 229\nRefinery checks: " + checks + "\nNo player build or deployment.\n");
            Debug.Log("DIGDEEP_REFINING_FEATURES_PASSED " + checks + " NO_BUILD");
        }
        static void Spawn(GameSettings source)
        {
            var line = source.spawnLines[3];
            Check(source.spawnLines.Length == 4 && line.firstRow == 46 && line.lastRow == 65 && line.RowCount == 20, "line four covers rows 46 through 65, twenty rows");
            Check(line.coalPercent == 1 && line.ironPercent == 10 && line.goldPercent == 2 && line.diamondPercent == .5f && line.oilPercent == .1f, "line four probabilities match all five minerals");
            var s = UnityEngine.Object.Instantiate(source);
            try
            {
                s.spawnLines = new[] { new SpawnLine { firstRow = 46, lastRow = 65, diamondPercent = 100 } };
                var map = new OreSpawnManager(s, 1);
                Check(map.Kind(new Vector2Int(2, 44)) == Mineral.Stone && map.Kind(new Vector2Int(2, 45)) == Mineral.Diamond && map.Kind(new Vector2Int(2, 64)) == Mineral.Diamond && map.Kind(new Vector2Int(2, 65)) == Mineral.Stone, "new line boundaries are inclusive and depth after 65 is ordinary stone");
                s.spawnLines[0].diamondPercent = .000001f; s.spawnLines[0].oilPercent = .000001f;
                bool guaranteed = true, notBothRequired = false;
                for (int seed = 1; seed <= 100; seed++)
                {
                    map = new OreSpawnManager(s, seed); int diamond = 0, oil = 0;
                    for (int row = 45; row <= 64; row++) for (int x = 0; x < 5; x++) { var kind = map.Kind(new Vector2Int(x, row)); if (kind == Mineral.Diamond) diamond++; if (kind == Mineral.Oil) oil++; }
                    guaranteed &= diamond + oil > 0; notBothRequired |= diamond == 0 || oil == 0;
                }
                Check(guaranteed && notBothRequired, "one mineral per entire band is guaranteed, not one of each rare mineral");
                var rng = new System.Random(93); var counts = new int[6];
                for (int i = 0; i < 5000; i++) counts[(int)OreSpawnManager.Choose(true, true, true, true, true, rng)]++;
                Check(counts[0] == 0 && counts.Skip(1).All(n => n > 700 && n < 1300), "all five collided candidates can win uniformly");
                Check(OreSpawnManager.Choose(false, false, false, false, true, rng) == Mineral.Oil && OreSpawnManager.Choose(false, false, false, true, false, rng) == Mineral.Diamond, "single rare candidates are retained");
                map = new OreSpawnManager(source, 37); var cell = new Vector2Int(3, 49); var first = map.Kind(cell); for (int i = 0; i < 30; i++) map.Kind(new Vector2Int(i % 5, i));
                Check(map.Kind(cell) == first, "viewing other bands never rerolls a rare mineral");
            }
            finally { UnityEngine.Object.DestroyImmediate(s); }
        }
        static void Rules(GameSettings s)
        {
            Check(s.OreHealth(Mineral.Diamond) == 30 && s.OreHealth(Mineral.Oil) == 30 && s.SellPrice(Mineral.Diamond) == 37 && s.SellPrice(Mineral.Oil) == 60 && s.SellPrice(Mineral.Gold) == 40, "diamond floors 125 percent and oil and gold retain fixed prices");
            foreach (var kind in new[] { Mineral.Diamond, Mineral.Oil })
            {
                s.spawnLines = new[] { new SpawnLine { firstRow = 1, lastRow = 1, diamondPercent = kind == Mineral.Diamond ? 100 : 0, oilPercent = kind == Mineral.Oil ? 100 : 0 } };
                var run = new MiningSession(s, Data(s, "DIAMOND"), 2); run.Start(); for (int hit = 0; hit < 9; hit++) run.Strike();
                Check(run.Stock(kind) == 0 && !run.IsDiscovered(kind) && run.Health(new Vector2Int(2, 0)) == 3, kind + " partially damaged ore has no premature reward");
                run.Strike(); Check(run.Stock(kind) == 1 && run.Picks[0].Remaining == 100 && run.IsDiscovered(kind) && run.Money == 0, kind + " ten hits award stock and cost ten durability");
                run.Regroup(); int price = s.SellPrice(kind);
                Check(run.Sell(kind, 1) && run.Money == price && run.Stock(kind) == 0 && run.IsDiscovered(kind), kind + " sale keeps discovery after selling out");
                Check(ProgressData.TryParse(JsonUtility.ToJson(run.Growth()), out var restored) && new MiningSession(s, restored).IsDiscovered(kind), kind + " zero-stock discovery survives reconnect");
            }
            const string old = "{\"version\":6,\"money\":431,\"coal\":9,\"iron\":8,\"gold\":7,\"bestDepth\":45,\"tutorial\":{\"controlsStep\":4,\"menuStep\":2},\"picks\":[{\"id\":\"ALLOY\"}]}";
            Check(ProgressData.TryParse(old, out var migrated) && migrated.version == 7 && migrated.money == 431 && migrated.bestDepth == 45 && migrated.picks[0].id == "ALLOY" && migrated.tutorial.menuStep == 2 && migrated.energyTenths == 0 && migrated.diamond == 0 && migrated.oil == 0, "version six migration preserves existing growth and tutorial");
            var data = Data(s, "DIAMOND"); data.diamond = 20; data.oil = 7; data.energyTenths = 13;
            Check(ProgressData.TryParse(JsonUtility.ToJson(data), out migrated) && new MiningSession(s, migrated).Picks[0].Maximum == 110 && migrated.energyTenths == 13 && migrated.diamond == 20 && migrated.oil == 7, "new resources and diamond tool round-trip together");
            foreach (var bad in new[] { "diamond", "oil", "energyTenths" })
                Check(!ProgressData.TryParse(JsonUtility.ToJson(data).Replace("\"" + bad + "\":" + (bad == "diamond" ? 20 : bad == "oil" ? 7 : 13), "\"" + bad + "\":-1"), out _), "negative " + bad + " is rejected");
            s.spawnLines = new SpawnLine[0];
        }
        static void ConversionRules(GameSettings s)
        {
            s.conversionRecipes = new[] { new ConversionRecipe { mineral = Mineral.Coal, energyTenthsPerOre = 1 } };
            var data = Data(s); data.coal = 60; var run = new MiningSession(s, data);
            for (int i = 0; i < 10; i++) Check(run.Convert(Mineral.Coal, 1), "single coal conversion " + i);
            Check(run.Coal == 50 && run.EnergyTenths == 10 && GameSettings.FormatEnergy(run.EnergyTenths) == "1.0", "ten one-coal conversions equal exactly one energy");
            Check(run.Convert(Mineral.Coal, 40) && run.EnergyTenths == 50 && run.Coal == 10, "batch conversion equals repeated conversion exactly");
            string before = JsonUtility.ToJson(run.Growth());
            Check(!run.Convert(Mineral.Coal, 0) && !run.Convert(Mineral.Coal, -1) && !run.Convert(Mineral.Coal, 11) && !run.Convert(Mineral.Gold, 1) && before == JsonUtility.ToJson(run.Growth()), "invalid conversions do not partially consume minerals or award energy");
            run.Start(); Check(!run.Convert(Mineral.Coal, 1) && before == JsonUtility.ToJson(run.Growth()), "no conversion during gameplay");
            run.Regroup(); Check(run.EnergyTenths == 50 && run.Convert(Mineral.Coal, 10) && run.IsDiscovered(Mineral.Coal) && run.Coal == 0, "regroup preserves energy and conversion preserves discovery");
            data = Data(s); data.iron = 3;
            s.conversionRecipes = new[] { new ConversionRecipe { mineral = Mineral.Iron, energyTenthsPerOre = 3 } };
            run = new MiningSession(s, data);
            Check(run.Convert(Mineral.Iron, 2) && run.Iron == 1 && run.EnergyTenths == 6 && !run.Convert(Mineral.Coal, 1), "future mineral recipes work without coal-specific logic");
            s.conversionRecipes = new[] { new ConversionRecipe { mineral = Mineral.Coal, energyTenthsPerOre = 1 } };
            data = Data(s); data.coal = 10; data.energyTenths = int.MaxValue - 2; run = new MiningSession(s, data);
            Check(run.MaximumConvertible(Mineral.Coal) == 2 && !run.Convert(Mineral.Coal, 3) && run.Convert(Mineral.Coal, 2) && run.EnergyTenths == int.MaxValue && run.Coal == 8, "energy capacity is checked before consuming materials");
        }
        static void UpgradeRules(GameSettings s)
        {
            var data = Data(s, "ALLOY"); data.money = 1000; data.diamond = 20; data.coal = 50;
            var run = new MiningSession(s, data);
            Check(!run.CanUpgrade(0) && run.Convert(Mineral.Coal, 50) && run.CanUpgrade(0), "coal conversion supplies the required five energy");
            Check(run.UpgradePick(0) && run.Money == 0 && run.Diamond == 0 && run.EnergyTenths == 0 && run.Picks[0].Name == "DIAMOND" && run.Picks[0].Power == 3 && run.Picks[0].Maximum == 110, "diamond upgrade consumes exactly 1000 money, 20 diamonds and five energy");
            Check(run.UpgradeCost(0) == 2000 && !run.UpgradePick(0), "diamond exposes the superalloy recipe but cannot upgrade without resources");
            for (int missing = 0; missing < 3; missing++)
            {
                data = Data(s, "ALLOY"); data.money = missing == 0 ? 999 : 1000; data.diamond = missing == 1 ? 19 : 20; data.energyTenths = missing == 2 ? 49 : 50;
                run = new MiningSession(s, data); string before = JsonUtility.ToJson(run.Growth());
                Check(!run.UpgradePick(0) && before == JsonUtility.ToJson(run.Growth()), "missing diamond upgrade resource " + missing + " leaves every balance unchanged");
            }
            data = Data(s, "ALLOY"); data.money = 1000; data.diamond = 20; data.energyTenths = 50; run = new MiningSession(s, data); run.Start();
            Check(!run.UpgradePick(0) && run.Diamond == 20 && run.EnergyTenths == 50, "diamond upgrades are blocked during gameplay");
        }
        static void Movement(GameSettings s)
        {
            s.spawnLines = new SpawnLine[0]; var run = new MiningSession(s, Data(s, "DIAMOND")); run.Start(); for (int i = 0; i < 8; i++) run.Strike();
            int remaining = run.Picks[0].Remaining, money = run.Money;
            Check(!run.Drop(new Vector2Int(2, 4)) && run.LastDropFailure == DropFailure.OutOfRange, "three rows upward is rejected");
            Check(run.Drop(new Vector2Int(2, 5)) && run.Picks[0].Remaining == remaining, "two rows upward through empty cells is free");
            Check(!run.Drop(new Vector2Int(2, 8)) && run.LastDropFailure == DropFailure.OutOfRange && run.Money == money, "three rows downward is rejected without cost");
            Check(run.Drop(new Vector2Int(2, 7)) && run.Picks[0].Remaining == remaining, "two rows downward through empty cells is free");
            Check(!run.Drop(new Vector2Int(0, 7)) && run.LastDropFailure == DropFailure.Blocked, "horizontal intermediate solids still block a jump");
            for (int x = 3; x <= 4; x++) { run.Drop(new Vector2Int(x, 7)); run.Strike(); }
            run.Drop(new Vector2Int(2, 7)); run.Drop(new Vector2Int(1, 7)); run.Strike(); run.Drop(new Vector2Int(0, 7)); run.Strike();
            remaining = run.Picks[0].Remaining;
            Check(run.Drop(new Vector2Int(4, 7)) && run.Position.x == 4 && run.Picks[0].Remaining == remaining, "four-column horizontal travel remains allowed along an open path");
        }
        static GameObject SpawnGame(GameSettings s, ProgressData data)
        {
            new ProgressStore().Reset(data);
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/DigDeep/Prefabs/DigDeepGame.prefab"));
            var game = instance.GetComponent<GameManager>(); game.settings = s; MiningFeatureChecks.Tick(game, "Awake"); Refresh(game); return instance;
        }
        static void Click(GameManager game, float x, float y) { var point = game.View.ScreenPoint(new Vector2(x, y)); game.Controls.Down(point); game.Controls.Up(point); Refresh(game); }
        static void Interface(GameSettings s)
        {
            GameObject instance = null; var store = new ProgressStore();
            try
            {
                var data = Data(s, "ALLOY"); data.money = 1000; data.coal = 100; data.diamond = 20; data.energyTenths = 49;
                instance = SpawnGame(s, data); var game = instance.GetComponent<GameManager>();
                Check(game.diamondPick != null && game.diamondOre != null && game.energyConverter != null && game.PickSprite("DIAMOND") == game.diamondPick, "new sprites are attached in the real game prefab");
                var bounds = game.View.ButtonBounds("upgrade");
                Check(game.View.UiRoot.Find("제련") != null && Vector2.Distance(bounds.position, GameUILayout.Upgrade.position) < .02f && Vector2.Distance(bounds.size, GameUILayout.Upgrade.size) < .02f, "renamed refinery retains its menu layout and touch bounds");
                foreach (var asset in new[] { "DiamondOre", "DiamondPick", "EnergyConverter" })
                { var importer = (TextureImporter)AssetImporter.GetAtPath("Assets/DigDeep/Art/" + asset + ".png"); Check(!importer.isReadable && !importer.mipmapEnabled && importer.maxTextureSize == 512, asset + " has bounded texture memory"); }
                Check(Mathf.Abs(game.diamondPick.rect.width / game.diamondPick.rect.height - game.stonePick.rect.width / game.stonePick.rect.height) < .01f, "new pickaxe uses the existing pickaxe sprite proportions");
                Click(game, 420, 143); Check(game.Modal == GameModal.Upgrade && game.RefineryTab == RefineryPage.Upgrade, "refinery opens the upgrade tab through the common pointer input");
                MiningFeatureChecks.Capture(game, "70-refinery-upgrade-tab");
                Click(game, 140, 400); Check(game.Modal == GameModal.UpgradeConfirm && game.View.ActionAt(new Vector2(240, 590)) == "blocked", "insufficient energy disables upgrade confirmation");
                var table = game.View.UiRoot.Find("Trading and confirmation/Confirmation content/Readable requirements");
                var rows = table.GetComponentsInChildren<Image>().Where(i => i.name.StartsWith("Requirement row")).ToArray();
                Check(rows.Length == 3 && rows[2].GetComponentsInChildren<Text>().Any(t => t.text == "4.9") && rows[2].color.r > rows[2].color.g, "money, diamonds and energy are separate rows and insufficient energy is highlighted");
                Check(table.GetComponentsInChildren<Text>().Any(t => t.text == "필요") && table.GetComponentsInChildren<Text>().Any(t => t.text == "보유"), "requirement table separates needed and owned amounts");
                MiningFeatureChecks.Capture(game, "71-diamond-energy-shortage");
                Click(game, 240, 702); Click(game, 335, 316); Check(game.RefineryTab == RefineryPage.Conversion, "conversion tab is selectable");
                var conversionRowText = game.View.UiRoot.Find("Trading and confirmation/Refinery content/Mineral conversion/Convertible mineral list/Scrollable content/Coal/Ore information").GetComponent<Text>();
                Check(conversionRowText.preferredHeight <= conversionRowText.rectTransform.rect.height && conversionRowText.text.Contains("0.1"), "mineral name, stock and conversion ratio fit without clipping");
                MiningFeatureChecks.Capture(game, "72-converter-ready"); Click(game, 240, 657);
                Check(game.Session.Coal == 99 && game.Session.EnergyTenths == 50 && store.Load(s).energyTenths == 50 && store.Load(s).coal == 99, "one shared-input conversion persists consumed coal and awarded energy together");
                MiningFeatureChecks.Capture(game, "73-conversion-complete");
                Click(game, 140, 316); Click(game, 140, 400); MiningFeatureChecks.Capture(game, "74-diamond-upgrade-ready");
                Check(game.View.ActionAt(new Vector2(240, 590)) == "upgrade-confirm", "conversion activates diamond upgrade when all materials are ready");
                Click(game, 240, 590); Check(game.Session.Picks[0].Name == "DIAMOND" && store.Load(s).picks[0].id == "DIAMOND" && store.Load(s).energyTenths == 0, "diamond UI upgrade and every deduction save together");
                Click(game, 335, 316); for (int i = 1; i < 15; i++) game.HandleAction("convert-more"); game.HandleAction("convert"); Click(game, 240, 702); Click(game, 332, 143);
                Check(game.Session.EnergyTenths == 15 && instance.GetComponentsInChildren<Text>().Any(t => t.name == "Energy amount" && t.text == "1.5"), "inventory displays energy to one decimal place");
                MiningFeatureChecks.Capture(game, "75-energy-inventory");
                Click(game, 240, 702); game.HandleAction("shop"); game.HandleAction("shop-sell"); Refresh(game);
                var sale = game.View.UiRoot.Find("Trading and confirmation/Shop content/Sale page/Discovered ore sale list/Scrollable content");
                Check(sale.Find("Diamond").gameObject.activeSelf && !sale.Find("Oil").gameObject.activeSelf, "spent diamonds stay discovered while unseen oil stays hidden from sale");
                game.HandleAction("close"); game.HandleAction("reset"); game.HandleAction("reset-next"); game.HandleAction("reset-confirm");
                var reset = store.Load(s); Check(reset.diamond == 0 && reset.oil == 0 && reset.energyTenths == 0 && reset.picks[0].id == "WOOD" && reset.tutorial.controlsStep == 0, "two-confirmation reset clears all new resources and tutorial");
                UnityEngine.Object.DestroyImmediate(instance); instance = null;
                s.conversionRecipes = OreCatalog.All.Select(e => new ConversionRecipe { mineral = e.Kind, energyTenthsPerOre = e.Kind == Mineral.Oil ? 2 : 1 }).ToArray();
                data = Data(s); data.oil = 5; instance = SpawnGame(s, data); game = instance.GetComponent<GameManager>(); game.HandleAction("upgrade"); game.HandleAction("refinery-convert"); Refresh(game);
                game.View.MenuWheel(new Vector2(240, 500), -1000); Click(game, 240, 527);
                Check(game.ConversionKind == Mineral.Oil, "future conversion minerals are reachable by scrolling the generated list");
                game.HandleAction("convert-all"); game.HandleAction("convert"); Refresh(game);
                Check(game.Session.Oil == 0 && game.Session.EnergyTenths == 10, "future selected mineral uses its own configured conversion ratio");
                MiningFeatureChecks.Capture(game, "76-future-conversion-list-check");
                UnityEngine.Object.DestroyImmediate(instance); instance = null;
                s.conversionRecipes = new[] { new ConversionRecipe { mineral = Mineral.Coal, energyTenthsPerOre = 1 } };
                data = Data(s); data.tutorial.menuStep = 2; instance = SpawnGame(s, data); game = instance.GetComponent<GameManager>(); game.HandleAction("upgrade"); game.HandleAction("refinery-convert");
                Check(game.RefineryTab == RefineryPage.Upgrade && game.Tutorial.Prompt == TutorialPrompt.Upgrade, "unfinished refinery tutorial cannot be bypassed via conversion tab");
                game.HandleAction("upgrade-pick0"); game.HandleAction("close"); Check(store.Load(s).tutorial.menuStep == 3, "renamed refinery tutorial completes without a mandatory purchase or upgrade");
                UnityEngine.Object.DestroyImmediate(instance); instance = null;
                data = Data(s, "DIAMOND"); s.spawnLines = new[] { new SpawnLine { firstRow = 1, lastRow = 1, diamondPercent = 100 }, new SpawnLine { firstRow = 2, lastRow = 2, oilPercent = 100 } };
                instance = SpawnGame(s, data); game = instance.GetComponent<GameManager>(); game.Play(); for (int i = 0; i < 10; i++) game.Session.Strike(); Refresh(game);
                MiningFeatureChecks.Capture(game, "77-diamond-and-oil-blocks");
                var oilTile = instance.GetComponentsInChildren<Image>().First(i => i.name == "Block 7");
                Check(oilTile.sprite == null && oilTile.color == Color.black && game.Session.Diamond == 1 && game.Session.Oil == 0, "oil renders as a plain black square beside the new diamond resource");
            }
            finally { if (instance != null) UnityEngine.Object.DestroyImmediate(instance); }
        }
    }
}
