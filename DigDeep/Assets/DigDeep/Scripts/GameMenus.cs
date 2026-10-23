using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
namespace DigDeep
{
    // Trading UI shares the game's single pointer dispatcher; no second input module.
    public sealed class GameMenus : MonoBehaviour
    {
        GameManager game; RectTransform layer;
        int displayedRevision = -1; GameModal displayedModal; MiningSession displayedSession;
        Text title, body, wallet, buyLabel, saleInfo, quantity, confirmLabel;
        Image buyButton, saleButton, confirmButton;
        RectTransform shop, purchase, sale, saleFooter, inventory, upgrades, confirmation;
        Image[] pickCards = new Image[4], pickIcons = new Image[4];
        Text[] pickLabels = new Text[4];
        Image buyTab, sellTab;
        RectTransform refinery, conversion, requirementTable, equipmentSelection;
        readonly Image[] equipmentCards = new Image[4];
        readonly Text[] equipmentLabels = new Text[4];
        Image upgradeTab, convertTab, conversionButton, converterImage;
        Text upgradeSpeed;
        Text energy, conversionInfo, conversionAmount, conversionGain, upgradeSummary, requirementStatus;
        sealed class RequirementRow { public Image Background, Icon; public Text Name, Needed, Owned; }
        readonly RequirementRow[] requirementRows = new RequirementRow[4];
        Text saleEmpty, inventoryEmpty;
        sealed class OreRow { public OreEntry Entry; public Image Card; public Text Label; }
        sealed class OreList
        {
            public Rect Area; public RectTransform Content; public OreRow[] Rows;
            public readonly List<OreRow> Visible = new List<OreRow>();
            public float Offset, RowHeight, Maximum;
        }
        OreList saleList, inventoryList, conversionList, scrolling;
        Mineral shownConversion = Mineral.Stone;
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
            var energyCard = Box(inventory, "Energy inventory card", new Rect(55, 299, 370, 58), muted);
            Box(energyCard.transform, "Energy accent", new Rect(0, 0, 4, 58), new Color32(63, 216, 231, 255));
            var energyIcon = Box(energyCard.transform, "Energy icon", new Rect(14, 14, 30, 30), game.energyIcon != null ? Color.white : new Color32(63, 216, 231, 255), game.energyIcon); energyIcon.preserveAspect = true;
            var energyName = Label(energyCard.transform, "Energy name", new Rect(54, 0, 90, 58), "에너지", 18, Color.white); energyName.alignment = TextAnchor.MiddleLeft;
            energy = Label(energyCard.transform, "Energy amount", new Rect(147, 0, 205, 58), "", 24, new Color32(101, 233, 242, 255)); energy.alignment = TextAnchor.MiddleRight;
            inventoryList = CreateList(inventory, "Discovered inventory list", new Rect(55, 368, 370, 207), 112);
            inventoryEmpty = Label(inventory, "Empty inventory", new Rect(55, 389, 370, 80), "아직 확인한 광물이 없어요.\n채굴한 광물이 여기에 보관됩니다.", 16, Color.white);
            Label(inventory, "Sale hint", new Rect(45, 594, 390, 34), "광물은 상점에서 판매할 수 있어요", 15, gold);
            refinery = Node(layer, "Refinery content", new Rect(0, 0, 480, 854));
            upgradeTab = Action(refinery, "refinery-upgrade", new Rect(55, 298, 178, 36), "강화", muted, Color.white);
            convertTab = Action(refinery, "refinery-convert", new Rect(247, 298, 178, 36), "변환", muted, Color.white);
            upgrades = Node(refinery, "Upgrade selection", new Rect(0, 0, 480, 854));
            for (int i = 0; i < 4; i++)
            {
                var rect = new Rect(55 + i % 2 * 192, 346 + i / 2 * 126, 178, 119);
                pickCards[i] = Action(upgrades, "upgrade-pick" + i, rect, "", muted, Color.white);
                pickCards[i].GetComponentInChildren<Text>().gameObject.SetActive(false);
                pickIcons[i] = Box(pickCards[i].transform, "Icon", new Rect(62, 7, 54, 54), Color.white);
                GameView.TiltPickIcon(pickIcons[i]);
                pickLabels[i] = Label(pickCards[i].transform, "Stats", new Rect(5, 64, 168, 47), "", 13, Color.white);
            }
            Label(upgrades, "Upgrade hint", new Rect(45, 606, 390, 30), "강화할 장비를 선택하세요", 15, gold);
            CreateConversion();
            confirmation = Node(layer, "Confirmation content", new Rect(0, 0, 480, 854));
            body = Label(confirmation, "Requirements", new Rect(48, 315, 384, 210), "", 18, Color.white);
            CreateRequirements(); CreateEquipmentChoices();
            confirmButton = Action(confirmation, "confirm", new Rect(55, 565, 370, 55), "", gold, dark);
            confirmLabel = confirmButton.GetComponentInChildren<Text>();
            Action(layer, "close", new Rect(55, 686, 370, 34), "뒤로가기", muted, Color.white);
            layer.gameObject.SetActive(false);
        }
        public void Refresh()
        {
            var modal = game.Modal; var session = game.Session;
            layer.gameObject.SetActive(modal != GameModal.None);
            if (modal == GameModal.None) { displayedRevision = -1; return; }
            if (displayedRevision == game.MenuRevision && displayedModal == modal && displayedSession == session && Time.unscaledTime - game.LastConversionTime > .35f) return;
            displayedRevision = game.MenuRevision; displayedModal = modal; displayedSession = session;
            equipmentSelection.gameObject.SetActive(modal == GameModal.EquipmentChoice);
            shop.gameObject.SetActive(modal == GameModal.Shop); inventory.gameObject.SetActive(modal == GameModal.Inventory);
            refinery.gameObject.SetActive(modal == GameModal.Upgrade);
            upgrades.gameObject.SetActive(game.RefineryTab == RefineryPage.Upgrade);
            conversion.gameObject.SetActive(game.RefineryTab == RefineryPage.Conversion);
            confirmation.gameObject.SetActive(modal == GameModal.UpgradeConfirm || modal == GameModal.ResetWarning || modal == GameModal.ResetConfirm);
            bool upgrading = modal == GameModal.UpgradeConfirm;
            requirementTable.gameObject.SetActive(upgrading); body.gameObject.SetActive(!upgrading);
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
                energy.text = GameSettings.FormatEnergy(session.EnergyTenths);
                UpdateList(inventoryList, false); inventoryEmpty.gameObject.SetActive(inventoryList.Visible.Count == 0);
            }
            else if (modal == GameModal.Upgrade)
            {
                title.text = "제련";
                upgradeTab.color = game.RefineryTab == RefineryPage.Upgrade ? new Color32(68, 108, 112, 255) : muted;
                convertTab.color = game.RefineryTab == RefineryPage.Conversion ? new Color32(68, 108, 112, 255) : muted;
                if (game.RefineryTab == RefineryPage.Conversion) RefreshConversion();
                for (int i = 0; i < 4; i++)
                {
                    pickCards[i].gameObject.SetActive(i < session.Picks.Length); if (i >= session.Picks.Length) continue;
                    var pick = session.Picks[i]; pickIcons[i].sprite = game.PickSprite(pick.Name);
                    pickLabels[i].text = (i + 1) + "번 " + PickName(pick.Name) + "\n피해 " + pick.Power + " · 내구도 " + pick.Maximum;
                }
            }
            else if (modal == GameModal.EquipmentChoice)
            {
                title.text = "장비 루트 선택";
                for (int i = 0; i < GameSettings.EquipmentChoices.Length; i++)
                {
                    string id = GameSettings.EquipmentChoices[i]; bool owned = session.HasEquipment(id);
                    equipmentCards[i].color = owned ? new Color32(42, 48, 53, 255) : muted;
                    equipmentLabels[i].text = PickName(id) + "\n내구도 " + game.settings.Durability(id) + " · 피해 " + game.settings.Power(id)
                        + "\n" + (id == "DYNAMITE" ? "한 판 최대 " + game.settings.Durability(id) + "회" : "속도 " + game.settings.SpeedMultiplier(id).ToString("0.##") + "배") + "\n" + (owned ? "보유 중" : EquipmentTrait(id));
                    equipmentLabels[i].color = owned ? new Color32(135, 145, 150, 255) : Color.white;
                }
            }
            else if (modal == GameModal.UpgradeConfirm)
            {
                title.text = "강화 확인"; int i = game.UpgradeIndex;
                int cost = session.UpgradeCost(i, game.UpgradeTarget); RefreshRequirements(i);
                confirmButton.gameObject.SetActive(cost >= 0);
                confirmButton.color = session.CanUpgrade(i, game.UpgradeTarget) ? gold : muted; confirmLabel.text = "강화";
                confirmLabel.color = session.CanUpgrade(i, game.UpgradeTarget) ? dark : new Color32(173, 185, 191, 255);
            }
            else
            {
                bool second = modal == GameModal.ResetConfirm;
                title.text = second ? "정말 초기화할까요?" : "데이터 초기화";
                body.text = second ? "삭제한 데이터는 복구할 수 없습니다.\n돈 · 광물 · 에너지 · 곡괭이와\n기록 · 튜토리얼을 모두 지웁니다.\n나무 곡괭이 하나로 시작합니다." : "돈 · 광물 · 에너지 · 소유 곡괭이와\n강화 · 최고 기록 · 튜토리얼 진행이\n모두 초기화됩니다.\n계속하시겠습니까?";
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
            else if (modal == GameModal.EquipmentChoice)
            {
                for (int i = 0; i < GameSettings.EquipmentChoices.Length; i++)
                    if (actions["equipment-" + i].Contains(point)) return s.HasEquipment(GameSettings.EquipmentChoices[i]) ? "blocked" : "equipment-" + i;
            }
            else if (modal == GameModal.Upgrade)
            {
                if (actions["refinery-upgrade"].Contains(point)) return "refinery-upgrade";
                if (actions["refinery-convert"].Contains(point)) return "refinery-convert";
                if (game.RefineryTab == RefineryPage.Conversion)
                {
                    UpdateList(conversionList, true);
                    if (conversionList.Area.Contains(point))
                    {
                        int index = Mathf.FloorToInt((point.y - conversionList.Area.y + conversionList.Offset) / conversionList.RowHeight);
                        return index >= 0 && index < conversionList.Visible.Count ? "convert-ore-" + conversionList.Visible[index].Entry.Id : "blocked";
                    }
                    foreach (var action in new[] { "convert-less", "convert-more", "convert-all" }) if (actions[action].Contains(point)) return action;
                    if (actions["convert"].Contains(point) && s.CanConvert(game.ConversionKind, game.ConversionQuantity)) return "convert";
                    return "blocked";
                }
                for (int i = 0; i < s.Picks.Length; i++) if (actions["upgrade-pick" + i].Contains(point)) return "upgrade-pick" + i;
            }
            else if (actions["confirm"].Contains(point))
            {
                if (modal == GameModal.ResetWarning) return "reset-next";
                if (modal == GameModal.ResetConfirm) return "reset-confirm";
                if (modal == GameModal.UpgradeConfirm && s.CanUpgrade(game.UpgradeIndex, game.UpgradeTarget)) return "upgrade-confirm";
            }
            return "blocked";
        }
        OreList CreateList(Transform parent, string name, Rect area, float height, OreEntry[] entries = null)
        {
            entries = entries ?? OreCatalog.All;
            var viewport = Node(parent, name, area); viewport.gameObject.AddComponent<RectMask2D>();
            var list = new OreList { Area = area, RowHeight = height,
                Content = Node(viewport, "Scrollable content", new Rect(0, 0, area.width, 0)), Rows = new OreRow[entries.Length] };
            for (int i = 0; i < list.Rows.Length; i++)
            {
                var entry = entries[i];
                var card = Box(list.Content, entry.Id, new Rect(0, 0, area.width, height - 6), muted);
                Box(card.transform, "Icon", new Rect(12, 10, height - 26, height - 26), entry.Kind == Mineral.Oil ? Color.black : Color.white, game.BlockSprite(entry.Kind, -1));
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
                bool converting = list == conversionList;
                bool known = converting || game.Session.IsDiscovered(row.Entry.Kind); row.Card.gameObject.SetActive(known);
                if (!known) continue;
                row.Card.rectTransform.anchoredPosition = new Vector2(0, -list.Visible.Count * list.RowHeight);
                row.Card.color = selectable && row.Entry.Kind == (converting ? game.ConversionKind : game.SaleKind) ? new Color32(68, 108, 112, 255) : muted;
                row.Label.text = row.Entry.Name + "  " + game.Session.Stock(row.Entry.Kind).ToString("N0") + "개\n"
                    + (converting ? "1개 → 에너지 " + GameSettings.FormatEnergy(game.settings.ConversionRate(row.Entry.Kind)) : "개당 " + game.settings.SellPrice(row.Entry.Kind) + "원" + (!selectable && row.Entry.UpgradeMaterial ? " · 강화 재료" : ""));
                list.Visible.Add(row);
            }
            list.Content.sizeDelta = new Vector2(list.Area.width, list.Visible.Count * list.RowHeight);
            list.Maximum = Mathf.Max(0, list.Content.sizeDelta.y - list.Area.height);
            list.Offset = Mathf.Clamp(list.Offset, 0, list.Maximum); list.Content.anchoredPosition = new Vector2(0, list.Offset);
        }
        void CreateConversion()
        {
            conversion = Node(refinery, "Mineral conversion", new Rect(0, 0, 480, 854));
            converterImage = Box(conversion, "Energy converter", new Rect(65, 346, 92, 88), Color.white, game.energyConverter); converterImage.preserveAspect = true;
            conversionInfo = Label(conversion, "Conversion balance", new Rect(174, 348, 251, 80), "", 20, new Color32(101, 233, 242, 255));
            Label(conversion, "Choose conversion mineral", new Rect(55, 431, 370, 18), "변환할 광물을 선택하세요", 12, Color.white);
            conversionList = CreateList(conversion, "Convertible mineral list", new Rect(55, 450, 370, 104), 64,
                OreCatalog.All.Where(e => game.settings.ConversionRate(e.Kind) > 0).ToArray());
            Action(conversion, "convert-less", new Rect(55, 565, 64, 36), "−", muted, Color.white, 24);
            conversionAmount = Label(conversion, "Conversion quantity", new Rect(128, 565, 130, 36), "", 21, Color.white);
            Action(conversion, "convert-more", new Rect(265, 565, 64, 36), "+", muted, Color.white, 24);
            Action(conversion, "convert-all", new Rect(340, 565, 85, 36), "전부", muted, Color.white);
            conversionGain = Label(conversion, "Conversion output", new Rect(55, 606, 370, 28), "", 16, Color.white);
            conversionButton = Action(conversion, "convert", new Rect(55, 640, 370, 36), "에너지로 변환", gold, dark);
        }
        void RefreshConversion()
        {
            UpdateList(conversionList, true);
            if (shownConversion != game.ConversionKind)
            {
                int index = conversionList.Visible.FindIndex(row => row.Entry.Kind == game.ConversionKind);
                if (index >= 0)
                {
                    float top = index * conversionList.RowHeight;
                    float offset = conversionList.Offset;
                    if (top < offset) offset = top;
                    else if (top + conversionList.RowHeight > offset + conversionList.Area.height)
                        offset = top + conversionList.RowHeight - conversionList.Area.height;
                    conversionList.Offset = Mathf.Clamp(offset, 0, conversionList.Maximum);
                    conversionList.Content.anchoredPosition = new Vector2(0, conversionList.Offset);
                }
                shownConversion = game.ConversionKind;
            }
            conversionInfo.text = "보유 에너지\n" + GameSettings.FormatEnergy(game.Session.EnergyTenths);
            conversionAmount.text = game.ConversionQuantity.ToString("N0");
            int rate = game.settings.ConversionRate(game.ConversionKind);
            long gain = (long)rate * game.ConversionQuantity;
            var entry = OreCatalog.Find(game.ConversionKind);
            conversionGain.text = entry == null || rate <= 0 ? "변환 가능한 광물이 없어요." : entry.Name + " " + game.ConversionQuantity.ToString("N0") + "개 → 에너지 " + GameSettings.FormatEnergy((int)System.Math.Min(int.MaxValue, gain));
            bool enabled = game.Session.CanConvert(game.ConversionKind, game.ConversionQuantity);
            conversionButton.color = enabled ? gold : muted;
            conversionButton.GetComponentInChildren<Text>().color = enabled ? dark : new Color32(173, 185, 191, 255);
            float age = Time.unscaledTime - game.LastConversionTime;
            converterImage.rectTransform.localScale = Vector3.one * (age >= 0 && age < .35f ? 1 + .05f * Mathf.Sin(age / .35f * Mathf.PI) : 1);
        }
        string EquipmentTrait(string id) => id == "DRILL" ? "빠른 깊이 돌파" : id == "TUNGSTEN" ? "튼튼한 기본형" : id == "CRUSHER" ? "광물 추가 드롭 +" + game.settings.ExtraDrops(id) : "지정 블록 주변 9칸 폭파";
        void CreateEquipmentChoices()
        {
            equipmentSelection = Node(layer, "Equipment branch selection", new Rect(0, 0, 480, 854));
            for (int i = 0; i < GameSettings.EquipmentChoices.Length; i++)
            {
                string id = GameSettings.EquipmentChoices[i];
                var rect = new Rect(55 + i % 2 * 192, 302 + i / 2 * 170, 178, 162);
                var card = equipmentCards[i] = Action(equipmentSelection, "equipment-" + i, rect, "", muted, Color.white);
                card.GetComponentInChildren<Text>().gameObject.SetActive(false);
                var icon = Box(card.transform, "Equipment icon", new Rect(61, 6, 56, 56), Color.white, game.PickSprite(id)); icon.preserveAspect = true;
                equipmentLabels[i] = Label(card.transform, "Equipment stats", new Rect(3, 67, 172, 91), "", 13, Color.white);
            }
            Label(equipmentSelection, "Shared equipment cost", new Rect(45, 647, 390, 28), "공통 비용 " + game.settings.equipmentCost.ToString("N0") + "원 · 에너지 " + GameSettings.FormatEnergy(game.settings.equipmentEnergyTenths), 14, gold);
        }
        void CreateRequirements()
        {
            requirementTable = Node(confirmation, "Readable requirements", new Rect(0, 0, 480, 854));
            upgradeSpeed = Label(requirementTable, "Upgrade speed", new Rect(55, 631, 370, 28), "", 16, gold);
            upgradeSummary = Label(requirementTable, "Upgrade summary", new Rect(55, 298, 370, 52), "", 16, Color.white);
            Label(requirementTable, "Material heading", new Rect(60, 355, 140, 26), "재료", 14, new Color32(168, 185, 194, 255));
            Label(requirementTable, "Required heading", new Rect(212, 355, 103, 26), "필요", 14, new Color32(168, 185, 194, 255));
            Label(requirementTable, "Owned heading", new Rect(322, 355, 103, 26), "보유", 14, new Color32(168, 185, 194, 255));
            for (int i = 0; i < requirementRows.Length; i++)
            {
                var background = Box(requirementTable, "Requirement row " + i, new Rect(55, 386 + i * 38, 370, 35), muted);
                var icon = Box(background.transform, "Icon", new Rect(6, 6, 23, 23), Color.white); icon.preserveAspect = true;
                var name = Label(background.transform, "Material", new Rect(36, 0, 113, 35), "", 15, Color.white); name.alignment = TextAnchor.MiddleLeft;
                requirementRows[i] = new RequirementRow { Background = background, Icon = icon, Name = name,
                    Needed = Label(background.transform, "Required amount", new Rect(157, 0, 103, 35), "", 18, Color.white),
                    Owned = Label(background.transform, "Owned amount", new Rect(267, 0, 103, 35), "", 18, Color.white) };
            }
            requirementStatus = Label(requirementTable, "Upgrade availability", new Rect(55, 540, 370, 22), "", 14, Color.white);
        }
        void RefreshRequirements(int index)
        {
            var s = game.Session; var pick = s.Picks[index]; var next = game.UpgradeTarget ?? GameSettings.NextPick(pick.Name);
            foreach (var row in requirementRows) row.Background.gameObject.SetActive(false);
            if (next == null)
            {
                upgradeSummary.text = (index + 1) + "번 " + PickName(pick.Name) + "\n현재 마지막 강화 단계입니다.";
                upgradeSpeed.text = "채굴 속도 " + game.settings.SpeedMultiplier(pick.Name).ToString("0.#") + "배";
                requirementStatus.text = "완성된 곡괭이입니다."; requirementStatus.color = Color.white; return;
            }
            upgradeSummary.text = PickName(pick.Name) + " → " + PickName(next) + "\n내구도 " + pick.Maximum + " → " + game.settings.Durability(next) + "  ·  피해 " + pick.Power + " → " + game.settings.Power(next);
            upgradeSpeed.text = "채굴 속도 " + game.settings.SpeedMultiplier(pick.Name).ToString("0.#") + "배 → " + game.settings.SpeedMultiplier(next).ToString("0.#") + "배";
            if (next == "DYNAMITE") upgradeSpeed.text = "블록을 선택해 폭파 · 회당 내구도 1";
            int slot = 0;
            void Row(string name, int needed, int owned, Sprite icon, Color tint, bool energyValue = false, bool moneyValue = false)
            {
                if (needed <= 0) return;
                var row = requirementRows[slot++]; row.Background.gameObject.SetActive(true);
                row.Background.color = owned >= needed ? muted : new Color32(77, 45, 45, 255);
                row.Icon.sprite = icon; row.Icon.color = tint;
                row.Name.text = name;
                string suffix = moneyValue ? "원" : energyValue ? "" : "개";
                row.Needed.text = (energyValue ? GameSettings.FormatEnergy(needed) : needed.ToString("N0")) + suffix;
                row.Owned.text = (energyValue ? GameSettings.FormatEnergy(owned) : owned.ToString("N0")) + suffix;
                row.Owned.fontSize = row.Owned.text.Length > 9 ? 12 : 18;
                row.Owned.color = owned >= needed ? new Color32(163, 235, 187, 255) : new Color32(255, 156, 143, 255);
            }
            Row("돈", s.UpgradeCost(index, game.UpgradeTarget), s.Money, game.moneyIcon, game.moneyIcon != null ? Color.white : gold, moneyValue: true);
            Row("석탄", s.UpgradeCoal(index, game.UpgradeTarget), s.Coal, game.coalOre, Color.white);
            Row("철광석", s.UpgradeOre(index, game.UpgradeTarget), s.Iron, game.ironOre, Color.white);
            Row("금광석", s.UpgradeGold(index, game.UpgradeTarget), s.Gold, game.goldOre, Color.white);
            Row("다이아몬드", s.UpgradeDiamond(index, game.UpgradeTarget), s.Diamond, game.diamondOre, Color.white);
            Row("에너지", s.UpgradeEnergy(index, game.UpgradeTarget), s.EnergyTenths, game.energyIcon, game.energyIcon != null ? Color.white : new Color32(63, 216, 231, 255), energyValue: true);
            bool can = s.CanUpgrade(index, game.UpgradeTarget);
            requirementStatus.text = can ? "강화할 수 있어요." : "붉게 표시된 재료가 부족해요.";
            requirementStatus.color = can ? new Color32(163, 235, 187, 255) : new Color32(255, 156, 143, 255);
        }
        OreList ListAt(Vector2 point)
        {
            if (game.Modal == GameModal.Shop && game.ShopTab == ShopPage.Sell && saleList.Area.Contains(point)) return saleList;
            if (game.Modal == GameModal.Inventory && inventoryList.Area.Contains(point)) return inventoryList;
            if (game.Modal == GameModal.Upgrade && game.RefineryTab == RefineryPage.Conversion && conversionList.Area.Contains(point)) return conversionList;
            return null;
        }
        public bool BeginScroll(Vector2 point)
        { scrolling = ListAt(point); if (scrolling == null) return false; UpdateList(scrolling, scrolling == saleList); scrollOrigin = scrolling.Offset; return true; }
        public void DragScroll(float distance)
        { if (scrolling == null) return; scrolling.Offset = Mathf.Clamp(scrollOrigin - distance, 0, scrolling.Maximum); scrolling.Content.anchoredPosition = new Vector2(0, scrolling.Offset); }
        public void EndScroll() { scrolling = null; }
        public void Wheel(Vector2 point, float distance)
        { if (Mathf.Approximately(distance, 0)) return; var list = ListAt(point); if (list == null) return; UpdateList(list, list == saleList); list.Offset = Mathf.Clamp(list.Offset - distance, 0, list.Maximum); list.Content.anchoredPosition = new Vector2(0, list.Offset); }
        public static string PickName(string id) => id == "DRILL" ? "드릴" : id == "TUNGSTEN" ? "텅스텐 곡괭이" : id == "CRUSHER" ? "분쇄기" : id == "DYNAMITE" ? "다이너마이트" : id == "SUPERALLOY" ? "초합금 곡괭이" : id == "DIAMOND" ? "다이아 곡괭이" : id == "ALLOY" ? "합금 곡괭이" : id == "IRON" ? "철 곡괭이" : id == "STONE" ? "돌 곡괭이" : "나무 곡괭이";
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
