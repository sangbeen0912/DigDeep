using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace DigDeep.Editor
{
    public static class RevisionChecks
    {
        static int assertions;
        static void Check(bool ok, string name) { assertions++; if (!ok) throw new Exception(name); }
        public static void Run()
        {
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "-digdeep-revision-test") < 0)
                throw new Exception("Use -digdeep-revision-test to isolate verification save data.");
            assertions = 0;
            GameObject instance = null;
            try
            {
                var settings = AssetDatabase.LoadAssetAtPath<GameSettings>("Assets/DigDeep/GameSettings.asset");
                var store = new ProgressStore(); store.Reset(ProgressData.Initial(settings));
                var data = ProgressData.Initial(settings, 123);
                data.picks[0].upgrade = 3; data.picks[0].power = 17;
                Check(store.Save(data), "growth save");
                var loaded = store.Load(settings);
                Check(loaded.money == 123 && loaded.picks.Count == 2 && loaded.picks[0].upgrade == 3 && loaded.picks[0].power == 17, "growth round trip");
                var run = new MiningSession(settings, loaded); run.Start(); run.Strike();
                int earned = run.Money; run.Regroup();
                Check(run.State == SessionState.Ready && run.Money == earned && run.Picks[0].Remaining == run.Picks[0].Maximum, "regroup without penalty");
                Check(run.Health(new Vector2Int(2, 0)) == settings.blockHealth && run.Position == new Vector2Int(2, -1), "regroup resets run");
                run.Start(); run.Strike();
                string json = JsonUtility.ToJson(run.Growth());
                Check(!json.Contains("Remaining") && !json.Contains("Position") && !json.Contains("Target"), "growth excludes session data");
                var fresh = new MiningSession(settings, run.Growth());
                Check(fresh.State == SessionState.Ready && fresh.Picks[0].Remaining == fresh.Picks[0].Maximum && fresh.Health(new Vector2Int(2, 0)) == settings.blockHealth, "reconnect prepares fresh run");
                Check(!ProgressData.TryParse("{}", out _) && !ProgressData.TryParse("broken", out _), "invalid saves rejected");
                instance = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/DigDeep/Prefabs/DigDeepGame.prefab"));
                var game = instance.GetComponent<GameManager>(); game.SendMessage("Awake");
                Check(game.Session.Money == 123, "manager restores growth");
                Check(game.View.ActionAt(new Vector2(240, 558)) == "play", "Dig button target");
                Check(game.View.ActionAt(new Vector2(325, 120)) == "shop" && game.View.ActionAt(new Vector2(420, 120)) == "upgrade", "separate interfaces");
                game.HandleAction("shop"); Check(game.Modal == GameModal.Shop, "shop opens");
                game.Play(); Check(game.Session.State == SessionState.Ready, "modal blocks play"); game.HandleAction("close");
                game.HandleAction("upgrade"); Check(game.Modal == GameModal.Upgrade, "upgrade opens"); game.HandleAction("close");
                Capture(game, "ready");
                game.Play();
                Check(game.View.ActionAt(new Vector2(420, 830)) == "blocked", "reset input disabled while playing");
                game.HandleAction("reset"); Check(game.Modal == GameModal.None, "reset command also guarded");
                game.HandleAction("reset-confirm"); Check(game.Session.Money == 123, "cannot bypass confirmation");
                game.Session.Strike(); int retained = game.Session.Money;
                Capture(game, "playing");
                game.HandleAction("regroup");
                Check(game.Session.State == SessionState.Ready && game.Session.Money == retained, "manager regroup");
                game.HandleAction("reset");
                Check(game.Modal == GameModal.ResetWarning && game.Session.Money == retained, "first warning does not reset");
                Capture(game, "reset-warning");
                game.HandleAction("close"); Check(game.Session.Money == retained, "first cancel preserves growth");
                game.HandleAction("reset"); game.HandleAction("reset-next");
                Check(game.Modal == GameModal.ResetConfirm && game.Session.Money == retained, "second warning does not reset");
                Capture(game, "reset-confirm");
                game.HandleAction("close"); Check(game.Session.Money == retained, "second cancel preserves growth");
                game.HandleAction("reset"); game.HandleAction("reset-next"); game.HandleAction("reset-confirm");
                Check(game.Session.Money == 0 && game.Session.Picks.Length == 2 && game.Session.Picks[0].Upgrade == 0 && game.Session.State == SessionState.Ready, "confirmed reset initial state, prototype ownership unchanged");
                game.SaveGrowth();
                Check(new ProgressStore().Load(settings).money == 0, "old money cannot return");
                Directory.CreateDirectory("Logs/Revision");
                File.WriteAllText("Logs/Revision/checks.txt", "PASS: " + assertions + " revision assertions; 21 mining rule checks. No player build.\n");
                Debug.Log("DIGDEEP_REVISION_CHECKS_PASSED " + assertions);
            }
            finally
            {
                if (instance != null) UnityEngine.Object.DestroyImmediate(instance);
                PlayerPrefs.DeleteKey("DigDeep.Progress.v1.Verification");
                PlayerPrefs.DeleteKey("DigDeep.Progress.backup.v1.Verification");
                PlayerPrefs.DeleteKey(GameSettings.MoneyKey + ".Verification"); PlayerPrefs.Save();
            }
        }

        static void Capture(GameManager game, string name)
        {
            game.View.SendMessage("LateUpdate");
            var canvas = game.GetComponentInChildren<Canvas>(); var camera = Camera.main;
            var target = new RenderTexture(480, 854, 24); target.Create();
            canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = camera; canvas.planeDistance = 1;
            camera.targetTexture = target; Canvas.ForceUpdateCanvases();
            var root = (RectTransform)canvas.transform.Find("Portrait 480x854"); root.localScale = Vector3.one; root.anchoredPosition = Vector2.zero;
            Canvas.ForceUpdateCanvases();
            RenderPipeline.SubmitRenderRequest(camera, new UniversalRenderPipeline.SingleCameraRequest { destination = target });
            var old = RenderTexture.active; RenderTexture.active = target;
            var image = new Texture2D(480, 854, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0, 0, 480, 854), 0, 0); image.Apply();
            Directory.CreateDirectory("Logs/Revision"); File.WriteAllBytes("Logs/Revision/" + name + ".png", image.EncodeToPNG());
            RenderTexture.active = old; camera.targetTexture = null; canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            UnityEngine.Object.DestroyImmediate(image); target.Release(); UnityEngine.Object.DestroyImmediate(target);
        }
    }
}
