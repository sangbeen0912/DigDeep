using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
namespace DigDeep.Editor
{
    public static class UiRecordFeatureChecks
    {
        static int checks;
        static void Check(bool valid, string name) { checks++; if (!valid) throw new Exception("UI/record verification: " + name); }
        public static void Run()
        {
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "-digdeep-revision-test") < 0) throw new Exception("Isolated save flag required");
            checks = 0;
            var settings = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameSettings>("Assets/DigDeep/GameSettings.asset"));
            settings.spawnLines = new SpawnLine[0];
            try { Records(settings); Results(settings); Resources(settings); Interface(settings); }
            finally { UnityEngine.Object.DestroyImmediate(settings); }
            Debug.Log("DIGDEEP_UI_RECORD_FEATURES_PASSED " + checks + " NO_BUILD");
            File.WriteAllText("Logs/MiningUpgrade/ui-record-checks.txt", "Existing checks: 177\nUI/record checks: " + checks + "\nNo player build or deployment.\n");
        }
        static void Records(GameSettings settings)
        {
            var data = ProgressData.Initial(settings, 17); data.bestDepth = 3; data.coal = 2; data.iron = 3; data.gold = 4;
            var run = new MiningSession(settings, data); int notices = 0; run.RecordBroken += row => notices++;
            Check(run.BestDepth == 3 && run.Deepest == 0, "restored record and fresh current depth");
            run.Start(); for (int i = 0; i < 3; i++) run.Strike();
            Check(run.BestDepth == 3 && notices == 0, "matching record does not notify");
            run.Strike(); run.Strike();
            Check(run.BestDepth == 5 && run.Deepest == 5 && notices == 1, "new record notified once while each deeper row updates");
            Check(ProgressData.TryParse(JsonUtility.ToJson(run.Growth()), out var restored) && restored.bestDepth == 5 && restored.money == 22, "record serializes with earned money");
            var reconnect = new MiningSession(settings, restored);
            Check(reconnect.BestDepth == 5 && reconnect.Deepest == 0 && reconnect.Picks[0].Remaining == 20, "reconnect keeps record but restores a new run");
            run.Regroup(); run.Start(); for (int i = 0; i < 6; i++) run.Strike();
            Check(run.BestDepth == 6 && notices == 2, "a later run can celebrate a new record once again");
            while (run.CanMine) run.Strike();
            Check(run.State == SessionState.GameOver && run.BestDepth == 20, "game over retains record"); run.Restart();
            Check(run.BestDepth == 20 && run.Deepest == 0, "restart retains record");
            const string old = "{\"version\":5,\"money\":123,\"coal\":2,\"iron\":3,\"gold\":4,\"tutorial\":{\"controlsStep\":3,\"menuStep\":1},\"picks\":[{\"id\":\"IRON\"}]}";
            Check(ProgressData.TryParse(old, out restored) && restored.version == 7 && restored.bestDepth == 0 && restored.money == 123 && restored.gold == 4 && restored.tutorial.controlsStep == 3 && restored.picks[0].id == "IRON", "version five migration preserves growth and tutorial");
            restored.bestDepth = -1; Check(!ProgressData.TryParse(JsonUtility.ToJson(restored), out _), "negative record rejected");
            Check(new MiningSession(settings, ProgressData.Initial(settings)).BestDepth == 0, "data reset clears record");
            settings.spawnLines = new[] { new SpawnLine { firstRow = 1, lastRow = 1, goldPercent = 100 } };
            run = new MiningSession(settings, ProgressData.Initial(settings)); run.Start(); run.Strike();
            Check(run.BestDepth == 0 && run.Deepest == 0, "partial damage is not a reached row");
            settings.spawnLines = new SpawnLine[0];
        }
        static void Results(GameSettings settings)
        {
            var data = ProgressData.Initial(settings); data.coal = 200; data.iron = 100; data.gold = 30;
            var run = new MiningSession(settings, data); run.Start(); while (run.CanMine) run.Strike();
            string summary = run.ResultSummary();
            Check(summary == "이번 판 도달 깊이\n20행", "result shows this run depth only despite stocked inventory and earned money");
            settings.spawnLines = new[] { new SpawnLine { firstRow = 1, lastRow = 1, coalPercent = 100 } };
            settings.woodDurability = 5; run = new MiningSession(settings, ProgressData.Initial(settings)); run.Start(); while (run.CanMine) run.Strike();
            summary = run.ResultSummary();
            Check(summary == "이번 판 도달 깊이\n1행" && run.RunCoal == 1, "mined coal is retained but result shows only depth");
            settings.spawnLines = new[] { new SpawnLine { firstRow = 1, lastRow = 1, coalPercent = 100 }, new SpawnLine { firstRow = 2, lastRow = 2, ironPercent = 100 }, new SpawnLine { firstRow = 3, lastRow = 3, goldPercent = 100 } };
            settings.woodDurability = 35; run = new MiningSession(settings, ProgressData.Initial(settings)); run.Start(); while (run.CanMine) run.Strike();
            summary = run.ResultSummary();
            Check(summary == "이번 판 도달 깊이\n3행" && run.RunCoal == 1 && run.RunIron == 1 && run.RunGold == 1, "all three minerals are retained but omitted from the result");
            settings.spawnLines = new SpawnLine[0]; settings.woodDurability = 20;
        }
        static void Resources(GameSettings settings)
        {
            var data = ProgressData.Initial(settings); data.bestDepth = 11;
            var run = new MiningSession(settings, data);
            Check(run.GrantResources(20000, 200, 100, 30) && run.Money == 20000 && run.Coal == 200 && run.Iron == 100 && run.Gold == 30, "presentation grant amounts");
            Check(run.BestDepth == 11 && run.Picks.Length == 1 && run.Picks[0].Remaining == 20 && run.Tutorial.controlsStep == 0 && run.RunGold == 0, "grant changes only resources and their discoveries");
            Check(run.IsDiscovered(Mineral.Gold) && run.CanSell(Mineral.Gold, 1), "presentation materials are usable in the sale list");
            Check(run.Buy() && run.Buy() && run.Buy(), "presentation money buys all additional slots");
            bool upgraded = true;
            for (int pick = 0; pick < 4; pick++) for (int step = 0; step < 3; step++) upgraded &= run.UpgradePick(pick);
            Check(upgraded && run.Picks.All(p => p.Name == "ALLOY") && run.Money == 5900 && run.Coal == 12 && run.Iron == 32 && run.Gold == 10, "grant is sufficient for all four alloy picks");
            string before = JsonUtility.ToJson(run.Growth());
            Check(!run.GrantResources(-1, 1, 1, 1) && before == JsonUtility.ToJson(run.Growth()), "negative grant has no partial effects");
            run.Start(); before = JsonUtility.ToJson(run.Growth());
            Check(!run.GrantResources(1, 1, 1, 1) && before == JsonUtility.ToJson(run.Growth()), "no grant during gameplay");
            run.Regroup(); Check(run.GrantResources(int.MaxValue, int.MaxValue, int.MaxValue, int.MaxValue) && run.Money == int.MaxValue && run.Gold == int.MaxValue, "repeated grants cap without overflow");
        }
        static GameObject Spawn(GameSettings settings, ProgressData data)
        {
            new ProgressStore().Reset(data);
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/DigDeep/Prefabs/DigDeepGame.prefab"));
            var game = instance.GetComponent<GameManager>(); game.settings = settings; MiningFeatureChecks.Tick(game, "Awake");
            var presenter = instance.GetComponentsInChildren<MonoBehaviour>(true).FirstOrDefault(m => m.GetType().Name == "PresentationResourcesButton");
            if (presenter != null) MiningFeatureChecks.Tick(presenter, "Awake");
            return instance;
        }
        static void Refresh(GameManager game) => MiningFeatureChecks.Tick(game.View, "LateUpdate");
        static void SameRect(Rect expected, Rect actual, string name)
            => Check(Mathf.Abs(expected.x - actual.x) < .02f && Mathf.Abs(expected.y - actual.y) < .02f && Mathf.Abs(expected.width - actual.width) < .02f && Mathf.Abs(expected.height - actual.height) < .02f, name);
        static void Focus(GameManager game, string action)
        {
            Refresh(game); var tutorial = game.GetComponent<TutorialView>(); var area = game.View.ButtonBounds(action);
            SameRect(area, tutorial.FocusBounds, action + " focus reads actual button bounds");
            var edge = game.View.UiRoot.Find("First play tutorial/Focus edge 0").GetComponent<RectTransform>();
            SameRect(new Rect(area.x - 2, area.y - 2, area.width + 4, 3), game.View.RootBounds(edge), action + " glowing top edge matches resized button");
        }
        static void Interface(GameSettings settings)
        {
            GameObject instance = null; var store = new ProgressStore();
            try
            {
                var data = ProgressData.Initial(settings); data.tutorial = TutorialProgress.Completed();
                instance = Spawn(settings, data); var game = instance.GetComponent<GameManager>();
                var component = instance.GetComponentsInChildren<MonoBehaviour>(true).FirstOrDefault(m => m.GetType().Name == "PresentationResourcesButton");
                if (component != null) MiningFeatureChecks.Tick(component, "Awake");
                Refresh(game);
                Check(game.shopSkin != null && game.inventorySkin != null && game.upgradeSkin != null && game.digSkin != null, "all new skins connected");
                SameRect(new Rect(204, 118, 80, 50), game.View.ButtonBounds("shop"), "shop wider with same height");
                SameRect(new Rect(292, 118, 80, 50), game.View.ButtonBounds("inventory"), "inventory rearranged");
                SameRect(new Rect(380, 118, 80, 50), game.View.ButtonBounds("upgrade"), "upgrade retains right boundary");
                Check(game.View.ActionAt(new Vector2(207, 140)) == "shop" && game.View.ActionAt(new Vector2(295, 140)) == "inventory" && game.View.ActionAt(new Vector2(383, 140)) == "upgrade", "newly widened button areas receive correct actions");
                Check(game.View.ActionAt(new Vector2(288, 140)) == "blocked" && !GameUILayout.Presentation.Overlaps(GameUILayout.Shop), "button gaps and presentation area do not overlap");
                var menuImporter = (TextureImporter)AssetImporter.GetAtPath("Assets/DigDeep/Art/MenuButtons.png");
                Check(!menuImporter.isReadable && menuImporter.maxTextureSize == 1024 && !menuImporter.mipmapEnabled, "UI atlas has bounded texture memory");
                var shopImage = game.View.UiRoot.Find("상점").GetComponent<Image>();
                float effectivePpu = shopImage.sprite.pixelsPerUnit / 100f * shopImage.pixelsPerUnitMultiplier;
                Check(Mathf.Abs(shopImage.sprite.border.x / effectivePpu - 37f) < 1f, "texture downscaling preserves the menu icon area and text spacing");
                MiningFeatureChecks.Capture(game, "60-buttons-ready");
                if (component != null)
                {
                    MiningFeatureChecks.Tick(component, "Awake"); Refresh(game);
                    var point = game.View.ScreenPoint(GameUILayout.Presentation.center);
                    game.Controls.Down(point); game.Controls.Up(point); Refresh(game);
                    Check(game.Session.Money == 100000 && game.Session.Gold == 100 && game.Session.State == SessionState.Ready && game.Session.Picks[0].Remaining == 20, "presentation button shares input without mining");
                    Check(store.Load(settings).gold == 100 && store.Load(settings).money == 100000, "presentation grant automatically persists");
                    MiningFeatureChecks.Capture(game, "61-presentation-grant");
                    game.Play(); Refresh(game); var before = game.Session.Money; game.HandleAction("testing-resources");
                    Check(game.Session.Money == before && !game.View.UiRoot.Find("Testing resource button").gameObject.activeSelf, "presentation button hidden and blocked during play");
                    game.Session.Strike(); Refresh(game);
                    Check(store.Load(settings).bestDepth == 1 && instance.GetComponentsInChildren<Text>(true).Any(t => t.text.StartsWith("최고 깊이 갱신!")), "record notice and save through manager");
                    MiningFeatureChecks.Capture(game, "62-record-broken"); game.Regroup();
                    MiningFeatureChecks.Tick(component, "OnDisable"); UnityEngine.Object.DestroyImmediate(component.gameObject); Refresh(game);
                    Check(game.View.ActionAt(GameUILayout.Presentation.center) == "blocked" && game.Session.Money == 100001 && game.Session.Gold == 100, "removing optional component removes button and preserves game data");
                    game.HandleAction("upgrade"); Check(game.Modal == GameModal.Upgrade, "regular UI still works after presentation removal"); game.HandleAction("close");
                }
                UnityEngine.Object.DestroyImmediate(instance); instance = null;
                data = ProgressData.Initial(settings); instance = Spawn(settings, data); game = instance.GetComponent<GameManager>(); Focus(game, "play");
                MiningFeatureChecks.Capture(game, "63-dig-tutorial-size");
                var presenter = instance.GetComponentsInChildren<MonoBehaviour>(true).FirstOrDefault(m => m.GetType().Name == "PresentationResourcesButton");
                if (presenter != null)
                {
                    MiningFeatureChecks.Tick(presenter, "Awake"); game.HandleAction("testing-resources");
                    Check(game.Tutorial.Prompt == TutorialPrompt.Dig && game.Session.Tutorial.controlsStep == 0 && game.Session.Money == 100000, "presentation grant does not skip tutorial");
                }
                for (int step = 0; step < 3; step++)
                {
                    UnityEngine.Object.DestroyImmediate(instance); instance = null;
                    data = ProgressData.Initial(settings); data.tutorial.controlsStep = 4; data.tutorial.firstGameOver = true; data.tutorial.menuStep = step;
                    instance = Spawn(settings, data); game = instance.GetComponent<GameManager>(); string action = step == 0 ? "inventory" : step == 1 ? "shop" : "upgrade";
                    Focus(game, action); MiningFeatureChecks.Capture(game, "64-focus-" + action);
                    game.View.UiRoot.localScale = Vector3.one * .6f; var tutorial = game.GetComponent<TutorialView>(); tutorial.Refresh();
                    SameRect(game.View.ButtonBounds(action), tutorial.FocusBounds, action + " focus matches when screen scale changes");
                }
                UnityEngine.Object.DestroyImmediate(instance); instance = null;
                data = ProgressData.Initial(settings); data.tutorial = TutorialProgress.Completed(); data.coal = 200; data.iron = 100; data.gold = 30;
                instance = Spawn(settings, data); game = instance.GetComponent<GameManager>(); game.Play(); while (game.Session.CanMine) game.Session.Strike(); Refresh(game);
                MiningFeatureChecks.Capture(game, "65-results-no-ores");
                var description = instance.GetComponentsInChildren<Text>(true).First(t => t.name == "Description").text;
                Check(description == "이번 판 도달 깊이\n20행", "result UI displays only current-run depth");
                UnityEngine.Object.DestroyImmediate(instance); instance = null;
                settings.woodDurability = 35;
                settings.spawnLines = new[] { new SpawnLine { firstRow = 1, lastRow = 1, coalPercent = 100 }, new SpawnLine { firstRow = 2, lastRow = 2, ironPercent = 100 }, new SpawnLine { firstRow = 3, lastRow = 3, goldPercent = 100 } };
                data = ProgressData.Initial(settings); data.tutorial = TutorialProgress.Completed();
                instance = Spawn(settings, data); game = instance.GetComponent<GameManager>(); game.Play(); while (game.Session.CanMine) game.Session.Strike(); Refresh(game);
                MiningFeatureChecks.Capture(game, "66-results-all-ores");
                var body = instance.GetComponentsInChildren<Text>(true).First(t => t.name == "Description");
                Check(body.preferredHeight <= body.rectTransform.rect.height && body.text == "이번 판 도달 깊이\n3행", "depth-only result fits above restart even when all mineral types were mined");
                game.Restart(); game.HandleAction("reset"); game.HandleAction("reset-next"); game.HandleAction("reset-confirm");
                Check(game.Session.BestDepth == 0 && store.Load(settings).bestDepth == 0 && game.Session.Money == 0, "two-confirmation reset clears record and resources together");
            }
            finally
            {
                settings.woodDurability = 20; settings.spawnLines = new SpawnLine[0];
                if (instance != null) UnityEngine.Object.DestroyImmediate(instance);
                foreach (var key in new[] { "DigDeep.Progress.v1.Verification", "DigDeep.Progress.backup.v1.Verification", "DigDeep.Money.v1.Verification" }) PlayerPrefs.DeleteKey(key);
                PlayerPrefs.Save();
            }
        }
    }
}
