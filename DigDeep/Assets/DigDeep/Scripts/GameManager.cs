using UnityEngine;

namespace DigDeep
{
    public enum GameModal { None, Shop, Inventory, Upgrade, UpgradeConfirm, ResetWarning, ResetConfirm }
    public enum ShopPage { Buy, Sell }
    public enum RefineryPage { Upgrade, Conversion }
    public sealed class GameManager : MonoBehaviour
    {
        public GameSettings settings;
        public Sprite stone, grass, wood, stonePick, iron, alloy, coalOre, ironOre, goldOre;
        public Sprite shopSkin, inventorySkin, upgradeSkin, digSkin;
        public Sprite diamondPick, diamondOre, energyConverter;
        public Font font;
        public MiningSession Session { get; private set; }
        public GameView View { get; private set; }
        public PointerController Controls { get; private set; }
        public TutorialFlow Tutorial { get; private set; }
        public GameModal Modal { get; private set; }
        public int UpgradeIndex { get; private set; }
        public ShopPage ShopTab { get; private set; } = ShopPage.Buy;
        public Mineral SaleKind { get; private set; } = Mineral.Stone;
        public int SaleQuantity { get; private set; }
        public RefineryPage RefineryTab { get; private set; } = RefineryPage.Upgrade;
        public Mineral ConversionKind { get; private set; } = Mineral.Coal;
        public int ConversionQuantity { get; private set; }
        public float LastConversionTime { get; private set; } = -10;
        public string SaveWarning => store.Warning;
        readonly ProgressStore store = new ProgressStore();
        public bool IsSwinging { get; private set; }
        public Vector2Int SwingOrigin { get; private set; }
        public Vector2Int SwingCell { get; private set; }
        public float SwingProgress => IsSwinging ? Mathf.Clamp01((Time.unscaledTime - swingStarted) / settings.strikeInterval) : 0;
        bool continuous, dropMining, impactDone;
        float swingStarted;

        void Awake()
        {
            if (settings == null) settings = ScriptableObject.CreateInstance<GameSettings>();
            Session = new MiningSession(settings, store.Load(settings));
            Tutorial = new TutorialFlow(this);
            Session.GrowthChanged += SaveGrowth;
            Session.Impact += Tutorial.OnImpact; Session.Changed += Tutorial.StateChanged;
            View = gameObject.AddComponent<GameView>();
            View.Initialize(this);
            Controls = gameObject.AddComponent<PointerController>();
            Controls.Initialize(this);
        }

        void Update()
        {
            if (!IsSwinging || Modal != GameModal.None) return;
            float progress = SwingProgress;
            if (!impactDone && progress >= 0.52f)
            {
                impactDone = true;
                Session.Strike();
                if (!Session.CanMine) { continuous = false; dropMining = false; }
                if (dropMining && !Session.Target.HasValue) { dropMining = false; continuous = false; }
            }
            if (progress >= 1f)
            {
                IsSwinging = false;
                if (continuous && Session.CanMine) BeginSwing();
            }
        }

        public void Tap() { if (Modal == GameModal.None && Tutorial.AllowsTap && Session.CanMine && !IsSwinging) { Tutorial.BeginTap(); BeginSwing(); } }
        public void Hold() { if (Modal != GameModal.None || !Tutorial.AllowsHold || !Session.CanMine) return; Tutorial.BeginHold(); continuous = true; if (!IsSwinging) BeginSwing(); }
        void BeginSwing()
        {
            SwingOrigin = Session.Position;
            SwingCell = Session.Target ?? (Session.Position + Vector2Int.up);
            IsSwinging = true; impactDone = false; swingStarted = Time.unscaledTime;
        }
        public void StopMining() { continuous = false; dropMining = false; IsSwinging = false; Tutorial?.CancelGesture(); }
        public void ReleaseHold() { bool complete = Tutorial.CanFinishHold; StopMining(); if (complete) Tutorial.FinishHold(); }
        public void Drop(Vector2Int cell)
        {
            StopMining();
            if (Modal != GameModal.None || !Tutorial.AllowsDrag) return;
            if (!Session.Drop(cell))
            {
                if (Session.LastDropFailure == DropFailure.Blocked) View.ShowNotice("길이 막혀 있어요.");
                else if (Session.LastDropFailure == DropFailure.OutOfRange || Session.LastDropFailure == DropFailure.OutOfBounds) View.ShowNotice("이동 범위를 벗어났어요.");
                return;
            }
            Tutorial.DropAccepted(cell);
            if (Session.Target.HasValue) { dropMining = true; continuous = true; BeginSwing(); }
        }
        public void DropOutsideField() { StopMining(); View.ShowNotice("이동 범위를 벗어났어요."); }
        public void SelectPick(int index) { if (Modal != GameModal.None || !Tutorial.AllowsAction("pick" + index)) return; bool switching = Tutorial.Prompt == TutorialPrompt.Switch; StopMining(); if (Session.Select(index)) Tutorial.PickSelected(switching); }
        public void Play() { if (Modal != GameModal.None || !Tutorial.AllowsAction("play")) return; StopMining(); Session.Start(); Tutorial.PlayStarted(); }
        public void Restart() { if (!Tutorial.AllowsAction("restart")) return; CancelInput(); Session.Restart(); View.ResetRunVisuals(); SaveGrowth(); }
        public void Regroup()
        {
            if (Modal != GameModal.None || Session.State != SessionState.Playing || !Tutorial.AllowsAction("regroup")) return;
            CancelInput(); Session.Regroup(); View.ResetRunVisuals(); SaveGrowth();
        }
        public void HandleAction(string action)
        {
            if (string.IsNullOrEmpty(action)) return;
            if (View.TryOptionalAction(action)) return;
            if (!Tutorial.AllowsAction(action)) return;
            if (action == "close") { var previous = Modal; CancelInput(); Modal = Modal == GameModal.UpgradeConfirm ? GameModal.Upgrade : GameModal.None; Tutorial.ActionCompleted(action, previous); return; }
            if (action == "reset-next" && Modal == GameModal.ResetWarning && Session.State != SessionState.Playing)
            { CancelInput(); Modal = GameModal.ResetConfirm; return; }
            if (action == "reset-confirm" && Modal == GameModal.ResetConfirm && Session.State != SessionState.Playing)
            {
                CancelInput();
                var initial = ProgressData.Initial(settings);
                if (!store.Reset(initial)) return;
                Session.GrowthChanged -= SaveGrowth; Session.Impact -= View.OnImpact; Session.RecordBroken -= View.OnRecordBroken; Session.Impact -= Tutorial.OnImpact; Session.Changed -= Tutorial.StateChanged;
                Session = new MiningSession(settings, initial);
                Session.GrowthChanged += SaveGrowth; Session.Impact += View.OnImpact; Session.RecordBroken += View.OnRecordBroken;
                Session.Impact += Tutorial.OnImpact; Session.Changed += Tutorial.StateChanged;
                Modal = GameModal.None; View.ResetRunVisuals(); return;
            }
            if (Modal == GameModal.Shop)
            {
                if (Session.State == SessionState.Playing) return;
                if (action == "shop-buy") { ShopTab = ShopPage.Buy; return; }
                if (action == "shop-sell") { ShopTab = ShopPage.Sell; EnsureSaleSelection(); return; }
                if (ShopTab == ShopPage.Buy) { if (action == "buy") Session.Buy(); return; }
                var entry = OreCatalog.FromAction(action);
                if (entry != null && Session.IsDiscovered(entry.Kind)) { SaleKind = entry.Kind; SaleQuantity = Session.Stock(SaleKind) > 0 ? 1 : 0; }
                else if (entry != null) return;
                else if (action == "sale-less") SaleQuantity = Mathf.Max(Session.Stock(SaleKind) > 0 ? 1 : 0, SaleQuantity - 1);
                else if (action == "sale-more" && SaleQuantity < Session.Stock(SaleKind)) SaleQuantity++;
                else if (action == "sale-all") SaleQuantity = Session.Stock(SaleKind);
                else if (action == "sell") Session.Sell(SaleKind, SaleQuantity);
                SaleQuantity = Mathf.Min(SaleQuantity, Session.Stock(SaleKind));
                return;
            }
            if (Modal == GameModal.Upgrade)
            {
                if (Session.State == SessionState.Playing) return;
                if (action == "refinery-upgrade") { RefineryTab = RefineryPage.Upgrade; return; }
                if (action == "refinery-convert") { RefineryTab = RefineryPage.Conversion; EnsureConversionSelection(); return; }
                if (RefineryTab == RefineryPage.Conversion)
                {
                    if (action.StartsWith("convert-ore-"))
                    {
                        var entry = OreCatalog.FromId(action.Substring(12));
                        if (entry != null && settings.ConversionRate(entry.Kind) > 0) { ConversionKind = entry.Kind; ConversionQuantity = Session.MaximumConvertible(entry.Kind) > 0 ? 1 : 0; }
                    }
                    else if (action == "convert-less") ConversionQuantity = Mathf.Max(Session.MaximumConvertible(ConversionKind) > 0 ? 1 : 0, ConversionQuantity - 1);
                    else if (action == "convert-more" && ConversionQuantity < Session.MaximumConvertible(ConversionKind)) ConversionQuantity++;
                    else if (action == "convert-all") ConversionQuantity = Session.MaximumConvertible(ConversionKind);
                    else if (action == "convert")
                    {
                        int previous = Session.EnergyTenths;
                        if (Session.Convert(ConversionKind, ConversionQuantity)) { LastConversionTime = Time.unscaledTime; View.ShowNotice("에너지 +" + GameSettings.FormatEnergy(Session.EnergyTenths - previous), false); }
                    }
                    ConversionQuantity = Mathf.Min(ConversionQuantity, Session.MaximumConvertible(ConversionKind)); return;
                }
            }
            if (Modal == GameModal.Upgrade && action.StartsWith("upgrade-pick") && int.TryParse(action.Substring(12), out int upgradeIndex))
            { if (upgradeIndex >= 0 && upgradeIndex < Session.Picks.Length) { UpgradeIndex = upgradeIndex; Open(GameModal.UpgradeConfirm); } return; }
            if (Modal == GameModal.UpgradeConfirm && action == "upgrade-confirm")
            { if (Session.UpgradePick(UpgradeIndex)) { Modal = GameModal.Upgrade; Tutorial.ActionCompleted(action, GameModal.UpgradeConfirm); } return; }
            if (Modal != GameModal.None) return;
            if (action == "play") Play();
            else if (action == "restart") Restart();
            else if (action == "regroup") Regroup();
            else if (action.StartsWith("pick") && int.TryParse(action.Substring(4), out int index)) SelectPick(index);
            else if (Session.State != SessionState.Playing)
            {
                if (action == "shop") Open(GameModal.Shop);
                else if (action == "inventory") Open(GameModal.Inventory);
                else if (action == "upgrade") Open(GameModal.Upgrade);
                else if (action == "reset") Open(GameModal.ResetWarning);
            }
        }
        void Open(GameModal modal)
        {
            CancelInput(); Modal = modal;
            if (modal == GameModal.Shop) { ShopTab = ShopPage.Buy; EnsureSaleSelection(); }
            if (modal == GameModal.Upgrade) RefineryTab = RefineryPage.Upgrade;
        }
        void EnsureConversionSelection()
        {
            if (settings.ConversionRate(ConversionKind) <= 0)
            {
                ConversionKind = Mineral.Stone;
                foreach (var entry in OreCatalog.All) if (settings.ConversionRate(entry.Kind) > 0) { ConversionKind = entry.Kind; break; }
            }
            ConversionQuantity = Session.MaximumConvertible(ConversionKind) > 0 ? 1 : 0;
        }
        void EnsureSaleSelection()
        {
            if (!Session.IsDiscovered(SaleKind))
            {
                SaleKind = Mineral.Stone;
                foreach (var entry in OreCatalog.All) if (Session.IsDiscovered(entry.Kind)) { SaleKind = entry.Kind; break; }
            }
            SaleQuantity = Session.Stock(SaleKind) > 0 ? 1 : 0;
        }
        public Sprite PickSprite(string id) => id == "DIAMOND" ? diamondPick : id == "ALLOY" ? alloy : id == "IRON" ? iron : id == "STONE" ? stonePick : wood;
        public Sprite BlockSprite(Mineral kind, int row) => kind == Mineral.Oil ? null : kind == Mineral.Diamond ? diamondOre : kind == Mineral.Coal ? coalOre : kind == Mineral.Iron ? ironOre : kind == Mineral.Gold ? goldOre : row == 0 ? grass : stone;
        public void SaveGrowth() { if (Session != null) store.Save(Session.Growth()); }
        void OnApplicationPause(bool paused) { if (paused) { CancelInput(); SaveGrowth(); } }
        void OnApplicationFocus(bool focused) { if (!focused) { CancelInput(); SaveGrowth(); } }
        void OnApplicationQuit() => SaveGrowth();
        void CancelInput() { StopMining(); if (Controls != null) Controls.Cancel(); }
    }
}
