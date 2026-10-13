using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
namespace DigDeep
{
    // Trading UI shares the game's single pointer dispatcher; no second input module.
    public sealed class GameMenus : MonoBehaviour
    {
        GameManager game; RectTransform layer;
        Text title, body, wallet, buyLabel, saleInfo, quantity, confirmLabel;
        Image buyButton, saleButton, confirmButton;
        RectTransform shop, purchase, sale, saleFooter, inventory, upgrades, confirmation;
        Image[] pickCards = new Image[4], pickIcons = new Image[4];
        Text[] pickLabels = new Text[4];
        Image buyTab, sellTab;
        Text saleEmpty, inventoryEmpty;
        sealed class OreRow { public OreEntry Entry; public Image Card; public Text Label; }
        sealed class OreList
        {
            public Rect Area; public RectTransform Content; public OreRow[] Rows;
            public readonly List<OreRow> Visible = new List<OreRow>();
            public float Offset, RowHeight, Maximum;
        }
        OreList saleList, inventoryList, scrolling;
        float scrollOrigin;
        readonly Dictionary<string, Rect> actions = new Dictionary<string, Rect>();
        Color dark = new Color32(20, 30, 38, 255), gold = new Color32(239, 189, 88, 255), muted = new Color32(48, 62, 72, 255);
        Font font;
        public void Initialize(GameManager owner, RectTransform parent)
        {
            game = owner; font = game.font != null ? game.font : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            layer = Node(parent, "Trading and confirmation", new Rect(0, 0, 480, 854));
            Box(layer, "Backdrop", new Rect(0, 0, 480, 854), new Color(0, 0, 0, .78f));
            Box(layer, "Panel", new Rect(25, 202, 430, 530), dark);
            Box(layer, "Accent", new Rect(25, 202, 430, 3), gold);
            title = Label(layer, "Title", new Rect(45, 218, 390, 42), "", 27, gold);
            wallet = Label(layer, "Wallet", new Rect(45, 260, 390, 26), "", 15, Color.white);
            shop = Node(layer, "Shop content", new Rect(0, 0, 480, 854));
            buyTab = Action(shop, "shop-buy", new Rect(55, 298, 178, 44), "구매", muted, Color.white);
            sellTab = Action(shop, "shop-sell", new Rect(247, 298, 178, 44), "판매", muted, Color.white);
            purchase = Node(shop, "Purchase page", new Rect(0, 0, 480, 854));
            GameView.TiltPickIcon(Box(purchase, "Wood pickaxe", new Rect(201, 365, 78, 78), Color.white, game.wood));
            buyButton = Action(purchase, "buy", new Rect(55, 470, 370, 55), "", gold, dark);
            buyLabel = buyButton.GetComponentInChildren<Text>();
            Label(purchase, "Ownership", new Rect(55, 544, 370, 52), "추가 구매는 나무 곡괭이\n최대 4개까지 보유할 수 있어요", 15, Color.white);
            sale = Node(shop, "Sale page", new Rect(0, 0, 480, 854));
            saleList = CreateList(sale, "Discovered ore sale list", new Rect(55, 356, 370, 176), 88);
            saleEmpty = Label(sale, "No discovered ores", new Rect(55, 392, 370, 92), "아직 확인한 광물이 없어요.\n광물을 채굴하면 판매 목록에 나타나요.", 16, Color.white);
            saleFooter = Node(sale, "Fixed sale controls", new Rect(0, 0, 480, 854));
            saleInfo = Label(saleFooter, "Sale description", new Rect(55, 540, 370, 38), "", 13, Color.white);
            Action(saleFooter, "sale-less", new Rect(55, 583, 64, 40), "−", muted, Color.white, 24);
            quantity = Label(saleFooter, "Sale amount", new Rect(128, 583, 130, 40), "", 21, Color.white);
            Action(saleFooter, "sale-more", new Rect(265, 583, 64, 40), "+", muted, Color.white, 24);
            Action(saleFooter, "sale-all", new Rect(340, 583, 85, 40), "전부", muted, Color.white);
            saleButton = Action(saleFooter, "sell", new Rect(55, 635, 370, 40), "판매", gold, dark);
            inventory = Node(layer, "Inventory content", new Rect(0, 0, 480, 854));
            inventoryList = CreateList(inventory, "Discovered inventory list", new Rect(55, 319, 370, 256), 112);
            inventoryEmpty = Label(inventory, "Empty inventory", new Rect(55, 389, 370, 80), "아직 확인한 광물이 없어요.\n채굴한 광물이 여기에 보관됩니다.", 16, Color.white);
            Label(inventory, "Sale hint", new Rect(45, 594, 390, 34), "광물은 상점에서 판매할 수 있어요", 15, gold);
            upgrades = Node(layer, "Upgrade selection", new Rect(0, 0, 480, 854));
            for (int i = 0; i < 4; i++)
            {
                var rect = new Rect(55 + i % 2 * 192, 313 + i / 2 * 133, 178, 119);
                pickCards[i] = Action(upgrades, "upgrade-pick" + i, rect, "", muted, Color.white);
                pickCards[i].GetComponentInChildren<Text>().gameObject.SetActive(false);
                pickIcons[i] = Box(pickCards[i].transform, "Icon", new Rect(62, 7, 54, 54), Color.white);
                GameView.TiltPickIcon(pickIcons[i]);
                pickLabels[i] = Label(pickCards[i].transform, "Stats", new Rect(5, 64, 168, 47), "", 13, Color.white);
            }
            Label(upgrades, "Upgrade hint", new Rect(45, 606, 390, 30), "강화할 곡괭이를 선택하세요", 15, gold);
            confirmation = Node(layer, "Confirmation content", new Rect(0, 0, 480, 854));
            body = Label(confirmation, "Requirements", new Rect(48, 315, 384, 210), "", 18, Color.white);
            confirmButton = Action(confirmation, "confirm", new Rect(55, 565, 370, 55), "", gold, dark);
            confirmLabel = confirmButton.GetComponentInChildren<Text>();
            Action(layer, "close", new Rect(55, 686, 370, 34), "뒤로가기", muted, Color.white);
            layer.gameObject.SetActive(false);
        }
        public void Refresh()
        {
            var modal = game.Modal; var session = game.Session;
            layer.gameObject.SetActive(modal != GameModal.None);
            if (modal == GameModal.None) return;
            shop.gameObject.SetActive(modal == GameModal.Shop); inventory.gameObject.SetActive(modal == GameModal.Inventory);
            upgrades.gameObject.SetActive(modal == GameModal.Upgrade);
            confirmation.gameObject.SetActive(modal == GameModal.UpgradeConfirm || modal == GameModal.ResetWarning || modal == GameModal.ResetConfirm);
            wallet.text = "보유 금액  " + session.Money.ToString("N0") + "원";
            if (modal == GameModal.Shop)
            {
                title.text = "상점";
                bool selling = game.ShopTab == ShopPage.Sell;
                purchase.gameObject.SetActive(!selling); sale.gameObject.SetActive(selling);
                buyTab.color = !selling ? new Color32(68, 108, 112, 255) : muted;
                sellTab.color = selling ? new Color32(68, 108, 112, 255) : muted;
                buyLabel.text = session.PurchasePrice < 0 ? "보유 곡괭이 4개 · 구매 완료" : "나무 곡괭이 구매  " + session.PurchasePrice.ToString("N0") + "원";
                buyButton.color = session.CanBuy ? gold : muted;
                buyLabel.color = session.CanBuy ? dark : new Color32(173, 185, 191, 255);
                UpdateList(saleList, true); saleEmpty.gameObject.SetActive(saleList.Visible.Count == 0);
                var selected = OreCatalog.Find(game.SaleKind);
                saleFooter.gameObject.SetActive(selected != null && session.IsDiscovered(game.SaleKind));
                saleInfo.text = selected == null ? "" : selected.Name + " · 개당 " + game.settings.SellPrice(game.SaleKind) + "원";
                quantity.text = game.SaleQuantity.ToString();
                long total = (long)game.SaleQuantity * game.settings.SellPrice(game.SaleKind);
                saleButton.GetComponentInChildren<Text>().text = "판매  ·  " + total.ToString("N0") + "원";
                bool canSell = session.CanSell(game.SaleKind, game.SaleQuantity);
                saleButton.color = canSell ? gold : muted;
                saleButton.GetComponentInChildren<Text>().color = canSell ? dark : new Color32(173, 185, 191, 255);
            }
            else if (modal == GameModal.Inventory)
            {
                title.text = "인벤토리";
                UpdateList(inventoryList, false); inventoryEmpty.gameObject.SetActive(inventoryList.Visible.Count == 0);
            }
            else if (modal == GameModal.Upgrade)
            {
                title.text = "곡괭이 강화";
                for (int i = 0; i < 4; i++)
                {
                    pickCards[i].gameObject.SetActive(i < session.Picks.Length); if (i >= session.Picks.Length) continue;
                    var pick = session.Picks[i]; pickIcons[i].sprite = game.PickSprite(pick.Name);
                    pickLabels[i].text = (i + 1) + "번 " + PickName(pick.Name) + "\n피해 " + pick.Power + " · 내구도 " + pick.Maximum;
                }
            }
            else if (modal == GameModal.UpgradeConfirm)
            {
                title.text = "강화 확인"; int i = game.UpgradeIndex; var pick = session.Picks[i];
                int cost = session.UpgradeCost(i), ore = session.UpgradeOre(i), coal = session.UpgradeCoal(i), goldOre = session.UpgradeGold(i);
                if (cost < 0) body.text = (i + 1) + "번 " + PickName(pick.Name) + "\n현재 마지막 강화 단계입니다.";
                else body.text = (i + 1) + "번 " + PickName(pick.Name) + " → " + PickName(GameSettings.NextPick(pick.Name))
                    + "\n\n필요 금액 " + cost + "원 / 보유 " + session.Money + "원"
                    + (coal > 0 ? "\n석탄 " + coal + "개 / 보유 " + session.Coal + "개" : "")
                    + (ore > 0 ? "\n철광석 " + ore + "개 / 보유 " + session.Iron + "개" : "")
                    + (goldOre > 0 ? "\n금광석 " + goldOre + "개 / 보유 " + session.Gold + "개" : "")
                    + (session.CanUpgrade(i) ? "\n강화할 수 있습니다." : "\n재료 또는 돈이 부족합니다.");
                confirmButton.gameObject.SetActive(cost >= 0);
                confirmButton.color = session.CanUpgrade(i) ? gold : muted; confirmLabel.text = "강화";
                confirmLabel.color = session.CanUpgrade(i) ? dark : new Color32(173, 185, 191, 255);
            }
            else
            {
                bool second = modal == GameModal.ResetConfirm;
                title.text = second ? "정말 초기화할까요?" : "데이터 초기화";
                body.text = second ? "삭제한 데이터는 복구할 수 없습니다.\n돈 · 광물 · 곡괭이 · 강화 정보를 지우고\n나무 곡괭이 하나로 시작합니다." : "돈 · 광물 · 소유 곡괭이 · 강화 정보가\n모두 초기화됩니다.\n계속하시겠습니까?";
                confirmButton.gameObject.SetActive(true); confirmButton.color = new Color32(175, 61, 50, 255);
                confirmLabel.text = second ? "데이터 초기화" : "계속";
                confirmLabel.color = Color.white;
            }
        }
        public string ActionAt(Vector2 point)
        {
            var modal = game.Modal; var s = game.Session;
            if (actions["close"].Contains(point)) return "close";
            if (modal == GameModal.Shop)
            {
                if (actions["shop-buy"].Contains(point)) return "shop-buy";
                if (actions["shop-sell"].Contains(point)) return "shop-sell";
                if (game.ShopTab == ShopPage.Buy) return actions["buy"].Contains(point) && s.CanBuy ? "buy" : "blocked";
                UpdateList(saleList, true);
                if (saleList.Area.Contains(point))
                {
                    int index = Mathf.FloorToInt((point.y - saleList.Area.y + saleList.Offset) / saleList.RowHeight);
                    return index >= 0 && index < saleList.Visible.Count ? saleList.Visible[index].Entry.Action : "blocked";
                }
                if (!s.IsDiscovered(game.SaleKind)) return "blocked";
                foreach (var action in new[] { "sale-less", "sale-more", "sale-all" })
                    if (actions[action].Contains(point)) return action;
                if (actions["sell"].Contains(point)) return s.CanSell(game.SaleKind, game.SaleQuantity) ? "sell" : "blocked";
            }
            else if (modal == GameModal.Upgrade)
            {
                for (int i = 0; i < s.Picks.Length; i++) if (actions["upgrade-pick" + i].Contains(point)) return "upgrade-pick" + i;
            }
            else if (actions["confirm"].Contains(point))
            {
                if (modal == GameModal.ResetWarning) return "reset-next";
                if (modal == GameModal.ResetConfirm) return "reset-confirm";
                if (modal == GameModal.UpgradeConfirm && s.CanUpgrade(game.UpgradeIndex)) return "upgrade-confirm";
            }
            return "blocked";
        }
        OreList CreateList(Transform parent, string name, Rect area, float height)
        {
            var viewport = Node(parent, name, area); viewport.gameObject.AddComponent<RectMask2D>();
            var list = new OreList { Area = area, RowHeight = height,
                Content = Node(viewport, "Scrollable content", new Rect(0, 0, area.width, 0)), Rows = new OreRow[OreCatalog.All.Length] };
            for (int i = 0; i < list.Rows.Length; i++)
            {
                var entry = OreCatalog.All[i];
                var card = Box(list.Content, entry.Id, new Rect(0, 0, area.width, height - 6), muted);
                Box(card.transform, "Icon", new Rect(12, 10, height - 26, height - 26), Color.white, game.BlockSprite(entry.Kind, -1));
                list.Rows[i] = new OreRow { Entry = entry, Card = card,
                    Label = Label(card.transform, "Ore information", new Rect(height, 6, area.width - height - 12, height - 18), "", 15, Color.white) };
            }
            return list;
        }
        void UpdateList(OreList list, bool selectable)
        {
            list.Visible.Clear();
            foreach (var row in list.Rows)
            {
                bool known = game.Session.IsDiscovered(row.Entry.Kind); row.Card.gameObject.SetActive(known);
                if (!known) continue;
                row.Card.rectTransform.anchoredPosition = new Vector2(0, -list.Visible.Count * list.RowHeight);
                row.Card.color = selectable && row.Entry.Kind == game.SaleKind ? new Color32(68, 108, 112, 255) : muted;
                row.Label.text = row.Entry.Name + "  " + game.Session.Stock(row.Entry.Kind) + "개\n개당 " + game.settings.SellPrice(row.Entry.Kind) + "원"
                    + (!selectable && row.Entry.UpgradeMaterial ? " · 강화 재료" : "");
                list.Visible.Add(row);
            }
            list.Content.sizeDelta = new Vector2(list.Area.width, list.Visible.Count * list.RowHeight);
            list.Maximum = Mathf.Max(0, list.Content.sizeDelta.y - list.Area.height);
            list.Offset = Mathf.Clamp(list.Offset, 0, list.Maximum); list.Content.anchoredPosition = new Vector2(0, list.Offset);
        }
        OreList ListAt(Vector2 point)
        {
            if (game.Modal == GameModal.Shop && game.ShopTab == ShopPage.Sell && saleList.Area.Contains(point)) return saleList;
            if (game.Modal == GameModal.Inventory && inventoryList.Area.Contains(point)) return inventoryList;
            return null;
        }
        public bool BeginScroll(Vector2 point)
        { scrolling = ListAt(point); if (scrolling == null) return false; UpdateList(scrolling, scrolling == saleList); scrollOrigin = scrolling.Offset; return true; }
        public void DragScroll(float distance)
        { if (scrolling == null) return; scrolling.Offset = Mathf.Clamp(scrollOrigin - distance, 0, scrolling.Maximum); scrolling.Content.anchoredPosition = new Vector2(0, scrolling.Offset); }
        public void EndScroll() { scrolling = null; }
        public void Wheel(Vector2 point, float distance)
        { if (Mathf.Approximately(distance, 0)) return; var list = ListAt(point); if (list == null) return; UpdateList(list, list == saleList); list.Offset = Mathf.Clamp(list.Offset - distance, 0, list.Maximum); list.Content.anchoredPosition = new Vector2(0, list.Offset); }
        public static string PickName(string id) => id == "ALLOY" ? "합금 곡괭이" : id == "IRON" ? "철 곡괭이" : id == "STONE" ? "돌 곡괭이" : "나무 곡괭이";
        Image Action(Transform parent, string id, Rect rect, string caption, Color color, Color textColor, int size = 17)
        { actions[id] = rect; var image = Box(parent, id, rect, color); Label(image.transform, "Label", new Rect(0, 0, rect.width, rect.height), caption, size, textColor); return image; }
        static RectTransform Node(Transform parent, string name, Rect rect)
        { var r = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>(); r.SetParent(parent, false); r.anchorMin = r.anchorMax = new Vector2(0, 1); r.pivot = new Vector2(0, 1); r.anchoredPosition = new Vector2(rect.x, -rect.y); r.sizeDelta = rect.size; return r; }
        static Image Box(Transform parent, string name, Rect rect, Color color, Sprite sprite = null)
        { var image = Node(parent, name, rect).gameObject.AddComponent<Image>(); image.color = color; image.sprite = sprite; image.raycastTarget = false; return image; }
        Text Label(Transform parent, string name, Rect rect, string value, int size, Color color)
        { var text = Node(parent, name, rect).gameObject.AddComponent<Text>(); text.font = font; text.fontSize = size; text.fontStyle = size >= 17 ? FontStyle.Bold : FontStyle.Normal; text.text = value; text.color = color; text.alignment = TextAnchor.MiddleCenter; text.raycastTarget = false; return text; }
    }
}
