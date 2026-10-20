using UnityEngine;
using UnityEngine.UI;
namespace DigDeep.Presentation
{
    // Optional module. Remove the PresentationTools child to remove only this button.
    [DisallowMultipleComponent, DefaultExecutionOrder(10)]
    public sealed class PresentationResourcesButton : MonoBehaviour
    {
        public int money = 20000, coal = 200, iron = 100, gold = 30;
        GameManager game;
        GameObject button;
        const string Id = "presentation-resources";
        void Awake() => Attach();
        void OnEnable() => Attach();
        void Start() => Attach();
        void Attach()
        {
            if (button != null) return;
            game = GetComponentInParent<GameManager>();
            if (game == null || game.View == null) return;
            var area = GameUILayout.Presentation;
            var target = new GameObject("Presentation resource button", typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>();
            target.SetParent(game.View.UiRoot, false); target.anchorMin = target.anchorMax = new Vector2(0, 1); target.pivot = new Vector2(0, 1);
            target.anchoredPosition = new Vector2(area.x, -area.y); target.sizeDelta = area.size;
            var image = target.GetComponent<Image>(); image.color = new Color32(48, 63, 72, 255); image.raycastTarget = false;
            var caption = new GameObject("Label", typeof(RectTransform), typeof(Text)).GetComponent<Text>();
            caption.rectTransform.SetParent(target, false); caption.rectTransform.anchorMin = Vector2.zero; caption.rectTransform.anchorMax = Vector2.one;
            caption.rectTransform.offsetMin = new Vector2(4, 3); caption.rectTransform.offsetMax = new Vector2(-4, -3);
            caption.font = game.font != null ? game.font : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            caption.fontSize = 13; caption.alignment = TextAnchor.MiddleCenter; caption.text = "발표용 재화 지급"; caption.color = new Color32(239, 189, 88, 255); caption.raycastTarget = false;
            button = target.gameObject;
            game.View.RegisterOptionalButton(Id, target, Available, Grant);
            button.SetActive(Available());
        }
        bool Available() => isActiveAndEnabled && game != null && game.Session.State == SessionState.Ready && game.Modal == GameModal.None;
        void Grant()
        {
            if (!Available()) return;
            if (game.Session.GrantResources(money, coal, iron, gold)) game.View.ShowNotice("발표용 재화를 지급했어요.", false);
        }
        void OnDisable()
        {
            if (game != null && game.View != null) game.View.UnregisterOptionalButton(Id);
            if (button != null) { button.SetActive(false); if (Application.isPlaying) Destroy(button); else DestroyImmediate(button); button = null; }
        }
    }
}
