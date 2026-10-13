using System;
using UnityEngine;
namespace DigDeep
{
    [Serializable]
    public sealed class TutorialProgress
    {
        // 0: Dig, 1: tap, 2: drag, 3: hold, 4: controls completed.
        public int controlsStep, menuStep;
        public bool firstGameOver, switchDone;
        public bool Valid => controlsStep >= 0 && controlsStep <= 4 && menuStep >= 0 && menuStep <= 3;
        public TutorialProgress Copy() => new TutorialProgress { controlsStep = controlsStep, menuStep = menuStep, firstGameOver = firstGameOver, switchDone = switchDone };
        public static TutorialProgress Completed() => new TutorialProgress { controlsStep = 4, menuStep = 3, firstGameOver = true, switchDone = true };
    }
    public enum TutorialPrompt { None, Dig, Tap, Drag, Hold, Restart, Inventory, Shop, Upgrade, Switch }
    public sealed class TutorialFlow
    {
        readonly GameManager game;
        bool tapPending, holding;
        int holdHits;
        Vector2Int? pendingDrop;
        public TutorialFlow(GameManager owner) { game = owner; }
        TutorialProgress Data => game.Session.Tutorial;
        public TutorialPrompt Prompt
        {
            get
            {
                var s = game.Session;
                if (Data.controlsStep < 4)
                {
                    if (s.State == SessionState.Ready) return TutorialPrompt.Dig;
                    if (s.State == SessionState.GameOver) return TutorialPrompt.Restart;
                    return Data.controlsStep <= 1 ? TutorialPrompt.Tap : Data.controlsStep == 2 ? TutorialPrompt.Drag : TutorialPrompt.Hold;
                }
                if (Data.firstGameOver && Data.menuStep < 3)
                {
                    if (s.State == SessionState.GameOver) return TutorialPrompt.Restart;
                    if (s.State == SessionState.Ready) return Data.menuStep == 0 ? TutorialPrompt.Inventory : Data.menuStep == 1 ? TutorialPrompt.Shop : TutorialPrompt.Upgrade;
                }
                if (!Data.switchDone && s.State == SessionState.Playing && s.Picks.Length > 1 && s.Picks[s.Selected].Remaining == 0)
                    foreach (var p in s.Picks) if (p.Remaining > 0) return TutorialPrompt.Switch;
                return TutorialPrompt.None;
            }
        }
        public Vector2Int DragDestination
        {
            get { var p = game.Session.Position; return new Vector2Int(p.x < GameSettings.Columns - 1 ? p.x + 1 : p.x - 1, p.y); }
        }
        public bool AllowsAction(string action)
        {
            if (action == "blocked" || string.IsNullOrEmpty(action)) return false;
            var modal = game.Modal;
            if (modal == GameModal.ResetWarning || modal == GameModal.ResetConfirm)
                return action == "close" || action == "reset-next" || action == "reset-confirm";
            if (action == "reset") return game.Session.State != SessionState.Playing;
            if (action == "regroup") return game.Session.State == SessionState.Playing;
            switch (Prompt)
            {
                case TutorialPrompt.Dig: return action == "play";
                case TutorialPrompt.Restart: return action == "restart";
                case TutorialPrompt.Tap:
                case TutorialPrompt.Drag:
                case TutorialPrompt.Hold: return IsUsablePickAction(action) && !game.Session.CanMine;
                case TutorialPrompt.Inventory: return modal == GameModal.None ? action == "inventory" : modal == GameModal.Inventory && action == "close";
                case TutorialPrompt.Shop:
                    return modal == GameModal.None ? action == "shop" : modal == GameModal.Shop && (action == "close" || action == "shop-buy" || action == "shop-sell" || action == "buy" || action == "sell" || action.StartsWith("sale-"));
                case TutorialPrompt.Upgrade:
                    if (modal == GameModal.None) return action == "upgrade";
                    if (modal == GameModal.Upgrade) return action.StartsWith("upgrade-pick");
                    return modal == GameModal.UpgradeConfirm && (action == "close" || action == "upgrade-confirm");
                case TutorialPrompt.Switch: return IsUsablePickAction(action);
                default: return true;
            }
        }
        bool IsUsablePickAction(string action) => action.StartsWith("pick") && int.TryParse(action.Substring(4), out int i) && i >= 0 && i < game.Session.Picks.Length && game.Session.Picks[i].Remaining > 0;
        public bool AllowsFieldStart(bool onPick) => Prompt == TutorialPrompt.None || (Prompt == TutorialPrompt.Drag ? onPick : (Prompt == TutorialPrompt.Tap || Prompt == TutorialPrompt.Hold) && !onPick);
        public bool AllowsTap => Prompt == TutorialPrompt.None || Prompt == TutorialPrompt.Tap;
        public bool AllowsHold => Prompt == TutorialPrompt.None || Prompt == TutorialPrompt.Hold;
        public bool AllowsDrag => Prompt == TutorialPrompt.None || Prompt == TutorialPrompt.Drag;
        public bool AllowsDrop(Vector2Int destination) => AllowsDrag && (Prompt != TutorialPrompt.Drag || destination == DragDestination);
        public void PlayStarted() { if (Data.controlsStep == 0) { Data.controlsStep = 1; Save(); } }
        public void BeginTap() { tapPending = Data.controlsStep == 1; }
        public void BeginHold() { holding = Data.controlsStep == 3; holdHits = 0; }
        public bool CanFinishHold => holding && holdHits >= 2;
        public void FinishHold()
        {
            if (Data.controlsStep != 3) return;
            Data.controlsStep = 4; CancelGesture(); Save(); game.View.ShowNotice("기본 조작을 익혔어요!", false);
        }
        public void DropAccepted(Vector2Int destination)
        {
            if (Data.controlsStep != 2) return;
            pendingDrop = destination; CheckDropCompleted();
        }
        void CheckDropCompleted()
        {
            if (Data.controlsStep != 2 || !pendingDrop.HasValue || game.Session.Position != pendingDrop.Value || game.Session.Target.HasValue) return;
            pendingDrop = null; Data.controlsStep = 3; Save();
        }
        public void OnImpact(Vector2Int cell, float damage, bool broken)
        {
            if (tapPending && Data.controlsStep == 1) { tapPending = false; Data.controlsStep = 2; Save(); }
            else if (holding && Data.controlsStep == 3)
            {
                holdHits++;
                if (game.Session.State == SessionState.GameOver && CanFinishHold) FinishHold();
            }
            CheckDropCompleted();
        }
        public void StateChanged()
        {
            if (game.Session.State == SessionState.GameOver && !Data.firstGameOver) { Data.firstGameOver = true; Save(); }
        }
        public void ActionCompleted(string action, GameModal previous)
        {
            if (!Data.firstGameOver || Data.controlsStep < 4 || Data.menuStep >= 3) return;
            if (Data.menuStep == 0 && previous == GameModal.Inventory && action == "close") { Data.menuStep = 1; Save(); }
            else if (Data.menuStep == 1 && previous == GameModal.Shop && action == "close") { Data.menuStep = 2; Save(); }
            else if (Data.menuStep == 2 && previous == GameModal.UpgradeConfirm && (action == "close" || (action == "upgrade-confirm" && game.Modal == GameModal.Upgrade)))
            { Data.menuStep = 3; Save(); game.View.ShowNotice("화면 안내를 모두 확인했어요!", false); }
        }
        public void PickSelected(bool wasSwitchPrompt)
        {
            if (wasSwitchPrompt && game.Session.CanMine && !Data.switchDone) { Data.switchDone = true; Save(); game.View.ShowNotice("다른 곡괭이로 계속 채굴할 수 있어요.", false); }
        }
        public void CancelGesture() { tapPending = holding = false; holdHits = 0; pendingDrop = null; }
        void Save() => game.SaveGrowth();
    }
}
