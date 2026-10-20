using UnityEngine;
using UnityEngine.UI;
namespace DigDeep
{
    public sealed class TutorialView : MonoBehaviour
    {
        GameManager game;
        RectTransform layer, bubble, bubbleShadow, accent;
        Image[] shade = new Image[4], edge = new Image[4], destinationEdge = new Image[4];
        Text title, explanation, detail;
        readonly Rect[] outside = new Rect[4];
        public Rect FocusBounds { get; private set; }
        public Rect MessageBounds { get; private set; }
        public void Initialize(GameManager owner, RectTransform parent)
        {
            game = owner;
            layer = Node(parent, "First play tutorial", new Rect(0, 0, 480, 854));
            for (int i = 0; i < 4; i++) shade[i] = Box(layer, "Focus shade " + i, new Color(0, 0, 0, .48f));
            for (int i = 0; i < 4; i++) { edge[i] = Box(layer, "Focus edge " + i, Color.white); destinationEdge[i] = Box(layer, "Drag destination edge " + i, Color.white); }
            bubbleShadow = Box(layer, "Tutorial panel shadow", new Color(0, 0, 0, .55f)).rectTransform;
            bubble = Node(layer, "Tutorial explanation", new Rect(24, 676, 432, 128));
            var background = bubble.gameObject.AddComponent<Image>(); background.color = new Color32(10, 20, 29, 255); background.raycastTarget = false;
            accent = Box(bubble, "Tutorial accent", new Color32(255, 207, 102, 255)).rectTransform;
            title = Label(bubble, "Tutorial title", new Rect(20, 10, 392, 28), 17, new Color32(255, 207, 102, 255), true);
            explanation = Label(bubble, "Tutorial instructions", new Rect(20, 43, 392, 38), 22, Color.white, true);
            detail = Label(bubble, "Tutorial detail", new Rect(20, 88, 392, 30), 17, new Color32(209, 224, 234, 255));
            layer.gameObject.SetActive(false);
        }
        public bool BlocksPoint(Vector2 point) => layer.gameObject.activeSelf && MessageBounds.Contains(point);
        public void Refresh()
        {
            var prompt = game.Tutorial.Prompt;
            bool resetting = game.Modal == GameModal.ResetWarning || game.Modal == GameModal.ResetConfirm;
            layer.gameObject.SetActive(prompt != TutorialPrompt.None && !resetting);
            if (!layer.gameObject.activeSelf) return;
            Rect focus = new Rect(40, 565, 120, 92); bool dim = true, destination = false;
            // Keep open menu contents and their back buttons unobscured.
            Rect panel = game.Modal == GameModal.None ? new Rect(24, 676, 432, 128) : new Rect(24, 36, 432, 132);
            if (prompt == TutorialPrompt.Dig) panel = new Rect(24, 616, 432, 136);
            string caption = "", action = "", note = "";
            switch (prompt)
            {
                case TutorialPrompt.Dig:
                    focus = game.View.ButtonBounds("play"); caption = "첫 채굴 시작";
                    action = "<color=#FFD166>Dig!!</color>를 눌러 시작하세요.";
                    note = game.Session.Tutorial.controlsStep == 0 ? "탭 · 드래그 · 홀드를 차례로 배워요." : "남은 조작 안내를 이어서 진행해요."; break;
                case TutorialPrompt.Tap:
                    dim = false; caption = "조작 1/3 · 탭";
                    action = "화면을 <color=#FFD166>한 번</color> 누르세요.";
                    note = "한 번 누르면 한 번 타격해요."; break;
                case TutorialPrompt.Drag:
                    dim = false; focus = game.View.PickBounds(); destination = true; caption = "조작 2/3 · 드래그";
                    action = "<color=#FFD166>곡괭이</color>를 옆칸으로 끌어놓으세요.";
                    note = "초록 테두리 칸에 놓으면 돼요."; break;
                case TutorialPrompt.Hold:
                    dim = false; caption = "조작 3/3 · 홀드";
                    action = "<color=#FFD166>길게 눌러</color> 연속으로 캐세요.";
                    note = "두 번 이상 캔 뒤 손을 떼세요."; break;
                case TutorialPrompt.Restart:
                    focus = game.View.ButtonBounds("restart"); caption = "채굴 종료";
                    action = "<color=#FFD166>다시 준비</color>를 눌러주세요.";
                    note = "곡괭이를 회복하고 다음 안내로 가요."; break;
                case TutorialPrompt.Inventory:
                    caption = "화면 1/3 · 인벤"; focus = game.Modal == GameModal.None ? game.View.ButtonBounds("inventory") : new Rect(55, 686, 370, 34);
                    action = game.Modal == GameModal.None ? "<color=#FFD166>인벤</color> 버튼을 눌러보세요." : "확인했으면 <color=#FFD166>뒤로가기</color>를 누르세요.";
                    note = game.Modal == GameModal.None ? "보유 광물과 에너지를 확인할 수 있어요." : "광물과 에너지가 이곳에 보관돼요."; break;
                case TutorialPrompt.Shop:
                    caption = "화면 2/3 · 상점"; focus = game.Modal == GameModal.None ? game.View.ButtonBounds("shop") : new Rect(55, 686, 370, 34);
                    action = game.Modal == GameModal.None ? "<color=#FFD166>상점</color> 버튼을 눌러보세요." : "확인했으면 <color=#FFD166>뒤로가기</color>를 누르세요.";
                    note = game.Modal == GameModal.None ? "곡괭이를 사고 광물을 팔 수 있어요." : "구매는 곡괭이 · 판매는 광물이에요."; break;
                case TutorialPrompt.Upgrade:
                    caption = "화면 3/3 · 제련";
                    focus = game.Modal == GameModal.None ? game.View.ButtonBounds("upgrade") : game.Modal == GameModal.Upgrade ? new Rect(55, 346, 370, 252) : new Rect(55, 686, 370, 34);
                    action = game.Modal == GameModal.None ? "<color=#FFD166>제련</color> 버튼을 눌러보세요." : game.Modal == GameModal.Upgrade ? "<color=#FFD166>곡괭이</color>를 하나 선택하세요." : "확인했으면 <color=#FFD166>뒤로가기</color>를 누르세요.";
                    note = game.Modal == GameModal.None ? "강화와 에너지 변환을 할 수 있어요." : game.Modal == GameModal.Upgrade ? "강화 탭에서 필요한 재료를 확인해요." : "강화하지 않아도 안내를 마칠 수 있어요."; break;
                case TutorialPrompt.Switch:
                    caption = "곡괭이 교체";
                    for (int i = 0; i < game.Session.Picks.Length; i++) if (game.Session.Picks[i].Remaining > 0) { focus = new Rect(20 + i * 54, 15, 49, 77); break; }
                    action = "왼쪽 위 <color=#FFD166>다른 곡괭이</color>를 누르세요.";
                    note = "사용 중인 곡괭이를 모두 썼어요."; break;
            }
            FocusBounds = focus; MessageBounds = panel;
            Set(bubbleShadow, new Rect(panel.x + 4, panel.y + 5, panel.width, panel.height)); Set(bubble, panel);
            Set(accent, new Rect(0, 8, 5, panel.height - 16));
            title.text = caption; explanation.text = action; detail.text = note;
            outside[0] = new Rect(0, 0, 480, focus.y); outside[1] = new Rect(0, focus.yMax, 480, 854 - focus.yMax);
            outside[2] = new Rect(0, focus.y, focus.x, focus.height); outside[3] = new Rect(focus.xMax, focus.y, 480 - focus.xMax, focus.height);
            for (int i = 0; i < 4; i++) { shade[i].gameObject.SetActive(dim); Set(shade[i].rectTransform, outside[i]); destinationEdge[i].gameObject.SetActive(destination); }
            Outline(edge, focus, new Color(1, .81f, .4f, .9f + .1f * Mathf.Sin(Time.unscaledTime * 5)));
            if (destination) Outline(destinationEdge, game.View.CellBounds(game.Tutorial.DragDestination), new Color(.45f, 1f, .67f, 1));
        }
        static void Outline(Image[] edges, Rect rect, Color color)
        {
            Set(edges[0].rectTransform, new Rect(rect.x - 2, rect.y - 2, rect.width + 4, 3));
            Set(edges[1].rectTransform, new Rect(rect.x - 2, rect.yMax - 1, rect.width + 4, 3));
            Set(edges[2].rectTransform, new Rect(rect.x - 2, rect.y, 3, rect.height));
            Set(edges[3].rectTransform, new Rect(rect.xMax - 1, rect.y, 3, rect.height));
            for (int i = 0; i < 4; i++) edges[i].color = color;
        }
        static void Set(RectTransform node, Rect rect) { node.anchoredPosition = new Vector2(rect.x, -rect.y); node.sizeDelta = rect.size; }
        static RectTransform Node(Transform parent, string name, Rect rect)
        { var node = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>(); node.SetParent(parent, false); node.anchorMin = node.anchorMax = new Vector2(0, 1); node.pivot = new Vector2(0, 1); Set(node, rect); return node; }
        static Image Box(Transform parent, string name, Color color)
        { var image = Node(parent, name, new Rect()).gameObject.AddComponent<Image>(); image.color = color; image.raycastTarget = false; return image; }
        Text Label(Transform parent, string name, Rect rect, int size, Color color, bool bold = false)
        {
            var text = Node(parent, name, rect).gameObject.AddComponent<Text>(); text.font = game.font != null ? game.font : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = size; text.fontStyle = bold ? FontStyle.Bold : FontStyle.Normal; text.color = color;
            text.alignment = TextAnchor.MiddleLeft; text.lineSpacing = 1.05f; text.supportRichText = true; text.raycastTarget = false; return text;
        }
    }
}
