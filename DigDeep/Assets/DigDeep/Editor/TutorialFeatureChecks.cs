using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
namespace DigDeep.Editor
{
    public static class TutorialFeatureChecks
    {
        static int checks;
        static readonly BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        static void Check(bool value, string message) { checks++; if (!value) throw new Exception("Tutorial verification: " + message); }
        public static void ApplyAndVerify()
        {
            MiningContentSetup.ApplyAndVerify();
            Run();
        }
        public static void Run()
        {
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "-digdeep-revision-test") < 0) throw new Exception("Isolated save flag required");
            checks = 0; var settings = AssetDatabase.LoadAssetAtPath<GameSettings>("Assets/DigDeep/GameSettings.asset");
            var config = UnityEngine.Object.Instantiate(settings); config.spawnLines = new SpawnLine[0];
            var store = new ProgressStore(); GameObject instance = null;
            try
            {
                var data = ProgressData.Initial(config); store.Reset(data); instance = Spawn(config); var game = instance.GetComponent<GameManager>();
                Check(game.Tutorial.Prompt == TutorialPrompt.Dig && game.View.ActionAt(new Vector2(280, 140)) == "blocked", "first launch forces Dig before commerce");
                MiningFeatureChecks.Capture(game, "20-tutorial-dig"); Click(game, new Vector2(240, 560)); Refresh(game);
                Check(game.Tutorial.Prompt == TutorialPrompt.Tap && store.Load(config).tutorial.controlsStep == 1, "Dig checkpoint saved");
                game.Hold(); Check(!game.IsSwinging, "hold cannot bypass tap lesson");
                var mine = game.View.ScreenPoint(new Vector2(100, 660)); game.Controls.Down(mine); game.Controls.Up(mine); Swing(game); Refresh(game);
                Check(game.Session.Tutorial.controlsStep == 2 && game.Session.Money == 1, "successful tap advances to drag");
                MiningFeatureChecks.Capture(game, "21-tutorial-drag");
                var pick = game.View.PickBounds().center; var destination = game.View.CellBounds(game.Tutorial.DragDestination).center;
                game.Controls.Down(game.View.ScreenPoint(pick)); game.Controls.Move(game.View.ScreenPoint(destination)); game.Controls.Up(game.View.ScreenPoint(destination));
                Check(game.Session.Tutorial.controlsStep == 2, "solid drop waits for actual arrival"); Swing(game); Refresh(game);
                Check(game.Session.Tutorial.controlsStep == 3 && game.Session.Position.x == 3, "drag arrival advances to hold");
                MiningFeatureChecks.Capture(game, "22-tutorial-hold");
                game.Regroup(); Check(!game.Session.Tutorial.firstGameOver && game.Session.Tutorial.controlsStep == 3 && game.Tutorial.Prompt == TutorialPrompt.Dig, "regroup does not count as first game over or replay completed lessons");
                UnityEngine.Object.DestroyImmediate(instance); instance = Spawn(config); game = instance.GetComponent<GameManager>();
                Check(game.Session.Tutorial.controlsStep == 3 && game.Session.State == SessionState.Ready && game.Session.Picks[0].Remaining == 20, "reconnect restores only checkpoint and fresh run");
                game.Play(); Refresh(game); Check(game.Tutorial.Prompt == TutorialPrompt.Hold, "reconnect resumes hold without tap or drag repetition");
                mine = game.View.ScreenPoint(new Vector2(100, 660)); game.Controls.Down(mine);
                typeof(PointerController).GetField("started", Private).SetValue(game.Controls, Time.unscaledTime - config.holdDelay - .01f);
                game.Controls.Move(mine); Swing(game); Swing(game);
                Check(game.Session.Tutorial.controlsStep == 3, "hold awaits finger release");
                game.Controls.Up(mine); Refresh(game);
                Check(game.Session.Tutorial.controlsStep == 4 && !game.IsSwinging && store.Load(config).tutorial.controlsStep == 4, "hold release finishes and saves controls");
                game.Regroup(); Check(!game.Session.Tutorial.firstGameOver && game.Tutorial.Prompt == TutorialPrompt.None, "regroup after controls still does not start UI lessons");
                game.Play(); while (game.Session.CanMine) game.Session.Strike(); Refresh(game);
                Check(game.Session.Tutorial.firstGameOver && game.Tutorial.Prompt == TutorialPrompt.Restart, "only actual game over unlocks UI tour");
                game.Restart(); Refresh(game); Check(game.Tutorial.Prompt == TutorialPrompt.Inventory, "inventory is first UI lesson");
                game.HandleAction("shop"); Check(game.Modal == GameModal.None, "cannot skip inventory to shop");
                game.HandleAction("inventory"); MiningFeatureChecks.Capture(game, "23-tutorial-inventory");
                game.HandleAction("close"); Check(store.Load(config).tutorial.menuStep == 1, "inventory explanation saves when closed");
                UnityEngine.Object.DestroyImmediate(instance); instance = Spawn(config); game = instance.GetComponent<GameManager>();
                Check(game.Tutorial.Prompt == TutorialPrompt.Shop, "reconnect resumes only unfinished UI lesson");
                game.HandleAction("shop"); MiningFeatureChecks.Capture(game, "24-tutorial-shop"); game.HandleAction("close");
                Check(game.Tutorial.Prompt == TutorialPrompt.Upgrade && game.Session.Picks.Length == 1, "shop tour completes without forced purchase");
                int money = game.Session.Money; game.HandleAction("upgrade"); game.HandleAction("close"); Check(game.Modal == GameModal.Upgrade, "upgrade tour asks user to select a pick");
                game.HandleAction("upgrade-pick0"); MiningFeatureChecks.Capture(game, "25-tutorial-upgrade"); game.HandleAction("close");
                Check(game.Session.Tutorial.menuStep == 3 && game.Session.Money == money && game.Tutorial.Prompt == TutorialPrompt.None, "upgrade tour completes even without materials and no forced trade");
                game.HandleAction("close"); game.Play(); while (game.Session.CanMine) game.Session.Strike(); game.Restart();
                Check(game.Tutorial.Prompt == TutorialPrompt.None && store.Load(config).tutorial.menuStep == 3, "completed tutorials do not replay on later game over");
                UnityEngine.Object.DestroyImmediate(instance); instance = null;
                Switching(config, store); DropAndEffects(config, store);
                Check(ProgressData.TryParse("{\"version\":3,\"money\":33,\"picks\":[{\"id\":\"WOOD\"}]}", out var legacy) && legacy.version == 7 && legacy.money == 33 && legacy.tutorial.controlsStep == 0, "legacy growth preserved with new tutorial checkpoints");
                Check(!ProgressData.TryParse("{\"version\":4,\"tutorial\":{\"controlsStep\":8},\"picks\":[{\"id\":\"WOOD\"}]}", out _), "invalid tutorial step cannot load");
                Directory.CreateDirectory("Logs/Tutorial"); File.WriteAllText("Logs/Tutorial/checks.txt", "PASS " + checks + " tutorial/feedback checks. Existing mining checks: Logs/MiningUpgrade/checks.txt. No build.\n");
                Debug.Log("DIGDEEP_TUTORIAL_FEEDBACK_PASSED " + checks);
            }
            finally
            {
                if (instance != null) UnityEngine.Object.DestroyImmediate(instance); UnityEngine.Object.DestroyImmediate(config);
                PlayerPrefs.DeleteKey("DigDeep.Progress.v1.Verification"); PlayerPrefs.DeleteKey("DigDeep.Progress.backup.v1.Verification"); PlayerPrefs.DeleteKey(GameSettings.MoneyKey + ".Verification"); PlayerPrefs.Save();
            }
        }
        static void Switching(GameSettings config, ProgressStore store)
        {
            var data = ProgressData.Initial(config, 100); data.tutorial = TutorialProgress.Completed(); data.tutorial.switchDone = false; store.Reset(data);
            var instance = Spawn(config); var game = instance.GetComponent<GameManager>();
            try
            {
                game.HandleAction("shop"); game.HandleAction("buy"); game.HandleAction("close"); game.Play(); while (game.Session.CanMine) game.Session.Strike(); Refresh(game);
                Check(game.Session.State == SessionState.Playing && game.Tutorial.Prompt == TutorialPrompt.Switch, "first purchase enables contextual switch tutorial on depletion");
                MiningFeatureChecks.Capture(game, "26-tutorial-switch"); Click(game, new Vector2(98, 50));
                Check(game.Session.Selected == 1 && game.Session.Tutorial.switchDone && store.Load(config).tutorial.switchDone, "valid usable pick click completes switch tutorial");
                while (game.Session.CanMine) game.Session.Strike();
                Check(game.Tutorial.Prompt == TutorialPrompt.None && game.Session.State == SessionState.GameOver, "all depleted uses game over rather than impossible switch");
                game.Restart(); game.Play(); while (game.Session.CanMine) game.Session.Strike(); Check(game.Tutorial.Prompt == TutorialPrompt.None, "switch guidance never repeats after completion");
                game.Regroup(); game.HandleAction("reset"); game.HandleAction("reset-next"); game.HandleAction("reset-confirm");
                var saved = store.Load(config);
                Check(saved.tutorial.controlsStep == 0 && saved.tutorial.menuStep == 0 && !saved.tutorial.firstGameOver && !saved.tutorial.switchDone && saved.picks.Count == 1, "two-stage reset clears every tutorial checkpoint");
            }
            finally { UnityEngine.Object.DestroyImmediate(instance); }
        }
        static void DropAndEffects(GameSettings baseConfig, ProgressStore store)
        {
            var config = UnityEngine.Object.Instantiate(baseConfig); config.woodDurability = 1000;
            var data = ProgressData.Initial(config); data.tutorial = TutorialProgress.Completed(); store.Reset(data);
            var instance = Spawn(config); var game = instance.GetComponent<GameManager>();
            try
            {
                game.Play(); for (int i = 0; i < 8; i++) game.Session.Strike(); int durability = game.Session.Picks[0].Remaining; int money = game.Session.Money; var position = game.Session.Position;
                game.Drop(new Vector2Int(0, 7)); Refresh(game);
                Check(game.Session.LastDropFailure == DropFailure.Blocked && Notice(game) == "길이 막혀 있어요.", "blocked path has exact persistent failure feedback");
                game.Drop(new Vector2Int(2, 1)); Refresh(game);
                Check(game.Session.LastDropFailure == DropFailure.OutOfRange && Notice(game) == "이동 범위를 벗어났어요.", "six-row move has range feedback after tutorial completion");
                Check(game.Session.Position == position && game.Session.Money == money && game.Session.Picks[0].Remaining == durability, "invalid drops do not move or charge durability");
                MiningFeatureChecks.Capture(game, "27-drop-range-feedback");
            }
            finally { UnityEngine.Object.DestroyImmediate(instance); }
            try
            {
                config.spawnLines = new[] { new SpawnLine { firstRow = 1, lastRow = 1000, coalPercent = 100 } };
                data = ProgressData.Initial(config); data.tutorial = TutorialProgress.Completed(); store.Reset(data); instance = Spawn(config); game = instance.GetComponent<GameManager>(); game.Play();
                game.Session.Strike(); Refresh(game); Check(RewardCount(game, "석탄 +1") == 0, "partial coal hit does not display fake loot");
                for (int i = 0; i < 4; i++) game.Session.Strike(); Refresh(game);
                Check(RewardCount(game, "석탄 +1") == 1 && game.Session.Coal == 1, "destroyed coal creates one colored loot label");
                Check(RewardTint(game, "석탄 +1") == GameView.OreColor(Mineral.Coal), "coal loot label uses coal image palette");
                MiningFeatureChecks.Capture(game, "28-coal-feedback"); int objects = instance.GetComponentsInChildren<Transform>(true).Length;
                for (int i = 0; i < 200; i++) game.Session.Strike(); Refresh(game);
                Check(instance.GetComponentsInChildren<Transform>(true).Length == objects && RewardCount(game, "석탄 +1") <= 10, "long mining reuses bounded label and particle pools without object growth");
                game.Regroup(); Refresh(game); Check(RewardCount(game, "석탄 +1") == 0, "regroup clears transient feedback");
            }
            finally { UnityEngine.Object.DestroyImmediate(instance); }
            try
            {
                config.spawnLines = new[] { new SpawnLine { firstRow = 1, lastRow = 35, ironPercent = 100 } };
                data = ProgressData.Initial(config); data.picks[0].id = "IRON"; data.tutorial = TutorialProgress.Completed(); store.Reset(data); instance = Spawn(config); game = instance.GetComponent<GameManager>(); game.Play();
                for (int i = 0; i < 8; i++) game.Session.Strike(); Refresh(game);
                Check(RewardCount(game, "철광석 +1") == 1 && RewardTint(game, "철광석 +1") == GameView.OreColor(Mineral.Iron), "iron acquisition uses its copper-colored label");
                MiningFeatureChecks.Capture(game, "29-iron-feedback");
            }
            finally { UnityEngine.Object.DestroyImmediate(instance); UnityEngine.Object.DestroyImmediate(config); }
        }
        static string Notice(GameManager game) => game.GetComponentInChildren<Canvas>().transform.Find("Portrait 480x854/Mining notice panel/Mining notice").GetComponent<Text>().text;
        static int RewardCount(GameManager game, string value) { int count = 0; foreach (var text in game.GetComponentsInChildren<Text>()) if (text.text == value) count++; return count; }
        static Color RewardTint(GameManager game, string value) { foreach (var text in game.GetComponentsInChildren<Text>()) if (text.text == value) return text.color; return Color.clear; }
        static GameObject Spawn(GameSettings settings)
        {
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/DigDeep/Prefabs/DigDeepGame.prefab"));
            var game = instance.GetComponent<GameManager>(); game.settings = settings; MiningFeatureChecks.Tick(game, "Awake"); Refresh(game); return instance;
        }
        static void Refresh(GameManager game) { MiningFeatureChecks.Tick(game.View, "LateUpdate"); Canvas.ForceUpdateCanvases(); }
        static void Click(GameManager game, Vector2 point) { var screen = game.View.ScreenPoint(point); game.Controls.Down(screen); game.Controls.Up(screen); }
        static void Swing(GameManager game) { typeof(GameManager).GetField("swingStarted", Private).SetValue(game, Time.unscaledTime - game.settings.strikeInterval - .01f); MiningFeatureChecks.Tick(game, "Update"); }
    }
}
