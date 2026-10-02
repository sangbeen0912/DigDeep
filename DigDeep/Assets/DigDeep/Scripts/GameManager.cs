using UnityEngine;

namespace DigDeep
{
    public sealed class GameManager : MonoBehaviour
    {
        public GameSettings settings;
        public Sprite stone, grass, wood, iron;
        public Font font;
        public MiningSession Session { get; private set; }
        public GameView View { get; private set; }
        public PointerController Controls { get; private set; }
        public bool IsSwinging { get; private set; }
        public float SwingProgress => IsSwinging ? Mathf.Clamp01((Time.unscaledTime - swingStarted) / settings.strikeInterval) : 0;
        bool continuous, dropMining, impactDone;
        float swingStarted;

        void Awake()
        {
            if (settings == null) settings = ScriptableObject.CreateInstance<GameSettings>();
            Session = new MiningSession(settings, PlayerPrefs.GetInt(GameSettings.MoneyKey, 0));
            Session.MoneyChanged += SaveMoney;
            View = gameObject.AddComponent<GameView>();
            View.Initialize(this);
            Controls = gameObject.AddComponent<PointerController>();
            Controls.Initialize(this);
        }

        void Update()
        {
            if (!IsSwinging) return;
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

        public void Tap() { if (Session.CanMine && !IsSwinging) BeginSwing(); }
        public void Hold() { if (!Session.CanMine) return; continuous = true; if (!IsSwinging) BeginSwing(); }
        void BeginSwing() { IsSwinging = true; impactDone = false; swingStarted = Time.unscaledTime; }
        public void StopMining() { continuous = false; dropMining = false; IsSwinging = false; }
        public void Drop(Vector2Int cell)
        {
            StopMining();
            if (Session.Drop(cell) && Session.Target.HasValue)
            { dropMining = true; continuous = true; BeginSwing(); }
        }
        public void SelectPick(int index) { StopMining(); Session.Select(index); }
        public void Play() { StopMining(); Session.Start(); }
        public void Restart() { StopMining(); Session.Restart(); }
        void SaveMoney(int amount) { PlayerPrefs.SetInt(GameSettings.MoneyKey, amount); PlayerPrefs.Save(); }
        void OnApplicationPause(bool paused) { if (paused) CancelInput(); }
        void OnApplicationFocus(bool focused) { if (!focused) CancelInput(); }
        void CancelInput() { StopMining(); if (Controls != null) Controls.Cancel(); }
    }
}
