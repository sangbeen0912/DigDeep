using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace DigDeep
{
    public sealed class GameView : MonoBehaviour
    {
        const float Width = 480, Height = 854, Pitch = 88, Left = 20, FieldTop = 148, Surface = 266;
        GameManager game;
        RectTransform root, field, world, pickRect, marker, overlay;
        Image pickImage, markerImage, readyButton, regroupButton, shopButton, upgradeButton, resetButton;
        RectTransform modal;
        Text modalTitle, modalBody, modalConfirm, modalCancel;
        Image confirmButton;
        Text readyHint;
        Text money, status, depth, title, detail, mainLabel;
        Image[] cards = new Image[2], bars = new Image[2];
        Image[] icons = new Image[2];
        Text[] durability = new Text[2];
        readonly List<Tile> tiles = new List<Tile>();
        readonly List<Chip> chips = new List<Chip>();
        readonly List<Image> routeMarkers = new List<Image>();
        readonly Dictionary<string, Rect> actions = new Dictionary<string, Rect>();
        Font font;
        bool dragging;
        Vector2 dragPoint;
        float scroll, impactTime = -10;
        Vector2Int impactCell;
        Color dark = new Color32(19, 27, 34, 255), gold = new Color32(239, 189, 88, 255);
        sealed class Tile { public RectTransform Rect; public Image Image; public Image[] Cracks; public Image Health; }
        sealed class Chip { public RectTransform Rect; public Image Image; public Vector2 Velocity, Start; public float Born; }

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
            pickRect = pickImage.rectTransform;
            pickRect.pivot = new Vector2(.5f, .5f);
            for (int i = 0; i < 12; i++)
            {
                var image = Box(world, "Stone particle", new Rect(0, 0, 5 + i % 4, 5 + i % 4), new Color32(157, 153, 141, 255));
                image.gameObject.SetActive(false); chips.Add(new Chip { Rect = image.rectTransform, Image = image, Born = -10 });
            }
            Box(root, "HUD", new Rect(0, 0, Width, FieldTop), dark);
            Box(root, "HUD underline", new Rect(20, 146, 440, 2), new Color32(49, 65, 72, 255));
            for (int i = 0; i < 2; i++)
            {
                Rect rect = new Rect(20 + i * 83, 15, 75, 77);
                cards[i] = Box(root, "Pickaxe slot " + i, rect, dark);
                Box(cards[i].transform, "Slot inset", new Rect(3, 3, 69, 71), new Color32(35, 45, 52, 255));
                icons[i] = Box(cards[i].transform, "Pickaxe", new Rect(17, 4, 40, 40), Color.white, i == 0 ? game.wood : game.iron);
                Box(cards[i].transform, "Durability track", new Rect(8, 46, 59, 5), new Color32(8, 16, 21, 255));
                bars[i] = Box(cards[i].transform, "Durability fill", new Rect(8, 46, 59, 5), new Color32(125, 210, 171, 255));
                durability[i] = Label(cards[i].transform, "Durability", new Rect(0, 55, 75, 15), "", 10, Color.white, TextAnchor.MiddleCenter);
                actions["pick" + i] = rect;
            }
            Label(root, "Wallet caption", new Rect(295, 22, 160, 17), "보유 골드", 12, new Color32(151, 167, 175, 255), TextAnchor.MiddleRight);
            money = Label(root, "Wallet", new Rect(255, 42, 200, 32), "0 G", 25, gold, TextAnchor.MiddleRight);
            depth = Label(root, "Depth", new Rect(300, 82, 154, 15), "", 10, new Color32(151, 167, 175, 255), TextAnchor.MiddleRight);
            Box(root, "Footer", new Rect(0, Height - 42, Width, 42), dark);
            status = Label(root, "Instructions", new Rect(15, Height - 39, 325, 34), "", 10, new Color32(180, 203, 210, 255), TextAnchor.MiddleLeft);
            regroupButton = Button(root, "재정비", new Rect(20, 100, 158, 42), new Color32(47, 69, 76, 255), Color.white);
            shopButton = Button(root, "상점", new Rect(292, 100, 78, 42), new Color32(47, 69, 76, 255), Color.white);
            upgradeButton = Button(root, "강화", new Rect(380, 100, 80, 42), new Color32(47, 69, 76, 255), Color.white);
            resetButton = Button(root, "데이터 초기화", new Rect(354, 814, 112, 34), new Color32(62, 39, 42, 255), new Color32(241, 163, 151, 255), 11);
            readyButton = Button(root, "Dig!!", new Rect(125, 530, 230, 64), gold, dark, 30);
            readyHint = Label(root, "Start hint", new Rect(70, 607, 340, 25), "Dig!!를 눌러 채굴 시작", 14, Color.white, TextAnchor.MiddleCenter);
            overlay = RectNode(root, "Session panel", new Rect(40, 345, 400, 280));
            Box(overlay, "Panel shadow", new Rect(5, 7, 400, 280), new Color(0, 0, 0, .35f));
            Box(overlay, "Panel", new Rect(0, 0, 400, 280), new Color32(20, 34, 41, 249));
            Box(overlay, "Accent", new Rect(0, 0, 400, 3), gold);
            title = Label(overlay, "Title", new Rect(20, 24, 360, 47), "DIG DEEP", 34, gold, TextAnchor.MiddleCenter);
            detail = Label(overlay, "Description", new Rect(25, 77, 350, 63), "", 15, Color.white, TextAnchor.MiddleCenter);
            var button = Box(overlay, "Play or restart", new Rect(45, 159, 310, 57), gold);
            mainLabel = Label(button.transform, "Label", new Rect(0, 0, 310, 57), "다시 준비", 24, dark, TextAnchor.MiddleCenter);
            modal = RectNode(root, "Dialog layer", new Rect(0, 0, Width, Height));
            Box(modal, "Dim backdrop", new Rect(0, 0, Width, Height), new Color(0, 0, 0, .75f));
            var panel = Box(modal, "Dialog", new Rect(35, 290, 410, 290), dark);
            Box(panel.transform, "Accent", new Rect(0, 0, 410, 3), gold);
            modalTitle = Label(modal, "Dialog title", new Rect(55, 315, 370, 42), "", 25, gold, TextAnchor.MiddleCenter);
            modalBody = Label(modal, "Dialog explanation", new Rect(60, 369, 360, 98), "", 16, Color.white, TextAnchor.MiddleCenter);
            var cancel = Box(modal, "Cancel", new Rect(60, 496, 170, 48), new Color32(46, 61, 70, 255));
            modalCancel = Label(cancel.transform, "Label", new Rect(0, 0, 170, 48), "취소", 17, Color.white, TextAnchor.MiddleCenter);
            confirmButton = Box(modal, "Confirm", new Rect(250, 496, 170, 48), new Color32(175, 61, 50, 255));
            modalConfirm = Label(confirmButton.transform, "Label", new Rect(0, 0, 170, 48), "계속", 17, Color.white, TextAnchor.MiddleCenter);
            modal.gameObject.SetActive(false);
            game.Session.Impact += OnImpact;
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
                int hp = session.Health(cell);
                var tile = tiles[i]; tile.Image.gameObject.SetActive(hp > 0);
                if (hp == 0) continue;
                tile.Rect.anchoredPosition = new Vector2(Left + col * Pitch + 1, -(Surface + row * Pitch + 1));
                tile.Image.sprite = row == 0 ? game.grass : game.stone;
                float shade = Mathf.Clamp(1 - row * .005f, .66f, 1);
                tile.Image.color = new Color(shade, shade, shade);
                bool damaged = hp < session.HealthMaximum;
                for (int j = 0; j < 3; j++) tile.Cracks[j].gameObject.SetActive(damaged && hp <= session.HealthMaximum * (1 - j * .25f));
                tile.Health.gameObject.SetActive(damaged);
                tile.Health.rectTransform.sizeDelta = new Vector2(66f * hp / session.HealthMaximum, 3);
            }
            var position = Center(session.Position);
            if (dragging) position = new Vector2(dragPoint.x, dragPoint.y + scroll);
            pickRect.anchoredPosition = new Vector2(position.x, -position.y);
            pickImage.sprite = session.Picks[session.Selected].Name == "WOOD" ? game.wood : game.iron;
            pickImage.color = session.CanMine || session.State == SessionState.Ready ? Color.white : new Color(.42f, .42f, .42f);
            float angle = game.IsSwinging ? Mathf.Sin(game.SwingProgress * Mathf.PI * 2) * 42 : -8;
            pickRect.localEulerAngles = new Vector3(0, 0, angle);
            if (game.IsSwinging && session.Target.HasValue)
            {
                Vector2 direction = Center(session.Target.Value) - Center(session.Position);
                pickRect.anchoredPosition += new Vector2(direction.x, -direction.y).normalized * (Mathf.Sin(game.SwingProgress * Mathf.PI) * 17);
            }
            for (int i = 0; i < 2; i++)
            {
                cards[i].gameObject.SetActive(i < session.Picks.Length);
                if (i >= session.Picks.Length) continue;
                var pick = session.Picks[i];
                icons[i].sprite = pick.Name == "WOOD" ? game.wood : game.iron;
                cards[i].color = i == session.Selected ? gold : new Color32(59, 73, 81, 255);
                bars[i].rectTransform.sizeDelta = new Vector2(59f * pick.Remaining / pick.Maximum, 5);
                durability[i].text = pick.Remaining == 0 ? "소진" : pick.Remaining + " / " + pick.Maximum;
                durability[i].color = pick.Remaining == 0 ? new Color32(224, 124, 109, 255) : Color.white;
            }
            money.text = session.Money.ToString("N0") + " G";
            depth.text = "최대 깊이  " + session.Deepest;
            bool playing = session.State == SessionState.Playing;
            bool ready = session.State == SessionState.Ready;
            overlay.gameObject.SetActive(session.State == SessionState.GameOver);
            readyButton.gameObject.SetActive(ready); readyHint.gameObject.SetActive(ready);
            regroupButton.gameObject.SetActive(playing);
            shopButton.gameObject.SetActive(!playing); upgradeButton.gameObject.SetActive(!playing);
            resetButton.gameObject.SetActive(!playing);
            title.text = "채굴 종료";
            detail.text = "이번 채굴 +" + session.RunIncome + " G\n곡괭이를 회복하고 다시 도전하세요";
            mainLabel.text = "다시 준비"; mainLabel.fontSize = 20;
            status.text = playing
                ? (!session.CanMine ? "소진 · 왼쪽 위에서 곡괭이를 선택하세요" : dragging ? "좌우 · 위 5칸까지 이동" : "탭 · 길게 누르기 · 곡괭이 드래그")
                : "성장 정보 자동 저장 · 가로 5칸";
            if (!string.IsNullOrEmpty(game.SaveWarning)) status.text = game.SaveWarning;
            UpdateDialog();
            UpdateParticles();
        }

        public void OnImpact(Vector2Int cell, int damage, bool broken)
        {
            impactCell = cell; impactTime = Time.unscaledTime;
            for (int i = 0; i < chips.Count; i++)
            {
                var chip = chips[i]; chip.Born = impactTime; chip.Start = Center(cell);
                chip.Velocity = new Vector2(Mathf.Sin(i * 2.4f) * (broken ? 105 : 65), -65 - i * 8);
                chip.Image.gameObject.SetActive(i < (broken ? 12 : 5));
            }
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
                chip.Image.color = new Color(.68f, .66f, .59f, 1 - elapsed / .45f);
            }
        }

        public Vector2 Local(Vector2 screen)
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle(root, screen, null, out var point);
            return new Vector2(point.x + Width / 2, Height / 2 - point.y);
        }
        public Vector2 ScreenPoint(Vector2 point)
        { return RectTransformUtility.WorldToScreenPoint(null, root.TransformPoint(new Vector3(point.x - Width / 2, Height / 2 - point.y))); }
        public bool InField(Vector2 point) => new Rect(Left, FieldTop, Pitch * 5, Height - FieldTop - 42).Contains(point);
        public Vector2Int CellAt(Vector2 point) => new Vector2Int(Mathf.FloorToInt((point.x - Left) / Pitch), Mathf.FloorToInt((point.y + scroll - Surface) / Pitch));
        public bool PickContains(Vector2 point)
        {
            Vector2 center = Center(game.Session.Position) - new Vector2(0, scroll);
            return new Rect(center.x - 38, center.y - 38, 76, 76).Contains(point);
        }
        public string ActionAt(Vector2 point)
        {
            if (game.Modal != GameModal.None)
            {
                if (new Rect(60, 496, 170, 48).Contains(point)) return "close";
                if (new Rect(250, 496, 170, 48).Contains(point))
                {
                    if (game.Modal == GameModal.ResetWarning) return "reset-next";
                    if (game.Modal == GameModal.ResetConfirm) return "reset-confirm";
                }
                return "blocked";
            }
            var state = game.Session.State;
            if (state == SessionState.Ready && new Rect(125, 530, 230, 64).Contains(point)) return "play";
            if (state == SessionState.GameOver && new Rect(85, 504, 310, 57).Contains(point)) return "restart";
            if (state != SessionState.Playing)
            {
                if (new Rect(292, 100, 78, 42).Contains(point)) return "shop";
                if (new Rect(380, 100, 80, 42).Contains(point)) return "upgrade";
                if (new Rect(354, 814, 112, 34).Contains(point)) return "reset";
            }
            else if (new Rect(20, 100, 158, 42).Contains(point)) return "regroup";
            foreach (var pair in actions) if (pair.Value.Contains(point)) return pair.Key;
            if (!InField(point) || state != SessionState.Playing) return "blocked";
            return null;
        }

        void UpdateDialog()
        {
            var value = game.Modal;
            modal.gameObject.SetActive(value != GameModal.None);
            bool reset = value == GameModal.ResetWarning || value == GameModal.ResetConfirm;
            confirmButton.gameObject.SetActive(reset);
            modalCancel.text = reset ? "취소" : "닫기";
            if (value == GameModal.ResetWarning)
            {
                modalTitle.text = "데이터 초기화";
                modalBody.text = "돈 · 소유 곡괭이 · 강화 정보가\n모두 초기화됩니다.\n계속하시겠습니까?";
                modalConfirm.text = "계속";
            }
            else if (value == GameModal.ResetConfirm)
            {
                modalTitle.text = "정말 초기화할까요?";
                modalBody.text = "삭제한 데이터는 복구할 수 없습니다.\n모든 성장 정보를 지우고\n처음부터 시작합니다.";
                modalConfirm.text = "데이터 초기화";
            }
            else if (value == GameModal.Shop)
            {
                modalTitle.text = "곡괭이 상점";
                modalBody.text = "보유 골드  " + game.Session.Money.ToString("N0") + " G\n\n구매 기능은 준비 중입니다.";
            }
            else if (value == GameModal.Upgrade)
            {
                modalTitle.text = "곡괭이 강화";
                var pick = game.Session.Picks[game.Session.Selected];
                modalBody.text = (pick.Name == "WOOD" ? "나무 곡괭이" : "철 곡괭이") + "  +" + pick.Upgrade
                    + "\n공격력 " + pick.Power + " · 최대 내구도 " + pick.Maximum + "\n강화 기능은 준비 중입니다.";
            }
        }

        public void SetDrag(Vector2 point)
        {
            dragging = true; dragPoint = point;
            var cell = CellAt(point);
            bool valid = InField(point) && game.Session.FindPath(cell, out _);
            marker.gameObject.SetActive(InField(point));
            marker.anchoredPosition = new Vector2(Left + cell.x * Pitch, -(Surface + cell.y * Pitch));
            markerImage.color = valid ? new Color(.35f, .9f, .63f, .35f) : new Color(.95f, .3f, .25f, .4f);
            foreach (var image in routeMarkers) image.gameObject.SetActive(false);
            if (valid && game.Session.FindPath(cell, out var path))
                for (int i = 0; i < path.Count && i < routeMarkers.Count; i++)
                {
                    var image = routeMarkers[i]; image.gameObject.SetActive(true);
                    var center = Center(path[i]); image.rectTransform.anchoredPosition = new Vector2(center.x - 5, -center.y + 5);
                }
        }
        public void EndDrag()
        { dragging = false; marker.gameObject.SetActive(false); foreach (var image in routeMarkers) image.gameObject.SetActive(false); }
        static Vector2 Center(Vector2Int cell) => new Vector2(Left + (cell.x + .5f) * Pitch, Surface + (cell.y + .5f) * Pitch);

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
