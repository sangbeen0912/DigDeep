using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace DigDeep
{
    public sealed class GameView : MonoBehaviour
    {
        const float Width = 480, Height = 854, Pitch = 88, Left = 20, FieldTop = 108, Surface = 226;
        GameManager game;
        RectTransform root, field, world, pickRect, marker, overlay;
        Image pickImage, markerImage;
        Text money, status, depth, title, detail, mainLabel;
        Image[] cards = new Image[2], bars = new Image[2];
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
            Cloud(new Rect(45, 154, 64, 9)); Cloud(new Rect(338, 132, 90, 10));
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
            Box(root, "HUD underline", new Rect(20, 102, 440, 2), new Color32(49, 65, 72, 255));
            for (int i = 0; i < 2; i++)
            {
                Rect rect = new Rect(20 + i * 83, 15, 75, 77);
                cards[i] = Box(root, "Pickaxe slot " + i, rect, dark);
                Box(cards[i].transform, "Slot inset", new Rect(3, 3, 69, 71), new Color32(35, 45, 52, 255));
                Box(cards[i].transform, "Pickaxe", new Rect(17, 4, 40, 40), Color.white, i == 0 ? game.wood : game.iron);
                Box(cards[i].transform, "Durability track", new Rect(8, 46, 59, 5), new Color32(8, 16, 21, 255));
                bars[i] = Box(cards[i].transform, "Durability fill", new Rect(8, 46, 59, 5), new Color32(125, 210, 171, 255));
                durability[i] = Label(cards[i].transform, "Durability", new Rect(0, 55, 75, 15), "", 10, Color.white, TextAnchor.MiddleCenter);
                actions["pick" + i] = rect;
            }
            Label(root, "Wallet caption", new Rect(295, 22, 160, 17), "보유 골드", 12, new Color32(151, 167, 175, 255), TextAnchor.MiddleRight);
            money = Label(root, "Wallet", new Rect(255, 42, 200, 32), "0 G", 25, gold, TextAnchor.MiddleRight);
            depth = Label(root, "Depth", new Rect(300, 82, 154, 15), "", 10, new Color32(151, 167, 175, 255), TextAnchor.MiddleRight);
            Box(root, "Footer", new Rect(0, Height - 42, Width, 42), dark);
            status = Label(root, "Instructions", new Rect(15, Height - 39, 450, 34), "", 12, new Color32(180, 203, 210, 255), TextAnchor.MiddleCenter);
            overlay = RectNode(root, "Session panel", new Rect(40, 345, 400, 280));
            Box(overlay, "Panel shadow", new Rect(5, 7, 400, 280), new Color(0, 0, 0, .35f));
            Box(overlay, "Panel", new Rect(0, 0, 400, 280), new Color32(20, 34, 41, 249));
            Box(overlay, "Accent", new Rect(0, 0, 400, 3), gold);
            title = Label(overlay, "Title", new Rect(20, 24, 360, 47), "DIG DEEP", 34, gold, TextAnchor.MiddleCenter);
            detail = Label(overlay, "Description", new Rect(25, 77, 350, 63), "", 15, Color.white, TextAnchor.MiddleCenter);
            var button = Box(overlay, "Play or restart", new Rect(45, 159, 310, 57), gold);
            mainLabel = Label(button.transform, "Label", new Rect(0, 0, 310, 57), "PLAY", 24, dark, TextAnchor.MiddleCenter);
            var shop = Box(overlay, "Shop placeholder", new Rect(45, 229, 150, 32), new Color32(36, 51, 59, 255));
            Label(shop.transform, "Label", new Rect(0, 0, 150, 32), "상점 · 준비 중", 12, new Color32(135, 151, 158, 255), TextAnchor.MiddleCenter);
            var upgrade = Box(overlay, "Upgrade placeholder", new Rect(205, 229, 150, 32), new Color32(36, 51, 59, 255));
            Label(upgrade.transform, "Label", new Rect(0, 0, 150, 32), "강화 · 준비 중", 12, new Color32(135, 151, 158, 255), TextAnchor.MiddleCenter);
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
            pickImage.sprite = session.Selected == 0 ? game.wood : game.iron;
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
                var pick = session.Picks[i];
                cards[i].color = i == session.Selected ? gold : new Color32(59, 73, 81, 255);
                bars[i].rectTransform.sizeDelta = new Vector2(59f * pick.Remaining / pick.Maximum, 5);
                durability[i].text = pick.Remaining == 0 ? "소진" : pick.Remaining + " / " + pick.Maximum;
                durability[i].color = pick.Remaining == 0 ? new Color32(224, 124, 109, 255) : Color.white;
            }
            money.text = session.Money.ToString("N0") + " G";
            depth.text = "최대 깊이  " + session.Deepest;
            overlay.gameObject.SetActive(session.State != SessionState.Playing);
            title.text = session.State == SessionState.GameOver ? "채굴 종료" : "DIG DEEP";
            detail.text = session.State == SessionState.GameOver
                ? "이번 채굴 +" + session.RunIncome + " G\n곡괭이를 회복하고 다시 도전하세요"
                : "한 칸 더 깊이, 나만의 길을 만들어 보세요\n탭 · 길게 누르기 · 곡괭이 드래그";
            mainLabel.text = session.State == SessionState.GameOver ? "곡괭이 회복 · 다시 준비" : "PLAY";
            mainLabel.fontSize = session.State == SessionState.GameOver ? 18 : 24;
            status.text = session.State == SessionState.Playing
                ? (!session.CanMine ? "곡괭이 소진 · 왼쪽 위에서 다른 곡괭이를 선택하세요" : dragging ? "빈 길을 따라 좌우 · 위 5칸까지 이동" : "탭으로 타격 · 길게 눌러 채굴 · 곡괭이를 잡아 이동")
                : "가로 5칸  /  모바일 세로형 채굴 게임";
            UpdateParticles();
        }

        void OnImpact(Vector2Int cell, int damage, bool broken)
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
            if (game.Session.State != SessionState.Playing && new Rect(85, 504, 310, 57).Contains(point))
                return game.Session.State == SessionState.Ready ? "play" : "restart";
            foreach (var pair in actions) if (pair.Value.Contains(point)) return pair.Key;
            if (!InField(point) || game.Session.State != SessionState.Playing) return "blocked";
            return null;
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
