using UnityEngine;
using UnityEngine.UI;
namespace DigDeep
{
    public sealed class TutorialView : MonoBehaviour
    {
        GameManager game;
        RectTransform layer, bubble;
        Image[] shade = new Image[4], edge = new Image[4], destinationEdge = new Image[4];
        Text title, explanation;
        readonly Rect[] outside = new Rect[4];
        public Rect FocusBounds { get; private set; }
        public void Initialize(GameManager owner, RectTransform parent)
        {
            game = owner;
            layer = Node(parent, "First play tutorial", new Rect(0, 0, 480, 854));
            for (int i = 0; i < 4; i++) shade[i] = Box(layer, "Focus shade " + i, new Color(0, 0, 0, .28f));
            for (int i = 0; i < 4; i++) { edge[i] = Box(layer, "Focus edge " + i, Color.white); destinationEdge[i] = Box(layer, "Drag destination edge " + i, Color.white); }
            bubble = Node(layer, "Tutorial explanation", new Rect(30, 712, 420, 88));
            var background = bubble.gameObject.AddComponent<Image>(); background.color = new Color32(20, 32, 40, 248); background.raycastTarget = false;
            title = Label(bubble, "Tutorial title", new Rect(14, 2, 392, 24), 13, new Color32(239, 189, 88, 255));
            explanation = Label(bubble, "Tutorial instructions", new Rect(14, 26, 392, 58), 14, Color.white);
            layer.gameObject.SetActive(false);
        }
        public bool BlocksPoint(Vector2 point) => layer.gameObject.activeSelf && new Rect(bubble.anchoredPosition.x, -bubble.anchoredPosition.y, bubble.sizeDelta.x, bubble.sizeDelta.y).Contains(point);
        public void Refresh()
        {
            var prompt = game.Tutorial.Prompt;
            bool resetting = game.Modal == GameModal.ResetWarning || game.Modal == GameModal.ResetConfirm;
            layer.gameObject.SetActive(prompt != TutorialPrompt.None && !resetting);
            if (!layer.gameObject.activeSelf) return;
            Rect focus = new Rect(40, 585, 120, 100); bool dim = true, destination = false;
            Rect panel = game.Modal == GameModal.None ? new Rect(30, 712, 420, 88) : new Rect(30, 736, 420, 76);
            if (prompt == TutorialPrompt.Dig) panel = new Rect(30, 620, 420, 105);
            string caption = "", text = "";
            switch (prompt)
            {
                case TutorialPrompt.Dig:
                    focus = game.View.ButtonBounds("play"); caption = "첫 채굴 시작";
                    text = game.Session.Tutorial.controlsStep == 0 ? "Dig!! 버튼을 눌러 채굴을 시작하세요." : "Dig!!를 눌러 남은 조작 안내를 이어가세요."; break;
                case TutorialPrompt.Tap:
                    dim = false; caption = "조작 안내 1/3 · 탭";
                    text = "화면을 한 번 눌러보세요.\n한 번 누르면 한 번 타격합니다."; break;
                case TutorialPrompt.Drag:
                    dim = false; focus = game.View.PickBounds(); destination = true; caption = "조작 안내 2/3 · 드래그";
                    text = "곡괭이를 표시된 옆칸으로 끌어놓으세요.\n블록이 있는 도착 칸은 캐고 이동합니다."; break;
                case TutorialPrompt.Hold:
                    dim = false; caption = "조작 안내 3/3 · 홀드";
                    text = "길게 누르면 연속으로 캡니다.\n두 번 이상 캐고 손을 떼서 멈춰보세요."; break;
                case TutorialPrompt.Restart:
                    focus = game.View.ButtonBounds("restart"); caption = "채굴 종료";
                    text = "다시 준비를 누르면 곡괭이가 회복됩니다.\n다음 안내를 이어서 진행할 수 있어요."; break;
                case TutorialPrompt.Inventory:
                    caption = "화면 안내 1/3 · 인벤"; focus = game.Modal == GameModal.None ? game.View.ButtonBounds("inventory") : new Rect(55, 686, 370, 34);
                    text = game.Modal == GameModal.None ? "인벤 버튼을 눌러 보관한 광물을 확인하세요." : "채굴한 광물이 이곳에 보관됩니다.\n뒤로가기를 누르면 다음 안내로 이동합니다."; break;
                case TutorialPrompt.Shop:
                    caption = "화면 안내 2/3 · 상점"; focus = game.Modal == GameModal.None ? game.View.ButtonBounds("shop") : new Rect(55, 686, 370, 34);
                    text = game.Modal == GameModal.None ? "상점 버튼을 눌러보세요." : "곡괭이는 구매 탭, 광물은 판매 탭을 이용해요.\n확인했으면 뒤로가기를 누르세요."; break;
                case TutorialPrompt.Upgrade:
                    caption = "화면 안내 3/3 · 제련";
                    focus = game.Modal == GameModal.None ? game.View.ButtonBounds("upgrade") : game.Modal == GameModal.Upgrade ? new Rect(55, 346, 370, 252) : new Rect(55, 686, 370, 34);
                    text = game.Modal == GameModal.None ? "제련 버튼을 눌러보세요." : game.Modal == GameModal.Upgrade ? "강화 탭에서 곡괭이를 선택해 재료를 확인하세요.\n변환 탭에서는 광물로 에너지를 만들 수 있어요." : "필요 수량과 보유 수량을 비교할 수 있어요.\n뒤로가기를 눌러 안내를 완료하세요."; break;
                case TutorialPrompt.Switch:
                    caption = "곡괭이 교체";
                    for (int i = 0; i < game.Session.Picks.Length; i++) if (game.Session.Picks[i].Remaining > 0) { focus = new Rect(20 + i * 54, 15, 49, 77); break; }
                    text = "사용 중인 곡괭이를 모두 사용했어요.\n왼쪽 위에서 다른 곡괭이를 선택하세요."; break;
            }
            FocusBounds = focus;
            Set(bubble, panel); explanation.rectTransform.sizeDelta = new Vector2(392, panel.height - 28);
            title.text = caption; explanation.text = text;
            outside[0] = new Rect(0, 0, 480, focus.y); outside[1] = new Rect(0, focus.yMax, 480, 854 - focus.yMax);
            outside[2] = new Rect(0, focus.y, focus.x, focus.height); outside[3] = new Rect(focus.xMax, focus.y, 480 - focus.xMax, focus.height);
            for (int i = 0; i < 4; i++) { shade[i].gameObject.SetActive(dim); Set(shade[i].rectTransform, outside[i]); destinationEdge[i].gameObject.SetActive(destination); }
            Outline(edge, focus, new Color(1, .77f, .3f, .65f + .3f * Mathf.Sin(Time.unscaledTime * 5)));
            if (destination) Outline(destinationEdge, game.View.CellBounds(game.Tutorial.DragDestination), new Color(.45f, 1f, .67f, .9f));
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
        Text Label(Transform parent, string name, Rect rect, int size, Color color)
        { var text = Node(parent, name, rect).gameObject.AddComponent<Text>(); text.font = game.font != null ? game.font : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); text.fontSize = size; text.color = color; text.alignment = TextAnchor.MiddleCenter; text.raycastTarget = false; return text; }
    }
}
