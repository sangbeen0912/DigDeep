using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace DigDeep
{
    public sealed class GameView : MonoBehaviour
    {
        const float Width = 480, Height = 854, Pitch = 88, Left = 20, FieldTop = 180, Surface = 298;
        public const float IdlePickAngle = -35;
        GameManager game;
        RectTransform root, field, world, pickRect, marker, overlay;
        Image pickImage, markerImage, readyButton, regroupButton, shopButton, inventoryButton, upgradeButton, resetButton;
        GameMenus menus;
        TutorialView tutorialView;
        Text notice, boomText;
        BlastBurstGraphic boom;
        float boomTime = -10;
        int shownMoney = -1, shownDepth = -1, shownBest = -1;
        readonly int[] shownRemaining = { -1, -1, -1, -1 };
        readonly int[] shownMaximum = { -1, -1, -1, -1 };
        Image noticePanel, impactFlash;
        float noticeUntil;
        Color noticeColor;
        sealed class Reward { public Text Text; public Vector2 Start; public float Born = -10; }
        readonly Reward[] rewards = new Reward[10];
        int nextReward;
        Text readyHint;
        Text money, status, depth, title, detail, mainLabel;
        Image[] cards = new Image[4], bars = new Image[4];
        Image[] icons = new Image[4];
        Text[] durability = new Text[4];
        readonly List<Tile> tiles = new List<Tile>();
        readonly List<Chip> chips = new List<Chip>();
        readonly List<Image> routeMarkers = new List<Image>();
        readonly Dictionary<string, Rect> actions = new Dictionary<string, Rect>();
        readonly Dictionary<string, Image> buttons = new Dictionary<string, Image>();
        sealed class OptionalButton { public RectTransform Target; public Func<bool> Available; public Action Execute; }
        readonly Dictionary<string, OptionalButton> optionalButtons = new Dictionary<string, OptionalButton>();
        readonly Vector3[] boundCorners = new Vector3[4];
        public RectTransform UiRoot => root;
        Font font;
        bool dragging;
        Vector2 dragPoint;
        float scroll, impactTime = -10;
        Vector2Int impactCell;
        Color dark = new Color32(19, 27, 34, 255), gold = new Color32(239, 189, 88, 255);
        sealed class Tile { public RectTransform Rect; public Image Image; public Image[] Cracks; public Image Health; }
        sealed class Chip { public RectTransform Rect; public Image Image; public Vector2 Velocity, Start; public Color Tint; public float Born; }

        public void Initialize(GameManager owner)
        {
            game = owner;
            font = game.font != null ? game.font : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            var canvasObject = new GameObject("Game Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            canvasObject.transform.SetParent(transform, false);
            canvasObject.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
            var backdrop = Box(canvasObject.transform, "Letterbox", new Rect(0, 0, Width, Height), dark);
            var bg = backdrop.rectTransform; bg.anchorMin = Vector2.zero; bg.anchorMax = Vector2.one; bg.offsetMin = bg.offsetMax = Vector2.zero;
            root = RectNode(canvasObject.transform, "Portrait 480x854", new Rect(0, 0, Width, Height));
            root.anchorMin = root.anchorMax = new Vector2(.5f, .5f); root.pivot = new Vector2(.5f, .5f);
            Box(root, "Background", new Rect(0, 0, Width, Height), new Color32(10, 19, 26, 255));
            field = RectNode(root, "Mining viewport", new Rect(0, FieldTop, Width, Height - FieldTop - 42));
            field.gameObject.AddComponent<RectMask2D>();
            world = RectNode(field, "Scrolling terrain", new Rect(0, -FieldTop, Width, Height * 2));
            Box(world, "Sky", new Rect(Left, 0, Pitch * 5, Surface), new Color32(89, 171, 195, 255));
            Cloud(new Rect(45, 194, 64, 9)); Cloud(new Rect(338, 172, 90, 10));
            // A bounded pool renders the visible slice of an otherwise lazy, unbounded mine.
            for (int i = 0; i < 60; i++)
            {
                var image = Box(world, "Block " + i, new Rect(0, 0, Pitch - 2, Pitch - 2), Color.white, game.stone);
                var tile = new Tile { Rect = image.rectTransform, Image = image, Cracks = new Image[3] };
                for (int j = 0; j < 3; j++)
                {
                    tile.Cracks[j] = Box(tile.Rect, "Crack " + j, new Rect(30 + j * 7, 24 + j * 13, 4, 20), new Color32(24, 29, 32, 255));
                    tile.Cracks[j].rectTransform.localEulerAngles = new Vector3(0, 0, j % 2 == 0 ? 35 : -35);
                }
                tile.Health = Box(tile.Rect, "Block HP", new Rect(10, 77, 66, 3), gold);
                tiles.Add(tile);
            }
            for (int i = 0; i < 24; i++)
            {
                var image = Box(world, "Reachable route", new Rect(0, 0, 10, 10), new Color32(123, 211, 166, 150));
                image.gameObject.SetActive(false); routeMarkers.Add(image);
            }
            markerImage = Box(world, "Drop target", new Rect(0, 0, Pitch, Pitch), new Color(0.4f, 1f, .7f, .3f));
            marker = markerImage.rectTransform; marker.gameObject.SetActive(false);
            pickImage = Box(world, "Active pickaxe", new Rect(0, 0, 76, 76), Color.white, game.wood);
            pickRect = pickImage.rectTransform; pickImage.preserveAspect = true;
            pickRect.pivot = new Vector2(.5f, .5f);
            for (int i = 0; i < 12; i++)
            {
                var image = Box(world, "Stone particle", new Rect(0, 0, 5 + i % 4, 5 + i % 4), new Color32(157, 153, 141, 255));
                image.gameObject.SetActive(false); chips.Add(new Chip { Rect = image.rectTransform, Image = image, Born = -10 });
            }
            impactFlash = Box(world, "Impact flash", new Rect(0, 0, 86, 86), Color.white); impactFlash.gameObject.SetActive(false);
            for (int i = 0; i < rewards.Length; i++)
            {
                var text = Label(world, "Ore reward " + i, new Rect(0, 0, 150, 35), "", 20, Color.white, TextAnchor.MiddleCenter);
                var outline = text.gameObject.AddComponent<Outline>(); outline.effectColor = new Color32(236, 241, 233, 245); outline.effectDistance = new Vector2(1, -1);
                text.gameObject.SetActive(false); rewards[i] = new Reward { Text = text };
            }
            var boomRect = RectNode(world, "Reusable explosion", new Rect(0, 0, 202, 160)); boomRect.pivot = new Vector2(.5f, .5f);
            boom = boomRect.gameObject.AddComponent<BlastBurstGraphic>(); boom.raycastTarget = false;
            boomText = Label(boomRect, "BOOM text", new Rect(1, 28, 200, 90), "BOOM!", 48, new Color32(255, 223, 25, 255), TextAnchor.MiddleCenter);
            boomText.fontStyle = FontStyle.Bold; var boomOutline = boomText.gameObject.AddComponent<Outline>(); boomOutline.effectColor = Color.black; boomOutline.effectDistance = new Vector2(3, -3);
            boom.gameObject.SetActive(false);
            Box(root, "HUD", new Rect(0, 0, Width, FieldTop), dark);
            Box(root, "HUD underline", new Rect(20, 178, 440, 2), new Color32(49, 65, 72, 255));
            for (int i = 0; i < 4; i++)
            {
                Rect rect = new Rect(20 + i * 54, 15, 49, 77);
                cards[i] = Box(root, "Pickaxe slot " + i, rect, dark);
                Box(cards[i].transform, "Slot inset", new Rect(3, 3, 43, 71), new Color32(35, 45, 52, 255));
                icons[i] = Box(cards[i].transform, "Pickaxe", new Rect(5, 5, 39, 39), Color.white, i == 0 ? game.wood : game.iron);
                TiltPickIcon(icons[i]);
                Box(cards[i].transform, "Durability track", new Rect(5, 46, 39, 5), new Color32(8, 16, 21, 255));
                bars[i] = Box(cards[i].transform, "Durability fill", new Rect(5, 46, 39, 5), new Color32(125, 210, 171, 255));
                durability[i] = Label(cards[i].transform, "Durability", new Rect(0, 55, 49, 15), "", 10, Color.white, TextAnchor.MiddleCenter);
                actions["pick" + i] = rect;
            }
            Label(root, "Wallet caption", new Rect(295, 22, 160, 17), "보유 금액", 12, new Color32(151, 167, 175, 255), TextAnchor.MiddleRight);
            money = Label(root, "Wallet", new Rect(255, 42, 200, 32), "0 G", 25, gold, TextAnchor.MiddleRight);
            depth = Label(root, "Depth", new Rect(285, 80, 169, 31), "", 10, new Color32(151, 167, 175, 255), TextAnchor.MiddleRight);
            Box(root, "Footer", new Rect(0, Height - 42, Width, 42), dark);
            status = Label(root, "Instructions", new Rect(15, Height - 39, 325, 34), "", 10, new Color32(180, 203, 210, 255), TextAnchor.MiddleLeft);
            regroupButton = Button(root, "재정비", new Rect(20, 118, 208, 50), new Color32(47, 69, 76, 255), Color.white);
            shopButton = SkinnedButton("shop", "상점", GameUILayout.Shop, game.shopSkin);
            inventoryButton = SkinnedButton("inventory", "인벤", GameUILayout.Inventory, game.inventorySkin);
            upgradeButton = SkinnedButton("upgrade", "제련", GameUILayout.Upgrade, game.upgradeSkin);
            resetButton = Button(root, "데이터 초기화", new Rect(354, 814, 112, 34), new Color32(62, 39, 42, 255), new Color32(241, 163, 151, 255), 11);
            readyButton = SkinnedButton("play", "Dig!!", GameUILayout.Dig, game.digSkin, true);
            buttons["regroup"] = regroupButton; buttons["reset"] = resetButton;
            readyHint = Label(root, "Start hint", new Rect(70, 607, 340, 25), "Dig!!를 눌러 채굴 시작", 14, Color.white, TextAnchor.MiddleCenter);
            overlay = RectNode(root, "Session panel", new Rect(40, 345, 400, 280));
            Box(overlay, "Panel shadow", new Rect(5, 7, 400, 280), new Color(0, 0, 0, .35f));
            Box(overlay, "Panel", new Rect(0, 0, 400, 280), new Color32(20, 34, 41, 249));
            Box(overlay, "Accent", new Rect(0, 0, 400, 3), gold);
            title = Label(overlay, "Title", new Rect(20, 24, 360, 47), "DIG DEEP", 34, gold, TextAnchor.MiddleCenter);
            detail = Label(overlay, "Description", new Rect(25, 75, 350, 82), "", 22, Color.white, TextAnchor.MiddleCenter);
            var button = Box(overlay, "Play or restart", new Rect(45, 159, 310, 57), gold);
            buttons["restart"] = button;
            mainLabel = Label(button.transform, "Label", new Rect(0, 0, 310, 57), "다시 준비", 24, dark, TextAnchor.MiddleCenter);
            menus = gameObject.AddComponent<GameMenus>(); menus.Initialize(game, root);
            noticePanel = Box(root, "Mining notice panel", new Rect(35, 190, 410, 45), new Color32(20, 30, 38, 235));
            notice = Label(noticePanel.transform, "Mining notice", new Rect(6, 0, 398, 45), "", 16, Color.white, TextAnchor.MiddleCenter);
            noticePanel.gameObject.SetActive(false);
            tutorialView = gameObject.AddComponent<TutorialView>(); tutorialView.Initialize(game, root);
            game.Session.Impact += OnImpact; game.Session.RecordBroken += OnRecordBroken; game.Session.Exploded += OnExplosion;
            Layout();
        }

        void Cloud(Rect rect)
        { Box(world, "Cloud", rect, new Color(1, 1, 1, .5f)); Box(world, "Cloud top", new Rect(rect.x + 15, rect.y - 7, rect.width - 30, 7), new Color(1, 1, 1, .5f)); }

        void Layout()
        {
            Rect safe = Screen.safeArea;
            float scale = Mathf.Min(safe.width / Width, safe.height / Height);
            root.localScale = Vector3.one * scale;
            root.anchoredPosition = safe.center - new Vector2(Screen.width, Screen.height) * .5f;
        }

        void LateUpdate()
        {
            Layout();
            var session = game.Session;
            float desired = Mathf.Max(0, Surface + (session.Position.y + .5f) * Pitch - 530);
            // Freeze camera while dragging so the release target does not shift under the finger.
            if (!dragging) scroll = Mathf.Lerp(scroll, desired, 1 - Mathf.Exp(-12 * Time.unscaledDeltaTime));
            world.anchoredPosition = new Vector2(0, FieldTop + scroll);
            int startRow = Mathf.Max(0, Mathf.FloorToInt((scroll + FieldTop - Surface) / Pitch));
            for (int i = 0; i < tiles.Count; i++)
            {
                int row = startRow + i / 5, col = i % 5;
                var cell = new Vector2Int(col, row);
                float hp = session.Health(cell);
                var tile = tiles[i]; tile.Image.gameObject.SetActive(hp > 0);
                if (hp == 0) continue;
                tile.Rect.anchoredPosition = new Vector2(Left + col * Pitch + 1, -(Surface + row * Pitch + 1));
                float impactAge = Time.unscaledTime - impactTime;
                if (!dragging && cell == impactCell && impactAge < .13f) tile.Rect.anchoredPosition += new Vector2(Mathf.Sin(impactAge * 90) * 2, 0);
                tile.Image.sprite = game.BlockSprite(session.Kind(cell), row);
                float shade = Mathf.Clamp(1 - row * .005f, .66f, 1);
                tile.Image.color = session.Kind(cell) == Mineral.Oil ? Color.black : new Color(shade, shade, shade);
                int maximum = session.MaximumHealth(cell);
                bool damaged = hp < maximum;
                for (int j = 0; j < 3; j++) tile.Cracks[j].gameObject.SetActive(damaged && hp <= maximum * (1 - j * .25f));
                tile.Health.gameObject.SetActive(damaged);
                tile.Health.rectTransform.sizeDelta = new Vector2(66f * hp / maximum, 3);
            }
            var position = Center(session.Position);
            if (game.IsSwinging) position = Center(game.SwingOrigin);
            if (dragging && !session.IsDynamite) position = new Vector2(dragPoint.x, dragPoint.y + scroll);
            pickRect.anchoredPosition = new Vector2(position.x, -position.y);
            pickImage.sprite = game.PickSprite(session.Picks[session.Selected].Name);
            pickImage.color = session.CanMine || session.State == SessionState.Ready ? Color.white : new Color(.42f, .42f, .42f);
            string selectedId = session.Picks[session.Selected].Name;
            bool straight = selectedId == "DRILL" || selectedId == "CRUSHER" || selectedId == "DYNAMITE";
            float angle = straight ? 0 : IdlePickAngle;
            if (game.IsSwinging)
            {
                Vector2 direction = Center(game.SwingCell) - Center(game.SwingOrigin);
                Vector2 motion = new Vector2(direction.x, -direction.y).normalized;
                if (selectedId == "DRILL")
                {
                    // The bit points down in the source sprite. Translate on the drill axis, never swing it.
                    angle = Mathf.Atan2(motion.y, motion.x) * Mathf.Rad2Deg + 90;
                    pickRect.anchoredPosition += motion * (Mathf.Sin(game.SwingProgress * Mathf.PI) * 15);
                }
                else if (selectedId == "CRUSHER")
                {
                    angle = Mathf.Atan2(motion.y, motion.x) * Mathf.Rad2Deg + 90;
                    pickRect.anchoredPosition += motion * (Mathf.Sin(game.SwingProgress * Mathf.PI) * 20);
                }
                else if (selectedId != "DYNAMITE")
                {
                    angle = Mathf.Atan2(motion.y, motion.x) * Mathf.Rad2Deg - 90 + Mathf.Cos(game.SwingProgress * Mathf.PI * 2) * 42;
                    pickRect.anchoredPosition += motion * (Mathf.Sin(game.SwingProgress * Mathf.PI) * 17);
                }
            }
            pickRect.localEulerAngles = new Vector3(0, 0, angle);
            for (int i = 0; i < 4; i++)
            {
                cards[i].gameObject.SetActive(i < session.Picks.Length);
                if (i >= session.Picks.Length) continue;
                var pick = session.Picks[i];
                icons[i].sprite = game.PickSprite(pick.Name);
                icons[i].rectTransform.localEulerAngles = new Vector3(0, 0, pick.Name == "DRILL" || pick.Name == "CRUSHER" || pick.Name == "DYNAMITE" ? 0 : IdlePickAngle);
                cards[i].color = i == session.Selected ? gold : new Color32(59, 73, 81, 255);
                bars[i].rectTransform.sizeDelta = new Vector2(39f * pick.Remaining / pick.Maximum, 5);
                if (shownRemaining[i] != pick.Remaining || shownMaximum[i] != pick.Maximum)
                { durability[i].text = pick.Remaining == 0 ? "소진" : pick.Remaining + " / " + pick.Maximum; shownRemaining[i] = pick.Remaining; shownMaximum[i] = pick.Maximum; }
                durability[i].color = pick.Remaining == 0 ? new Color32(224, 124, 109, 255) : Color.white;
            }
            if (shownMoney != session.Money) { shownMoney = session.Money; money.text = shownMoney.ToString("N0") + "원"; }
            if (shownDepth != session.Deepest || shownBest != session.BestDepth) { shownDepth = session.Deepest; shownBest = session.BestDepth; depth.text = "이번 판 " + shownDepth + "행\n최고 기록 " + shownBest + "행"; }
            bool playing = session.State == SessionState.Playing;
            bool ready = session.State == SessionState.Ready;
            overlay.gameObject.SetActive(session.State == SessionState.GameOver);
            readyButton.gameObject.SetActive(ready); readyHint.gameObject.SetActive(ready && game.Tutorial.Prompt != TutorialPrompt.Dig);
            regroupButton.gameObject.SetActive(playing);
            shopButton.gameObject.SetActive(!playing); upgradeButton.gameObject.SetActive(!playing); inventoryButton.gameObject.SetActive(!playing);
            resetButton.gameObject.SetActive(!playing);
            title.text = "채굴 종료";
            if (session.State == SessionState.GameOver) detail.text = session.ResultSummary();
            mainLabel.text = "다시 준비"; mainLabel.fontSize = 20;
            status.text = playing
                ? (!session.CanMine ? "소진 · 왼쪽 위에서 곡괭이를 선택하세요" : dragging ? "시작 위치 기준 상하 2칸 · 좌우 이동" : session.IsDynamite ? "블록을 탭해 폭파 · 주변 9칸 · 회당 내구도 1" : "탭 · 길게 누르기 · 곡괭이 드래그")
                : "성장 정보 자동 저장 · 가로 5칸";
            if (!string.IsNullOrEmpty(game.SaveWarning)) status.text = game.SaveWarning;
            foreach (var optional in optionalButtons.Values)
                if (optional.Target != null) optional.Target.gameObject.SetActive(optional.Available());
            menus.Refresh();
            tutorialView.Refresh();
            UpdateParticles();
            UpdateFeedback();
        }

        public void OnRecordBroken(int row) => ShowNotice("최고 깊이 갱신! " + row + "행", false);

        public void OnImpact(Vector2Int cell, float damage, bool broken)
        {
            impactCell = cell; impactTime = Time.unscaledTime;
            Color tint = OreColor(game.Session.Kind(cell));
            for (int i = 0; i < chips.Count; i++)
            {
                var chip = chips[i]; chip.Born = impactTime; chip.Start = Center(cell);
                chip.Velocity = new Vector2(Mathf.Sin(i * 2.4f) * (broken ? 105 : 65), -65 - i * 8);
                chip.Tint = Color.Lerp(tint, Color.white, .15f);
                chip.Image.gameObject.SetActive(i < (broken ? 9 : 4));
            }
            impactFlash.rectTransform.anchoredPosition = new Vector2(Left + cell.x * Pitch + 1, -(Surface + cell.y * Pitch + 1));
            impactFlash.color = new Color(1, 1, 1, broken ? .35f : .2f); impactFlash.gameObject.SetActive(true);
            var ore = OreCatalog.Find(game.Session.Kind(cell));
            if (broken && ore != null && game.Session.LastRewardAmount > 0)
            {
                var reward = rewards[nextReward]; nextReward = (nextReward + 1) % rewards.Length;
                reward.Start = Center(cell) - new Vector2(75, 30); reward.Born = impactTime;
                reward.Text.text = ore.Name + " +" + game.Session.LastRewardAmount; reward.Text.color = tint; reward.Text.gameObject.SetActive(true);
            }
        }

        public void OnExplosion(Vector2Int cell)
        {
            boomTime = Time.unscaledTime; var center = Center(cell);
            boom.rectTransform.anchoredPosition = new Vector2(center.x, -center.y); boom.gameObject.SetActive(true);
        }
        void UpdateParticles()
        {
            foreach (var chip in chips)
            {
                if (!chip.Image.gameObject.activeSelf) continue;
                float elapsed = Time.unscaledTime - chip.Born;
                if (elapsed > .45f) { chip.Image.gameObject.SetActive(false); continue; }
                Vector2 point = chip.Start + chip.Velocity * elapsed + new Vector2(0, 220 * elapsed * elapsed);
                chip.Rect.anchoredPosition = new Vector2(point.x, -point.y);
                var color = chip.Tint; color.a = 1 - elapsed / .45f; chip.Image.color = color;
            }
        }
        public static Color OreColor(Mineral kind) => kind == Mineral.Oil ? Color.black : kind == Mineral.Diamond ? new Color32(39, 221, 235, 255) : kind == Mineral.Coal ? new Color32(43, 47, 52, 255) : kind == Mineral.Iron ? new Color32(196, 137, 92, 255) : kind == Mineral.Gold ? new Color32(240, 193, 40, 255) : new Color32(157, 153, 141, 255);
        public void ShowNotice(string message, bool error = true)
        {
            if (notice.text == message && Time.unscaledTime < noticeUntil - .7f) return;
            notice.text = message; noticeUntil = Time.unscaledTime + 1.7f; noticeColor = error ? new Color32(255, 173, 148, 255) : new Color32(179, 238, 200, 255);
        }
        public void ResetRunVisuals()
        {
            scroll = 0; impactTime = -10; noticeUntil = 0; boomTime = -10; boom.gameObject.SetActive(false);
            foreach (var chip in chips) chip.Image.gameObject.SetActive(false);
            foreach (var reward in rewards) reward.Text.gameObject.SetActive(false);
            impactFlash.gameObject.SetActive(false);
        }
        void UpdateFeedback()
        {
            float boomAge = Time.unscaledTime - boomTime;
            bool showingBoom = boomAge >= 0 && boomAge < .45f; boom.gameObject.SetActive(showingBoom);
            if (showingBoom)
            {
                float alpha = Mathf.Clamp01((.45f - boomAge) / .18f);
                boom.color = new Color(1, .12f, .08f, alpha);
                boomText.color = new Color(1, .88f, .08f, alpha);
                boom.rectTransform.localScale = Vector3.one * (.7f + .4f * Mathf.Sin(Mathf.Min(1, boomAge / .3f) * Mathf.PI * .5f));
            }
            noticePanel.rectTransform.anchoredPosition = new Vector2(35, game.Modal == GameModal.None ? -190 : -748);
            noticePanel.gameObject.SetActive(Time.unscaledTime < noticeUntil);
            var noticeTint = noticeColor; noticeTint.a = Mathf.Clamp01((noticeUntil - Time.unscaledTime) * 4); notice.color = noticeTint;
            float flashAge = Time.unscaledTime - impactTime;
            impactFlash.gameObject.SetActive(flashAge < .14f);
            var flash = impactFlash.color; flash.a = Mathf.Clamp01(1 - flashAge / .14f) * .3f; impactFlash.color = flash;
            foreach (var reward in rewards)
            {
                if (!reward.Text.gameObject.activeSelf) continue;
                float age = Time.unscaledTime - reward.Born;
                if (age > .8f) { reward.Text.gameObject.SetActive(false); continue; }
                var position = reward.Start + new Vector2(0, -age * 38); reward.Text.rectTransform.anchoredPosition = new Vector2(position.x, -position.y);
                var color = reward.Text.color; color.a = Mathf.Clamp01((.8f - age) * 4); reward.Text.color = color;
            }
        }

        public Vector2 Local(Vector2 screen)
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle(root, screen, null, out var point);
            return new Vector2(point.x + Width / 2, Height / 2 - point.y);
        }
        public Vector2 ScreenPoint(Vector2 point)
        { return RectTransformUtility.WorldToScreenPoint(null, root.TransformPoint(new Vector3(point.x - Width / 2, Height / 2 - point.y))); }
        public bool VisibleCell(Vector2Int cell) => cell.x >= 0 && cell.x < GameSettings.Columns && cell.y >= 0 && CellBounds(cell).Overlaps(new Rect(Left, FieldTop, Pitch * 5, Height - FieldTop - 42));
        public bool InField(Vector2 point) => new Rect(Left, FieldTop, Pitch * 5, Height - FieldTop - 42).Contains(point);
        public Vector2Int CellAt(Vector2 point) => new Vector2Int(Mathf.FloorToInt((point.x - Left) / Pitch), Mathf.FloorToInt((point.y + scroll - Surface) / Pitch));
        public bool PickContains(Vector2 point)
        {
            Vector2 center = Center(game.Session.Position) - new Vector2(0, scroll);
            return new Rect(center.x - 38, center.y - 38, 76, 76).Contains(point);
        }
        public Rect PickBounds()
        { var center = Center(game.Session.Position) - new Vector2(0, scroll); return new Rect(center.x - 38, center.y - 38, 76, 76); }
        public Rect CellBounds(Vector2Int cell) => new Rect(Left + cell.x * Pitch, Surface + cell.y * Pitch - scroll, Pitch, Pitch);
        public string ActionAt(Vector2 point)
        {
            if (game.Modal == GameModal.None)
                foreach (var optional in optionalButtons)
                    if (optional.Value.Target != null && optional.Value.Available() && RootBounds(optional.Value.Target).Contains(point)) return optional.Key;
            if (tutorialView.BlocksPoint(point)) return "blocked";
            var action = BaseActionAt(point);
            return action == null || game.Tutorial.AllowsAction(action) ? action : "blocked";
        }
        string BaseActionAt(Vector2 point)
        {
            if (game.Modal != GameModal.None) return menus.ActionAt(point);
            var state = game.Session.State;
            if (state == SessionState.Ready && ButtonBounds("play").Contains(point)) return "play";
            if (state == SessionState.GameOver && ButtonBounds("restart").Contains(point)) return "restart";
            if (state != SessionState.Playing)
            {
                if (ButtonBounds("shop").Contains(point)) return "shop";
                if (ButtonBounds("inventory").Contains(point)) return "inventory";
                if (ButtonBounds("upgrade").Contains(point)) return "upgrade";
                if (ButtonBounds("reset").Contains(point)) return "reset";
            }
            else if (ButtonBounds("regroup").Contains(point)) return "regroup";
            foreach (var pair in actions) if (pair.Value.Contains(point)) return pair.Key;
            if (!InField(point) || state != SessionState.Playing) return "blocked";
            return null;
        }

        public Rect ButtonBounds(string action) => buttons.TryGetValue(action, out var button) && button != null ? RootBounds(button.rectTransform) : new Rect();
        public Rect RootBounds(RectTransform target)
        {
            target.GetWorldCorners(boundCorners);
            Vector3 topLeft = root.InverseTransformPoint(boundCorners[1]), bottomRight = root.InverseTransformPoint(boundCorners[3]);
            return new Rect(topLeft.x + Width * .5f, Height * .5f - topLeft.y, bottomRight.x - topLeft.x, topLeft.y - bottomRight.y);
        }
        public void RegisterOptionalButton(string id, RectTransform target, Func<bool> available, Action execute)
            => optionalButtons[id] = new OptionalButton { Target = target, Available = available, Execute = execute };
        public void UnregisterOptionalButton(string id)
        {
            if (optionalButtons.TryGetValue(id, out var optional) && optional.Target != null) optional.Target.gameObject.SetActive(false);
            optionalButtons.Remove(id);
        }
        public bool TryOptionalAction(string id)
        {
            if (!optionalButtons.TryGetValue(id, out var optional)) return false;
            if (optional.Target != null && optional.Available()) optional.Execute();
            return true;
        }

        public void SetDrag(Vector2 point)
        {
            dragging = true; dragPoint = point;
            var cell = CellAt(point);
            List<Vector2Int> path = null;
            bool bombing = game.Session.IsDynamite;
            bool valid = InField(point) && (bombing ? game.Session.CanBlast(cell) : game.Session.FindPath(cell, out path));
            marker.gameObject.SetActive(InField(point));
            marker.sizeDelta = Vector2.one * Pitch * (bombing ? 3 : 1);
            marker.anchoredPosition = new Vector2(Left + (cell.x - (bombing ? 1 : 0)) * Pitch, -(Surface + (cell.y - (bombing ? 1 : 0)) * Pitch));
            markerImage.color = valid ? new Color(.35f, .9f, .63f, .35f) : new Color(.95f, .3f, .25f, .4f);
            foreach (var image in routeMarkers) image.gameObject.SetActive(false);
            if (valid && path != null)
                for (int i = 0; i < path.Count && i < routeMarkers.Count; i++)
                {
                    var image = routeMarkers[i]; image.gameObject.SetActive(true);
                    var center = Center(path[i]); image.rectTransform.anchoredPosition = new Vector2(center.x - 5, -center.y + 5);
                }
        }
        public void EndDrag()
        { dragging = false; marker.gameObject.SetActive(false); foreach (var image in routeMarkers) image.gameObject.SetActive(false); menus.EndScroll(); }
        public bool BeginMenuScroll(Vector2 point) => menus.BeginScroll(point);
        public void DragMenuScroll(float distance) => menus.DragScroll(distance);
        public void MenuWheel(Vector2 point, float distance) => menus.Wheel(point, distance);
        static Vector2 Center(Vector2Int cell) => new Vector2(Left + (cell.x + .5f) * Pitch, Surface + (cell.y + .5f) * Pitch);
        public static void TiltPickIcon(Image image)
        {
            var rect = image.rectTransform;
            rect.anchoredPosition += new Vector2(rect.sizeDelta.x * .5f, -rect.sizeDelta.y * .5f);
            rect.pivot = new Vector2(.5f, .5f);
            rect.localEulerAngles = new Vector3(0, 0, IdlePickAngle);
            rect.localScale = Vector3.one * .82f;
            image.preserveAspect = true;
        }

        static RectTransform RectNode(Transform parent, string name, Rect rect)
        {
            var node = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            node.SetParent(parent, false); node.anchorMin = node.anchorMax = new Vector2(0, 1); node.pivot = new Vector2(0, 1);
            node.anchoredPosition = new Vector2(rect.x, -rect.y); node.sizeDelta = rect.size; return node;
        }
        static Image Box(Transform parent, string name, Rect rect, Color color, Sprite sprite = null)
        {
            var image = RectNode(parent, name, rect).gameObject.AddComponent<Image>();
            image.color = color; image.sprite = sprite; image.raycastTarget = false; return image;
        }
        Image SkinnedButton(string id, string caption, Rect rect, Sprite sprite, bool primary = false)
        {
            var button = Box(root, caption, rect, sprite != null ? Color.white : primary ? gold : new Color32(47, 69, 76, 255), sprite);
            if (sprite != null)
            {
                button.type = Image.Type.Sliced;
                // Unity also scales sprite PPU when limiting texture resolution.
                // Keep the fixed borders and icons at their intended on-screen size.
                button.pixelsPerUnitMultiplier = sprite.rect.height / rect.height * 100f / sprite.pixelsPerUnit;
            }
            float inset = sprite != null ? (primary ? 54 : 37) : 0;
            var text = Label(button.transform, "Label", new Rect(inset, 0, rect.width - inset - (sprite != null ? 5 : 0), rect.height), caption, primary ? 30 : 16, primary ? dark : Color.white, TextAnchor.MiddleCenter);
            text.fontStyle = FontStyle.Bold;
            if (primary && sprite != null) TiltPickIcon(Box(button.transform, "Dig pickaxe", new Rect(14, 12, 40, 40), Color.white, game.wood));
            buttons[id] = button; return button;
        }
        Image Button(Transform parent, string caption, Rect rect, Color color, Color textColor, int size = 16)
        {
            var button = Box(parent, caption, rect, color);
            Label(button.transform, "Label", new Rect(0, 0, rect.width, rect.height), caption, size, textColor, TextAnchor.MiddleCenter);
            return button;
        }
        Text Label(Transform parent, string name, Rect rect, string value, int size, Color color, TextAnchor alignment)
        {
            var text = RectNode(parent, name, rect).gameObject.AddComponent<Text>();
            text.font = font; text.fontSize = size; text.color = color; text.text = value; text.alignment = alignment;
            text.fontStyle = size >= 16 ? FontStyle.Bold : FontStyle.Normal;
            text.horizontalOverflow = HorizontalWrapMode.Wrap; text.verticalOverflow = VerticalWrapMode.Overflow;
            text.raycastTarget = false; return text;
        }
    }
}
