using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
namespace DigDeep.Editor
{
    public static class ReadabilityChecks
    {
        static int checks;
        static void Check(bool value, string name) { checks++; if (!value) throw new Exception("Readability verification: " + name); }
        static void Refresh(GameManager game) { MiningFeatureChecks.Tick(game.View, "LateUpdate"); Canvas.ForceUpdateCanvases(); }
        static GameObject Spawn(GameSettings settings, ProgressData data)
        {
            new ProgressStore().Reset(data);
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/DigDeep/Prefabs/DigDeepGame.prefab"));
            var game = instance.GetComponent<GameManager>(); game.settings = settings; MiningFeatureChecks.Tick(game, "Awake"); Refresh(game); return instance;
        }
        static void Layout(GameManager game, string label)
        {
            Refresh(game); var view = game.GetComponent<TutorialView>(); var panel = view.MessageBounds;
            Check(panel.x >= 16 && panel.xMax <= 464 && panel.y >= 0 && panel.yMax <= 812, label + " stays inside the game area");
            Check(!panel.Overlaps(view.FocusBounds), label + " leaves its interactive target uncovered");
            var bubble = game.View.UiRoot.Find("First play tutorial/Tutorial explanation");
            Check(bubble.GetComponent<Image>().color.a == 1, label + " has an opaque high-contrast background");
            foreach (var text in bubble.GetComponentsInChildren<Text>())
                Check(text.preferredWidth <= text.rectTransform.rect.width + .5f && text.preferredHeight <= text.rectTransform.rect.height + .5f, label + " fits " + text.name);
            var main = bubble.Find("Tutorial instructions").GetComponent<Text>();
            Check(main.fontSize >= 22 && main.fontStyle == FontStyle.Bold && main.alignment == TextAnchor.MiddleLeft, label + " has large bold left-aligned action text");
            Check(game.View.UiRoot.Find("First play tutorial/Focus edge 0").GetComponent<Image>().color.a >= .79f, label + " target outline remains visible");
        }
        public static void Run()
        {
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "-digdeep-revision-test") < 0) throw new Exception("Isolated verification flag required");
            EditorSceneManager.OpenScene(DigDeepSetup.ScenePath); checks = 0;
            var settings = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameSettings>("Assets/DigDeep/GameSettings.asset")); settings.spawnLines = new SpawnLine[0];
            GameObject instance = null;
            try
            {
                var data = ProgressData.Initial(settings); data.tutorial = TutorialProgress.Completed(); data.picks[0].id = "ALLOY"; data.money = 1000; data.diamond = 20; data.energyTenths = 50;
                instance = Spawn(settings, data); var game = instance.GetComponent<GameManager>(); string before = JsonUtility.ToJson(game.Session.Growth());
                Check(game.moneyIcon != null && game.energyIcon != null && game.moneyIcon != game.energyIcon, "both distinct currency sprites are attached");
                foreach (string asset in new[] { "MoneyIcon", "EnergyIcon" })
                { var importer = (TextureImporter)AssetImporter.GetAtPath("Assets/DigDeep/Art/" + asset + ".png"); Check(importer.maxTextureSize == 128 && !importer.isReadable && !importer.mipmapEnabled, asset + " uses bounded UI texture memory"); }
                game.HandleAction("upgrade"); game.HandleAction("upgrade-pick0"); Refresh(game);
                var rows = game.View.UiRoot.Find("Trading and confirmation/Confirmation content/Readable requirements");
                var money = rows.Find("Requirement row 0/Icon").GetComponent<Image>(); var energy = rows.Find("Requirement row 2/Icon").GetComponent<Image>();
                Check(money.sprite == game.moneyIcon && energy.sprite == game.energyIcon && money.color == Color.white && energy.color == Color.white, "upgrade requirements display real untinted money and energy icons");
                Check(money.preserveAspect && energy.preserveAspect, "currency icons keep their proportions");
                MiningFeatureChecks.Capture(game, "80-currency-requirement-icons");
                game.HandleAction("close"); game.HandleAction("close"); game.HandleAction("inventory"); Refresh(game);
                Check(game.View.UiRoot.Find("Trading and confirmation/Inventory content/Energy inventory card/Energy icon").GetComponent<Image>().sprite == game.energyIcon, "inventory reuses the same energy symbol");
                Check(before == JsonUtility.ToJson(game.Session.Growth()), "visual UI changes leave resources and tutorial progress intact");
                MiningFeatureChecks.Capture(game, "81-energy-inventory-icon");
                UnityEngine.Object.DestroyImmediate(instance); instance = null;
                instance = Spawn(settings, ProgressData.Initial(settings)); game = instance.GetComponent<GameManager>(); Layout(game, "Dig");
                Check(!game.View.UiRoot.Find("Start hint").gameObject.activeSelf, "Dig tutorial removes the overlapping duplicate hint");
                Check(game.View.ActionAt(GameUILayout.Dig.center) == "play", "larger Dig tutorial leaves start button usable");
                MiningFeatureChecks.Capture(game, "82-readable-dig-tutorial");
                for (int step = 1; step <= 3; step++)
                {
                    UnityEngine.Object.DestroyImmediate(instance); instance = null; data = ProgressData.Initial(settings); data.tutorial.controlsStep = step;
                    instance = Spawn(settings, data); game = instance.GetComponent<GameManager>(); game.Play(); Layout(game, "Control " + step);
                    Check(!game.GetComponent<TutorialView>().BlocksPoint(new Vector2(100, 660)), "control " + step + " keeps the lower mining area available");
                    if (step == 2) Check(!game.GetComponent<TutorialView>().MessageBounds.Overlaps(game.View.CellBounds(game.Tutorial.DragDestination)), "drag instruction does not cover the destination");
                    MiningFeatureChecks.Capture(game, "83-readable-control-" + step);
                }
                UnityEngine.Object.DestroyImmediate(instance); instance = null; data = ProgressData.Initial(settings); data.tutorial.controlsStep = 4;
                instance = Spawn(settings, data); game = instance.GetComponent<GameManager>(); game.Play(); while (game.Session.CanMine) game.Session.Strike(); Layout(game, "Restart");
                Check(game.View.ActionAt(new Vector2(240, 532)) == "restart", "larger end tutorial leaves restart button usable");
                for (int step = 0; step < 3; step++)
                {
                    UnityEngine.Object.DestroyImmediate(instance); instance = null; data = ProgressData.Initial(settings); data.tutorial.controlsStep = 4; data.tutorial.firstGameOver = true; data.tutorial.menuStep = step;
                    instance = Spawn(settings, data); game = instance.GetComponent<GameManager>(); string action = step == 0 ? "inventory" : step == 1 ? "shop" : "upgrade";
                    Layout(game, action + " button"); game.HandleAction(action); Layout(game, action + " open");
                    Check(!game.GetComponent<TutorialView>().MessageBounds.Overlaps(new Rect(25, 202, 430, 530)), action + " tutorial leaves the actual menu panel visible");
                    MiningFeatureChecks.Capture(game, "84-readable-" + action + "-tutorial");
                    if (step == 2) { game.HandleAction("upgrade-pick0"); Layout(game, "upgrade requirements"); MiningFeatureChecks.Capture(game, "85-readable-upgrade-requirements-tutorial"); }
                    Check(game.View.ActionAt(new Vector2(240, 702)) == "close", action + " tutorial leaves back action usable");
                }
                UnityEngine.Object.DestroyImmediate(instance); instance = null; data = ProgressData.Initial(settings); data.tutorial = TutorialProgress.Completed(); data.tutorial.switchDone = false; data.picks.Add(new PickProgress { id = "WOOD" });
                instance = Spawn(settings, data); game = instance.GetComponent<GameManager>(); game.Play(); for (int i = 0; i < 20; i++) game.Session.Strike(); Layout(game, "Switch");
                Check(game.View.ActionAt(new Vector2(98, 50)) == "pick1", "large switch message keeps the usable tool selectable");
                MiningFeatureChecks.Capture(game, "86-readable-switch-tutorial"); game.SelectPick(1); Refresh(game);
                Check(game.Tutorial.Prompt == TutorialPrompt.None && !game.GetComponent<TutorialView>().BlocksPoint(new Vector2(100, 700)), "completed tutorial panel no longer blocks input");
            }
            finally
            {
                if (instance != null) UnityEngine.Object.DestroyImmediate(instance); UnityEngine.Object.DestroyImmediate(settings);
                foreach (string key in new[] { "DigDeep.Progress.v1.Verification", "DigDeep.Progress.backup.v1.Verification", "DigDeep.Money.v1.Verification" }) PlayerPrefs.DeleteKey(key);
                PlayerPrefs.Save();
            }
            File.WriteAllText("Logs/MiningUpgrade/readability-checks.txt", "Existing checks: 303\nReadability checks: " + checks + "\nNo player build in these checks.\n");
            Debug.Log("DIGDEEP_READABILITY_PASSED " + checks);
        }
    }
}
