using UnityEngine;

namespace DigDeep
{
    public enum GameModal { None, Shop, Upgrade, ResetWarning, ResetConfirm }
    public sealed class GameManager : MonoBehaviour
    {
        public GameSettings settings;
        public Sprite stone, grass, wood, iron;
        public Font font;
        public MiningSession Session { get; private set; }
        public GameView View { get; private set; }
        public PointerController Controls { get; private set; }
        public GameModal Modal { get; private set; }
        public string SaveWarning => store.Warning;
        readonly ProgressStore store = new ProgressStore();
        public bool IsSwinging { get; private set; }
        public float SwingProgress => IsSwinging ? Mathf.Clamp01((Time.unscaledTime - swingStarted) / settings.strikeInterval) : 0;
        bool continuous, dropMining, impactDone;
        float swingStarted;

        void Awake()
        {
            if (settings == null) settings = ScriptableObject.CreateInstance<GameSettings>();
            Session = new MiningSession(settings, store.Load(settings));
            Session.MoneyChanged += SaveMoney;
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

        public void Tap() { if (Modal == GameModal.None && Session.CanMine && !IsSwinging) BeginSwing(); }
        public void Hold() { if (Modal != GameModal.None || !Session.CanMine) return; continuous = true; if (!IsSwinging) BeginSwing(); }
        void BeginSwing() { IsSwinging = true; impactDone = false; swingStarted = Time.unscaledTime; }
        public void StopMining() { continuous = false; dropMining = false; IsSwinging = false; }
        public void Drop(Vector2Int cell)
        {
            StopMining();
            if (Modal == GameModal.None && Session.Drop(cell) && Session.Target.HasValue)
            { dropMining = true; continuous = true; BeginSwing(); }
        }
        public void SelectPick(int index) { if (Modal != GameModal.None) return; StopMining(); Session.Select(index); }
        public void Play() { if (Modal != GameModal.None) return; StopMining(); Session.Start(); }
        public void Restart() { CancelInput(); Session.Restart(); SaveGrowth(); }
        public void Regroup()
        {
            if (Modal != GameModal.None || Session.State != SessionState.Playing) return;
            CancelInput(); Session.Regroup(); SaveGrowth();
        }
        public void HandleAction(string action)
        {
            if (action == "close") { CancelInput(); Modal = GameModal.None; return; }
            if (action == "reset-next" && Modal == GameModal.ResetWarning && Session.State != SessionState.Playing)
            { CancelInput(); Modal = GameModal.ResetConfirm; return; }
            if (action == "reset-confirm" && Modal == GameModal.ResetConfirm && Session.State != SessionState.Playing)
            {
                CancelInput();
                var initial = ProgressData.Initial(settings);
                if (!store.Reset(initial)) return;
                Session.MoneyChanged -= SaveMoney; Session.Impact -= View.OnImpact;
                Session = new MiningSession(settings, initial);
                Session.MoneyChanged += SaveMoney; Session.Impact += View.OnImpact;
                Modal = GameModal.None; return;
            }
            if (Modal != GameModal.None) return;
            if (action == "play") Play();
            else if (action == "restart") Restart();
            else if (action == "regroup") Regroup();
            else if (action.StartsWith("pick") && int.TryParse(action.Substring(4), out int index)) SelectPick(index);
            else if (Session.State != SessionState.Playing)
            {
                if (action == "shop") Open(GameModal.Shop);
                else if (action == "upgrade") Open(GameModal.Upgrade);
                else if (action == "reset") Open(GameModal.ResetWarning);
            }
        }
        void Open(GameModal modal) { CancelInput(); Modal = modal; }
        void SaveMoney(int amount) => SaveGrowth();
        public void SaveGrowth() { if (Session != null) store.Save(Session.Growth()); }
        void OnApplicationPause(bool paused) { if (paused) { CancelInput(); SaveGrowth(); } }
        void OnApplicationFocus(bool focused) { if (!focused) { CancelInput(); SaveGrowth(); } }
        void OnApplicationQuit() => SaveGrowth();
        void CancelInput() { StopMining(); if (Controls != null) Controls.Cancel(); }
    }
}
