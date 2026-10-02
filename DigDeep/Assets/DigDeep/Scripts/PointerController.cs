using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

namespace DigDeep
{
    // All screen targets share this one gesture owner: no UI click-through or second-finger actions.
    [DefaultExecutionOrder(-100)]
    public sealed class PointerController : MonoBehaviour
    {
        GameManager game;
        TouchControl touch;
        bool active, pickCandidate, dragging, held, canceled;
        string action;
        Vector2 origin, last;
        float started;
        public void Initialize(GameManager value) { game = value; }

        void Update()
        {
            var screen = Touchscreen.current;
            if (touch != null)
            {
                if (touch.phase.ReadValue() == UnityEngine.InputSystem.TouchPhase.Canceled) { Cancel(); return; }
                var point = touch.position.ReadValue();
                if (!touch.press.isPressed) { Up(point); touch = null; }
                else Move(point);
                return;
            }
            if (screen != null)
            {
                foreach (var candidate in screen.touches)
                    if (candidate.press.wasPressedThisFrame && !active)
                    { touch = candidate; Down(candidate.position.ReadValue()); return; }
                foreach (var candidate in screen.touches) if (candidate.press.isPressed) return;
            }
            var mouse = Mouse.current;
            if (mouse == null) return;
            var position = mouse.position.ReadValue();
            if (mouse.leftButton.wasPressedThisFrame) Down(position);
            else if (active && mouse.leftButton.isPressed) Move(position);
            if (active && mouse.leftButton.wasReleasedThisFrame) Up(position);
        }

        public void Down(Vector2 screen)
        {
            if (active) return;
            active = true; started = Time.unscaledTime; origin = last = game.View.Local(screen);
            held = dragging = canceled = false;
            action = game.View.ActionAt(origin);
            pickCandidate = action == null && game.Session.CanMine && game.View.PickContains(origin);
            if (pickCandidate) game.StopMining();
            if (action == null && (!game.View.InField(origin) || !game.Session.CanMine)) canceled = true;
        }

        public void Move(Vector2 screen)
        {
            if (!active || canceled) return;
            last = game.View.Local(screen);
            float distance = Vector2.Distance(last, origin);
            if (action != null) { if (distance > game.settings.dragThreshold) canceled = true; return; }
            if (pickCandidate)
            {
                if (distance >= game.settings.dragThreshold) dragging = true;
                if (dragging) game.View.SetDrag(last);
                return;
            }
            if (distance >= game.settings.dragThreshold || !game.View.InField(last))
            { canceled = true; game.StopMining(); return; }
            if (!held && Time.unscaledTime - started >= game.settings.holdDelay)
            { held = true; game.Hold(); }
        }

        public void Up(Vector2 screen)
        {
            if (!active) return;
            last = game.View.Local(screen);
            if (!canceled)
            {
                if (action != null && game.View.ActionAt(last) == action)
                {
                    game.HandleAction(action);
                }
                else if (dragging)
                { if (game.View.InField(last)) game.Drop(game.View.CellAt(last)); }
                else if (action == null && !pickCandidate && !held) game.Tap();
            }
            if (held) game.StopMining();
            game.View.EndDrag(); active = false; action = null;
        }

        public void Cancel()
        { active = false; touch = null; action = null; dragging = held = false; game.StopMining(); game.View.EndDrag(); }
    }
}
