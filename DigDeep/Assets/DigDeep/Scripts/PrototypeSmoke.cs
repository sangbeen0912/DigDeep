using System;
using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace DigDeep
{
    // Opt-in build verification only; never runs for ordinary players.
    public sealed class PrototypeSmoke : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Begin()
        {
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "-digdeep-smoke") >= 0)
            {
                Application.runInBackground = true;
                new GameObject("Smoke verification").AddComponent<PrototypeSmoke>();
            }
        }

        IEnumerator Start()
        {
            yield return null;
            var game = FindFirstObjectByType<GameManager>();
            if (game == null) { Fail("manager missing"); yield break; }
            int savedMoney = game.Session.Money;
            Directory.CreateDirectory("Builds/Verification");
            yield return new WaitForSecondsRealtime(.3f);
            Capture("Builds/Verification/01-ready.png");
            yield return new WaitForSecondsRealtime(.3f);
            var play = game.View.ScreenPoint(new Vector2(240, 530));
            game.Controls.Down(play); game.Controls.Up(play);
            if (game.Session.State != SessionState.Playing) { Fail("PLAY input routing"); yield break; }
            var mine = game.View.ScreenPoint(new Vector2(100, 700));
            int initial = game.Session.Picks[0].Remaining;
            game.Controls.Down(mine); game.Controls.Up(mine);
            yield return new WaitForSecondsRealtime(.4f);
            if (game.Session.Picks[0].Remaining != initial - game.settings.woodPower) { Fail("tap must hit exactly once"); yield break; }
            game.Controls.Down(mine);
            yield return new WaitForSecondsRealtime(game.settings.holdDelay + .05f);
            game.Controls.Move(mine);
            yield return new WaitForSecondsRealtime(1.2f);
            game.Controls.Up(mine);
            int stopped = game.Session.Picks[0].Remaining;
            yield return new WaitForSecondsRealtime(.5f);
            if (game.Session.Picks[0].Remaining != stopped) { Fail("hold release must stop damage"); yield break; }
            // UI tap must not also cause a strike.
            var card = game.View.ScreenPoint(new Vector2(135, 50));
            game.Controls.Down(card); game.Controls.Up(card);
            yield return new WaitForSecondsRealtime(.4f);
            if (game.Session.Selected != 1 || game.Session.Picks[1].Remaining != game.Session.Picks[1].Maximum)
            { Fail("UI selection click-through"); yield break; }
            game.SelectPick(0);
            for (int i = 0; i < 8; i++) game.Session.Strike();
            yield return new WaitForSecondsRealtime(.6f);
            Capture("Builds/Verification/02-mining.png");
            yield return new WaitForSecondsRealtime(.3f);
            while (game.Session.CanMine) game.Session.Strike();
            if (game.Session.State != SessionState.Playing) { Fail("manual switch should remain available"); yield break; }
            game.SelectPick(1);
            while (game.Session.CanMine) game.Session.Strike();
            yield return new WaitForSecondsRealtime(.3f);
            if (game.Session.State != SessionState.GameOver) { Fail("all exhausted game over"); yield break; }
            int earned = game.Session.Money;
            Capture("Builds/Verification/03-gameover.png");
            yield return new WaitForSecondsRealtime(.3f);
            game.Restart();
            if (game.Session.Money != earned || game.Session.Picks[0].Remaining != initial)
            { Fail("restart money and durability"); yield break; }
            // Smoke runs must not award money to the user's real save.
            PlayerPrefs.SetInt(GameSettings.MoneyKey, savedMoney); PlayerPrefs.Save();
            File.WriteAllText("Builds/Verification/smoke-result.txt", "PASS: start, tap, hold release, UI isolation, manual selection, game over, restart, persistent money.\n");
            Debug.Log("DIGDEEP_SMOKE_PASS");
            Application.Quit(0);
        }
        static void Fail(string message) { Debug.LogError("DIGDEEP_SMOKE_FAIL " + message); Application.Quit(1); }

        static void Capture(string path)
        {
            var canvas = FindFirstObjectByType<Canvas>();
            var camera = Camera.main;
            var target = new RenderTexture(480, 854, 24);
            target.Create();
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = camera; canvas.planeDistance = 1;
            camera.targetTexture = target;
            Canvas.ForceUpdateCanvases();
            RenderPipeline.SubmitRenderRequest(camera, new UniversalRenderPipeline.SingleCameraRequest { destination = target });
            var old = RenderTexture.active;
            RenderTexture.active = target;
            var texture = new Texture2D(480, 854, TextureFormat.RGB24, false);
            texture.ReadPixels(new Rect(0, 0, 480, 854), 0, 0); texture.Apply();
            File.WriteAllBytes(path, texture.EncodeToPNG());
            RenderTexture.active = old; camera.targetTexture = null;
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            Destroy(texture); target.Release(); Destroy(target);
        }
    }
}
