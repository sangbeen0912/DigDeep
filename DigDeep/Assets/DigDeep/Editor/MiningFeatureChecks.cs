using System.Linq;
using System;
using System.IO;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
namespace DigDeep.Editor
{
    public static class MiningFeatureChecks
    {
        static int checks;
        static void Check(bool value, string name) { checks++; if (!value) throw new Exception("Mining verification: " + name); }
        public static void Tick(MonoBehaviour component, string method)
        {
            component.GetType().GetMethod(method, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).Invoke(component, null);
        }
        static GameSettings Plain()
        { var s = ScriptableObject.CreateInstance<GameSettings>(); s.spawnLines = new SpawnLine[0]; return s; }
        static SpawnLine OreLine(Mineral kind) => new SpawnLine { name = "Test", firstRow = 1, lastRow = 35, coalPercent = kind == Mineral.Coal ? 100 : 0, ironPercent = kind == Mineral.Iron ? 100 : 0 };
        public static void Rules()
        {
            checks = 0; var s = Plain();
            try
            {
                var run = new MiningSession(s, 0, 1);
                Check(!run.IsDiscovered(Mineral.Coal) && !run.IsDiscovered(Mineral.Iron) && !run.CanSell(Mineral.Coal, 1), "fresh game has no confirmed ores");
                Check(run.Picks.Length == 1 && run.Picks[0].Name == "WOOD" && run.Picks[0].Maximum == 20, "one initial wooden pick");
                Check(!run.Strike(), "no mining before Dig"); run.Start();
                for (int i = 0; i < 20; i++) run.Strike();
                Check(run.Money == 20 && run.Picks[0].Remaining == 0 && run.State == SessionState.GameOver, "20 stone hits pay 20 and exhaust wood");
                run.Restart(); Check(run.Money == 20 && run.Health(new Vector2Int(2, 0)) == 1 && run.Picks[0].Remaining == 20, "restart retains growth and refreshes run");
                s.woodDurability = 100;
                run = new MiningSession(s, 0, 2); run.Start(); for (int i = 0; i < 8; i++) run.Strike();
                int durability = run.Picks[0].Remaining;
                Check(!run.Drop(new Vector2Int(2, 1)), "six-row movement rejected");
                Check(run.Drop(new Vector2Int(2, 5)), "up two through empty cells");
                Check(run.Drop(new Vector2Int(2, 7)) && run.Picks[0].Remaining == durability, "down two through empty cells is free");
                Check(!run.Drop(new Vector2Int(0, 7)), "solid intermediate columns block crossing");
                Check(run.Drop(new Vector2Int(2, 8)), "downward solid destination accepted"); run.Strike();
                Check(run.Position == new Vector2Int(2, 8), "downward destination mined");
                Check(!run.Drop(new Vector2Int(5, 8)), "outside board rejected");
                s.woodDurability = 20; s.spawnLines = new[] { OreLine(Mineral.Coal) };
                run = new MiningSession(s, 0, 3); run.Start(); run.Strike();
                Check(!run.IsDiscovered(Mineral.Coal), "seeing or partially striking coal does not confirm it");
                Check(run.Coal == 0 && run.Health(new Vector2Int(2, 0)) == 4, "partial coal does not award inventory");
                for (int i = 0; i < 4; i++) run.Strike();
                Check(run.Coal == 1 && run.Money == 0 && run.Picks[0].Remaining == 15, "coal enters inventory once after five hits");
                Check(run.IsDiscovered(Mineral.Coal) && !run.IsDiscovered(Mineral.Iron), "first acquisition confirms only that ore");
                run.Regroup(); Check(run.Sell(Mineral.Coal, 1) && run.Coal == 0 && run.IsDiscovered(Mineral.Coal), "sell-out keeps confirmed ore record");
                Check(ProgressData.TryParse(JsonUtility.ToJson(run.Growth()), out var knownData) && new MiningSession(s, knownData).IsDiscovered(Mineral.Coal), "confirmed record survives reconnect with zero stock");
                Check(!new MiningSession(s, ProgressData.Initial(s)).IsDiscovered(Mineral.Coal), "reset removes confirmations");
                Check(!run.Sell((Mineral)999, 1), "unknown catalog item cannot be sold");
                s.woodDurability = 3; var data = ProgressData.Initial(s); data.picks.Add(new PickProgress { id = "WOOD" });
                run = new MiningSession(s, data, 4); run.Start(); for (int i = 0; i < 3; i++) run.Strike();
                Check(run.State == SessionState.Playing && !run.CanMine && run.Health(new Vector2Int(2, 0)) == 2, "partial target and manual switch after depletion");
                Check(run.Select(1), "second usable pick selection"); run.Strike(); run.Strike();
                Check(run.Coal == 1 && run.Picks[1].Remaining == 1, "second pick completes same target");
                s.woodDurability = 20; s.spawnLines = new[] { OreLine(Mineral.Iron) };
                data = ProgressData.Initial(s); data.picks[0].id = "IRON";
                run = new MiningSession(s, data, 5); var damage = new List<float>(); run.Impact += (cell, amount, broken) => damage.Add(amount);
                run.Start(); for (int i = 0; i < 8; i++) run.Strike();
                Check(run.Iron == 1 && run.Picks[0].Remaining == 42 && damage[7] == 1, "iron ore: 8 strikes, 8 durability, last damage capped to one");
                Check(s.SellPrice(Mineral.Coal) == 6 && s.SellPrice(Mineral.Iron) == 18, "per-item prices floor 125 percent");
                s.spawnLines = new SpawnLine[0]; data = ProgressData.Initial(s, 11100); run = new MiningSession(s, data, 6);
                Check(run.PurchasePrice == 100 && run.Buy() && run.PurchasePrice == 1000, "first purchase");
                Check(run.Buy() && run.PurchasePrice == 10000 && run.Buy(), "second and third purchases");
                Check(run.Picks.Length == 4 && run.Money == 0 && !run.Buy() && run.PurchasePrice == -1, "cap four, no extra deduction");
                Check(Array.TrueForAll(run.Picks, p => p.Name == "WOOD"), "all purchased picks are wood");
                Check(ProgressData.TryParse(JsonUtility.ToJson(run.Growth()), out var restored) && restored.picks.Count == 4, "four same-kind picks serialize");
                data = ProgressData.Initial(s); data.coal = 10; data.iron = 12; run = new MiningSession(s, data, 7);
                Check(run.Sell(Mineral.Coal, 3) && run.Coal == 7 && run.Money == 18, "coal quantity sale");
                Check(run.Sell(Mineral.Iron, 2) && run.Iron == 10 && run.Money == 54, "iron quantity sale preserves material reserve");
                Check(!run.Sell(Mineral.Coal, 99) && !run.Sell(Mineral.Coal, 0) && run.Coal == 7 && run.Money == 54, "invalid quantity cannot mutate");
                Check(run.UpgradePick(0) && run.Picks[0].Name == "STONE" && run.Picks[0].Maximum == 30 && run.Money == 4 && run.Coal == 4 && run.Iron == 10, "wood to stone consumes 50 and three coal");
                data = run.Growth(); data.money = 200; data.coal = 14; data.iron = 7; run = new MiningSession(s, data, 8);
                Check(run.UpgradePick(0) && run.Picks[0].Name == "IRON" && run.Picks[0].Power == 2 && run.Picks[0].Maximum == 50 && run.Money == 0 && run.Iron == 0 && run.Coal == 0, "stone to iron consumes 200, seven iron and fourteen coal");
                Check(!run.UpgradePick(0), "alloy requires its own money and materials");
                data = ProgressData.Initial(s, 50); data.coal = 2; run = new MiningSession(s, data);
                Check(!run.UpgradePick(0) && run.Money == 50 && run.Coal == 2 && run.Picks[0].Name == "WOOD", "missing third coal blocks stone upgrade without deduction");
                data = ProgressData.Initial(s, 200); data.coal = 13; data.iron = 7; data.picks[0].id = "STONE"; run = new MiningSession(s, data);
                Check(!run.UpgradePick(0) && run.Money == 200 && run.Coal == 13 && run.Iron == 7 && run.Picks[0].Name == "STONE", "missing fourteenth coal blocks iron upgrade without deduction");
                data = ProgressData.Initial(s, 200); data.picks[0].id = "STONE"; data.iron = 6; data.coal = 14; run = new MiningSession(s, data, 9);
                Check(!run.UpgradePick(0) && run.Money == 200 && run.Iron == 6 && run.Coal == 14, "missing iron keeps every balance");
                run.Start(); Check(!run.Buy() && !run.Sell(Mineral.Iron, 1) && !run.UpgradePick(0), "all trading blocked during gameplay");
                run.Strike(); run.Regroup();
                Check(run.State == SessionState.Ready && run.Money == 201 && run.Picks[0].Remaining == 30 && run.Iron == 6, "regroup has no growth penalty");
                string snapshot = JsonUtility.ToJson(run.Growth());
                Check(!snapshot.Contains("Remaining") && !snapshot.Contains("Position") && !snapshot.Contains("Target"), "only growth stored");
                Check(!ProgressData.TryParse("{}", out _) && !ProgressData.TryParse("broken", out _), "invalid data rejected");
                Check(ProgressData.TryParse("{\"version\":1,\"money\":47,\"picks\":[{\"id\":\"WOOD\",\"power\":4,\"maximum\":120,\"upgrade\":0}]}", out restored), "legacy save read");
                run = new MiningSession(s, restored, 10);
                Check(run.Money == 47 && run.Picks[0].Maximum == 20 && run.Picks[0].Power == 1, "legacy wood rebalanced without money loss");
                Check(!ProgressData.TryParse("{\"version\":2,\"money\":0,\"coal\":-1,\"picks\":[{\"id\":\"WOOD\"}]}", out _), "negative inventory rejected");
                Check(!ProgressData.TryParse("{\"version\":9,\"money\":0,\"picks\":[{\"id\":\"WOOD\"}]}", out _), "unknown schema rejected");
                Check(ProgressData.TryParse("{\"version\":2,\"coal\":3,\"iron\":0,\"picks\":[{\"id\":\"WOOD\"}]}", out var migrated)
                    && migrated.version == 7 && migrated.discovered.Contains("Coal") && !migrated.discovered.Contains("Iron"), "legacy inventory confirms only owned ores");
                Check(!ProgressData.TryParse("{\"version\":3,\"discovered\":[\"InvalidOre\"],\"picks\":[{\"id\":\"WOOD\"}]}", out _), "invalid confirmation record rejected");
                SpawnRules(s);
                Debug.Log("DIGDEEP_MINING_RULES_PASSED " + checks);
            }
            finally { UnityEngine.Object.DestroyImmediate(s); }
        }
        static void SpawnRules(GameSettings s)
        {
            s.spawnLines = new[] { new SpawnLine { firstRow = 2, lastRow = 10, coalPercent = 100 }, new SpawnLine { firstRow = 11, lastRow = 35, ironPercent = 100 } };
            var spawn = new OreSpawnManager(s, 11);
            Check(spawn.Kind(new Vector2Int(0, 9)) == Mineral.Coal && spawn.Kind(new Vector2Int(0, 10)) == Mineral.Iron && spawn.Kind(new Vector2Int(0, 35)) == Mineral.Stone, "one-based inclusive bands, unconfigured stone");
            Check(spawn.Kind(new Vector2Int(0, 0)) == Mineral.Stone && spawn.Kind(new Vector2Int(0, 1)) == Mineral.Coal, "first row is rock and line one starts on second row");
            s.spawnLines = new[] { new SpawnLine { firstRow = 2, lastRow = 10, coalPercent = .00001f }, new SpawnLine { firstRow = 11, lastRow = 35, coalPercent = .00001f, ironPercent = .00001f } };
            bool allGuaranteed = true, allStable = true;
            for (int seed = 0; seed < 300; seed++)
            {
                spawn = new OreSpawnManager(s, seed); int first = 0, second = 0;
                for (int row = 0; row < 35; row++) for (int col = 0; col < 5; col++)
                {
                    var cell = new Vector2Int(col, row); var kind = spawn.Kind(cell);
                    allStable &= kind == spawn.Kind(cell);
                    if (kind != Mineral.Stone) { if (row < 10) first++; else second++; }
                }
                allGuaranteed &= first >= 1 && second >= 1;
            }
            Check(allGuaranteed, "every band guarantees at least one of its ores"); Check(allStable, "repeated cell access cannot reroll");
            s.spawnLines = new[] { new SpawnLine { firstRow = 2, lastRow = 10, coalPercent = 5 }, new SpawnLine { firstRow = 11, lastRow = 35, coalPercent = 5, ironPercent = 5 } };
            int coalFirst = 0, coalSecond = 0, ironSecond = 0;
            for (int seed = 0; seed < 500; seed++)
            {
                spawn = new OreSpawnManager(s, seed);
                for (int row = 0; row < 35; row++) for (int col = 0; col < 5; col++)
                {
                    var kind = spawn.Kind(new Vector2Int(col, row));
                    if (row < 10 && kind == Mineral.Coal) coalFirst++;
                    else if (row >= 10) { if (kind == Mineral.Coal) coalSecond++; if (kind == Mineral.Iron) ironSecond++; }
                }
            }
            Check(Math.Abs(coalFirst / 22500.0 - (.05 + Math.Pow(.95, 45) / 45)) < .006, "nine-row first-line coal probability with guarantee");
            Check(Math.Abs(coalSecond / 62500.0 - .04875) < .004 && Math.Abs(ironSecond / 62500.0 - .04875) < .004, "independent rolls plus uniform conflict choice");
        }
        public static void RunIntegration()
        {
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "-digdeep-revision-test") < 0) throw new Exception("Verification save isolation flag required.");
            GameObject instance = null; int initialChecks = checks;
            try
            {
                var settings = AssetDatabase.LoadAssetAtPath<GameSettings>("Assets/DigDeep/GameSettings.asset");
                var store = new ProgressStore(); var data = ProgressData.Initial(settings, 11200); data.coal = 25; data.iron = 12; data.tutorial = TutorialProgress.Completed(); store.Reset(data);
                instance = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/DigDeep/Prefabs/DigDeepGame.prefab"));
                var game = instance.GetComponent<GameManager>(); Tick(game, "Awake");
                Check(game.Session.Picks.Length == 1 && game.Session.Money == 11200 && game.Session.Iron == 12, "manager loads full growth");
                Check(game.View.ActionAt(new Vector2(350, 140)) == "inventory", "inventory beside shop");
                Capture(game, "01-ready"); game.HandleAction("shop");
                Check(game.ShopTab == ShopPage.Buy, "shop opens purchase tab"); Capture(game, "09-purchase");
                var saleTab = game.View.ScreenPoint(new Vector2(335, 320)); game.Controls.Down(saleTab); game.Controls.Up(saleTab);
                Check(game.ShopTab == ShopPage.Sell, "sale tab uses shared pointer dispatcher");
                Capture(game, "10-sale-list"); ScrollChecks(game);
                CheckSaleWording(game);
                game.HandleAction("sale-iron"); game.HandleAction("sale-all");
                Check(game.Session.Iron == 12 && game.SaleQuantity == 12, "all chooses amount without selling");
                game.HandleAction("sale-less"); game.HandleAction("sale-less");
                game.HandleAction("sell"); Check(game.Session.Iron == 2 && game.Session.Money == 11380, "sale dispatcher and persistence");
                Capture(game, "02-shop"); game.HandleAction("shop-buy");
                for (int i = 0; i < 3; i++) game.HandleAction("buy");
                Check(game.Session.Picks.Length == 4 && game.Session.Money == 280, "buy dispatcher applies price ladder");
                game.HandleAction("buy"); Check(game.Session.Money == 280, "full purchase button cannot double-charge");
                game.HandleAction("close"); game.HandleAction("upgrade");
                Capture(game, "03-upgrade-selection"); game.HandleAction("upgrade-pick0");
                Check(game.Modal == GameModal.UpgradeConfirm, "select an individual upgrade target");
                Capture(game, "04-upgrade-ready"); game.HandleAction("upgrade-confirm");
                Check(game.Session.Picks[0].Name == "STONE" && game.Session.Picks[1].Name == "WOOD" && game.Session.Money == 230 && game.Session.Coal == 22, "selected pick upgrade deducts exactly three coal");
                game.HandleAction("upgrade-pick0");
                Check(game.View.ActionAt(new Vector2(240, 590)) == "blocked", "insufficient ore disables confirm");
                game.HandleAction("upgrade-confirm"); Check(game.Session.Money == 230 && game.Session.Iron == 2 && game.Session.Coal == 22, "guard rechecks every material without partial deduction");
                Capture(game, "05-upgrade-blocked"); game.HandleAction("close"); game.HandleAction("close");
                game.HandleAction("inventory"); Capture(game, "06-inventory"); game.HandleAction("close");
                var snapshot = store.Load(settings);
                Check(snapshot.money == 230 && snapshot.iron == 2 && snapshot.coal == 22 && snapshot.picks.Count == 4 && snapshot.picks[0].id == "STONE", "money and all upgrade materials persist together");
                var playPoint = game.View.ScreenPoint(new Vector2(240, 560));
                game.Controls.Down(playPoint); game.Controls.Up(playPoint);
                Check(game.Session.State == SessionState.Playing, "Dig button uses shared pointer dispatcher");
                Check(game.View.ActionAt(new Vector2(350, 140)) == "blocked", "inventory unavailable during gameplay");
                game.HandleAction("shop"); Check(game.Modal == GameModal.None, "shop blocked while playing");
                var fourth = game.View.ScreenPoint(new Vector2(207, 50));
                int before = game.Session.Picks[3].Remaining;
                game.Controls.Down(fourth); game.Controls.Up(fourth);
                Check(game.Session.Selected == 3 && game.Session.Picks[3].Remaining == before && !game.IsSwinging, "fourth slot selection cannot trigger mining");
                var minePoint = game.View.ScreenPoint(new Vector2(100, 700));
                game.Controls.Down(minePoint); game.Controls.Up(minePoint); FinishSwing(game);
                Check(game.Session.Picks[3].Remaining == before - 1, "pointer tap performs one strike");
                game.Controls.Down(minePoint);
                typeof(PointerController).GetField("started", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).SetValue(game.Controls, Time.unscaledTime - game.settings.holdDelay - .01f);
                game.Controls.Move(minePoint); Check(game.IsSwinging, "pointer hold starts mining"); FinishSwing(game);
                game.Controls.Up(minePoint); int stopped = game.Session.Picks[3].Remaining; Tick(game, "Update");
                Check(!game.IsSwinging && game.Session.Picks[3].Remaining == stopped, "hold release immediately cancels following strike");
                game.SelectPick(0);
                for (int i = 0; i < 7; i++) game.Session.Strike();
                Capture(game, "07-mining"); game.Regroup();
                int retained = game.Session.Money; game.HandleAction("reset");
                Check(game.Modal == GameModal.ResetWarning && game.Session.Money == retained, "first reset warning preserves growth");
                game.HandleAction("reset-next"); Capture(game, "08-reset-confirm"); game.HandleAction("close");
                Check(game.Session.Money == retained, "reset cancellation preserves growth");
                game.HandleAction("reset"); game.HandleAction("reset-next"); game.HandleAction("reset-confirm");
                Check(game.Session.Money == 0 && game.Session.Coal == 0 && game.Session.Iron == 0 && game.Session.Picks.Length == 1 && game.Session.Picks[0].Name == "WOOD", "reset clears all growth to one wood");
                game.SaveGrowth(); Check(store.Load(settings).picks.Count == 1 && store.Load(settings).money == 0, "old saves cannot return");
                UnityEngine.Object.DestroyImmediate(instance); instance = null;
                DiscoveryUiChecks(settings, store);
                UpgradeUiChecks(settings, store);
                Directory.CreateDirectory("Logs/MiningUpgrade");
                File.WriteAllText("Logs/MiningUpgrade/checks.txt", "PASS " + checks + " assertions (" + (checks - initialChecks) + " integration). No player build.\n");
                Debug.Log("DIGDEEP_MINING_FEATURES_PASSED " + checks);
            }
            finally
            {
                if (instance != null) UnityEngine.Object.DestroyImmediate(instance);
                PlayerPrefs.DeleteKey("DigDeep.Progress.v1.Verification"); PlayerPrefs.DeleteKey("DigDeep.Progress.backup.v1.Verification"); PlayerPrefs.DeleteKey(GameSettings.MoneyKey + ".Verification"); PlayerPrefs.Save();
            }
        }
        static void CheckSaleWording(GameManager game)
        {
            var sale = game.GetComponentInChildren<Canvas>().transform.Find("Portrait 480x854/Trading and confirmation/Shop content/Sale page");
            bool clean = true;
            foreach (var text in sale.GetComponentsInChildren<UnityEngine.UI.Text>()) clean &= !text.text.Contains("강화");
            Check(clean, "sales rows and fixed footer have no upgrade-material wording");
        }
        static void UpgradeUiChecks(GameSettings settings, ProgressStore store)
        {
            GameObject instance = null;
            try
            {
                var data = ProgressData.Initial(settings, 1000); data.coal = 17; data.iron = 7; data.tutorial = TutorialProgress.Completed(); store.Reset(data);
                instance = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/DigDeep/Prefabs/DigDeepGame.prefab"));
                var game = instance.GetComponent<GameManager>(); Tick(game, "Awake");
                game.HandleAction("upgrade"); game.HandleAction("upgrade-pick0"); game.HandleAction("upgrade-confirm");
                game.HandleAction("upgrade-pick0"); Capture(game, "14-iron-upgrade-ready");
                Check(game.View.ActionAt(new Vector2(240, 590)) == "upgrade-confirm", "iron confirm enables only with money and both ores");
                var rows = game.View.UiRoot.Find("Trading and confirmation/Confirmation content/Readable requirements");
                var requirements = string.Join(" ", rows.GetComponentsInChildren<UnityEngine.UI.Text>().Select(t => t.text));
                Check(requirements.Contains("석탄") && requirements.Contains("14개") && requirements.Contains("철광석") && requirements.Contains("7개"), "iron confirmation shows both required ores");
                game.HandleAction("upgrade-confirm"); var saved = store.Load(settings);
                Check(game.Session.Picks[0].Name == "IRON" && saved.money == 750 && saved.coal == 0 && saved.iron == 0 && saved.picks[0].id == "IRON", "successful iron upgrade saves all deductions together");
                UnityEngine.Object.DestroyImmediate(instance); instance = null;
                data = ProgressData.Initial(settings, 50); data.coal = 2; data.tutorial = TutorialProgress.Completed(); store.Reset(data);
                instance = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/DigDeep/Prefabs/DigDeepGame.prefab"));
                game = instance.GetComponent<GameManager>(); Tick(game, "Awake");
                game.HandleAction("upgrade"); game.HandleAction("upgrade-pick0"); Capture(game, "15-stone-upgrade-coal-shortage");
                Check(game.View.ActionAt(new Vector2(240, 590)) == "blocked", "stone confirm disables with two coal");
                game.HandleAction("upgrade-confirm"); saved = store.Load(settings);
                Check(saved.money == 50 && saved.coal == 2 && saved.picks[0].id == "WOOD", "blocked confirm cannot deduct money or coal");
            }
            finally { if (instance != null) UnityEngine.Object.DestroyImmediate(instance); }
        }
        static void ScrollChecks(GameManager game)
        {
            var menus = game.GetComponent<GameMenus>();
            var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic;
            var list = typeof(GameMenus).GetField("saleList", flags).GetValue(menus);
            var areaField = list.GetType().GetField("Area", flags); var offsetField = list.GetType().GetField("Offset", flags);
            var content = (RectTransform)list.GetType().GetField("Content", flags).GetValue(list);
            var viewport = (RectTransform)content.parent; Rect oldArea = (Rect)areaField.GetValue(list); Vector2 oldSize = viewport.sizeDelta;
            // A shorter viewport creates overflow without introducing unplanned ore types.
            Rect small = oldArea; small.height = 88; areaField.SetValue(list, small); viewport.sizeDelta = new Vector2(oldSize.x, 88);
            try
            {
                menus.Refresh(); var selected = game.SaleKind; int stock = game.Session.Iron; int money = game.Session.Money;
                var start = game.View.ScreenPoint(new Vector2(240, 400)); var end = game.View.ScreenPoint(new Vector2(240, 285));
                game.Controls.Down(start); game.Controls.Move(end); game.Controls.Up(end);
                Check((float)offsetField.GetValue(list) == 88 && game.ShopTab == ShopPage.Sell && game.SaleKind == selected, "vertical swipe scrolls without selecting or changing tabs");
                Check(game.View.ActionAt(new Vector2(240, 400)) == "sale-iron" && game.View.ActionAt(new Vector2(240, 500)) == "blocked", "scrolled row hit testing respects viewport clipping");
                Check(game.Session.Iron == stock && game.Session.Money == money, "scroll cannot sell or buy");
                game.View.MenuWheel(new Vector2(240, 400), 1000); Check((float)offsetField.GetValue(list) == 0, "mouse wheel clamps list to top");
            }
            finally { areaField.SetValue(list, oldArea); viewport.sizeDelta = oldSize; game.View.MenuWheel(new Vector2(240, 400), 1000); }
        }
        static void DiscoveryUiChecks(GameSettings settings, ProgressStore store)
        {
            var config = UnityEngine.Object.Instantiate(settings); config.spawnLines = new[] { OreLine(Mineral.Coal) };
            GameObject instance = null;
            try
            {
                var data = ProgressData.Initial(config); data.tutorial = TutorialProgress.Completed(); store.Reset(data);
                instance = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/DigDeep/Prefabs/DigDeepGame.prefab"));
                var game = instance.GetComponent<GameManager>(); game.settings = config; Tick(game, "Awake");
                game.HandleAction("shop"); game.HandleAction("shop-sell"); Capture(game, "11-sale-undiscovered");
                var root = game.GetComponentInChildren<Canvas>().transform.Find("Portrait 480x854/Trading and confirmation/Shop content/Sale page/Discovered ore sale list/Scrollable content");
                Check(!root.Find("Coal").gameObject.activeSelf && !root.Find("Iron").gameObject.activeSelf, "unconfirmed ores have no visible sales rows");
                Check(game.View.ActionAt(new Vector2(240, 400)) == "blocked" && game.View.ActionAt(new Vector2(240, 650)) == "blocked", "empty list cannot select or sell");
                game.HandleAction("sale-iron"); game.HandleAction("sale-all"); game.HandleAction("sell");
                Check(game.SaleKind == Mineral.Stone && game.Session.Money == 0, "direct command cannot select unconfirmed ore");
                game.HandleAction("close"); game.Play(); game.Session.Strike();
                Check(!game.Session.IsDiscovered(Mineral.Coal), "partial mining keeps sale locked");
                for (int i = 0; i < 4; i++) game.Session.Strike(); game.Regroup();
                game.HandleAction("shop"); game.HandleAction("shop-sell"); Capture(game, "12-sale-one-confirmed");
                Check(root.Find("Coal").gameObject.activeSelf && !root.Find("Iron").gameObject.activeSelf, "acquired coal becomes visible while iron stays hidden");
                game.HandleAction("sale-iron"); Check(game.SaleKind == Mineral.Coal && game.SaleQuantity == 1, "hidden ore command cannot change active selection");
                game.HandleAction("sale-all"); Check(game.Session.Coal == 1 && game.Session.Money == 0, "all remains quantity-only after discovery");
                game.HandleAction("sell"); Capture(game, "13-sale-sold-out");
                Check(root.Find("Coal").gameObject.activeSelf && game.Session.Coal == 0 && game.Session.Money == 6 && game.View.ActionAt(new Vector2(240, 650)) == "blocked", "sold-out discovered row remains and sale button disables");
                var saved = store.Load(config);
                Check(saved.discovered.Contains("Coal") && !saved.discovered.Contains("Iron") && new MiningSession(config, saved).IsDiscovered(Mineral.Coal), "manager saves confirmations separately from stock");
                game.HandleAction("close"); game.HandleAction("reset"); game.HandleAction("reset-next"); game.HandleAction("reset-confirm");
                Check(!game.Session.IsDiscovered(Mineral.Coal) && store.Load(config).discovered.Count == 0, "two-confirmation reset clears discovery save");
            }
            finally { if (instance != null) UnityEngine.Object.DestroyImmediate(instance); UnityEngine.Object.DestroyImmediate(config); }
        }
        static void FinishSwing(GameManager game)
        {
            typeof(GameManager).GetField("swingStarted", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).SetValue(game, Time.unscaledTime - game.settings.strikeInterval - .01f);
            Tick(game, "Update");
        }
        public static void Capture(GameManager game, string name)
        {
            Tick(game.View, "LateUpdate");
            var canvas = game.GetComponentInChildren<Canvas>(); var camera = Camera.main;
            var target = new RenderTexture(480, 854, 24); target.Create();
            canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = camera; canvas.planeDistance = 1; camera.targetTexture = target;
            Canvas.ForceUpdateCanvases(); var root = (RectTransform)canvas.transform.Find("Portrait 480x854"); root.localScale = Vector3.one; root.anchoredPosition = Vector2.zero; Canvas.ForceUpdateCanvases();
            RenderPipeline.SubmitRenderRequest(camera, new UniversalRenderPipeline.SingleCameraRequest { destination = target });
            var old = RenderTexture.active; RenderTexture.active = target;
            var image = new Texture2D(480, 854, TextureFormat.RGB24, false); image.ReadPixels(new Rect(0, 0, 480, 854), 0, 0); image.Apply();
            Directory.CreateDirectory("Logs/MiningUpgrade"); File.WriteAllBytes("Logs/MiningUpgrade/" + name + ".png", image.EncodeToPNG());
            RenderTexture.active = old; camera.targetTexture = null; canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            UnityEngine.Object.DestroyImmediate(image); target.Release(); UnityEngine.Object.DestroyImmediate(target);
        }
    }
}
