using System;
using System.Collections.Generic;
using UnityEngine;

namespace DigDeep
{
    public enum SessionState { Ready, Playing, GameOver }

    public sealed class Pickaxe
    {
        public readonly string Name;
        public readonly int Power, Maximum;
        public int Remaining;
        public Pickaxe(string name, int power, int maximum)
        { Name = name; Power = Math.Max(1, power); Maximum = Math.Max(1, maximum); Remaining = Maximum; }
    }

    // Rules are independent of animation, touch devices, and scene objects.
    public sealed class MiningSession
    {
        public readonly Pickaxe[] Picks;
        public SessionState State { get; private set; } = SessionState.Ready;
        public Vector2Int Position { get; private set; } = new Vector2Int(2, -1);
        public Vector2Int? Target { get; private set; }
        public int Selected { get; private set; }
        public int Money { get; private set; }
        public int RunIncome { get; private set; }
        public int Deepest { get; private set; }
        public int HealthMaximum { get; }
        public bool CanMine => State == SessionState.Playing && Picks[Selected].Remaining > 0;
        public event Action Changed;
        public event Action<Vector2Int, int, bool> Impact;
        public event Action<int> MoneyChanged;
        readonly int reward;
        readonly Dictionary<Vector2Int, int> health = new Dictionary<Vector2Int, int>();

        public MiningSession(GameSettings settings, int savedMoney)
        {
            HealthMaximum = Math.Max(1, settings.blockHealth);
            reward = Math.Max(0, settings.blockReward);
            Money = Math.Max(0, savedMoney);
            Picks = new[] { new Pickaxe("WOOD", settings.woodPower, settings.woodDurability),
                new Pickaxe("IRON", settings.ironPower, settings.ironDurability) };
        }

        public int Health(Vector2Int cell)
        {
            if (cell.y < 0) return 0;
            return health.TryGetValue(cell, out var value) ? value : HealthMaximum;
        }

        public void Start()
        {
            if (State != SessionState.Ready) return;
            State = SessionState.Playing;
            Changed?.Invoke();
        }

        public void Restart()
        {
            if (State != SessionState.GameOver) return;
            health.Clear();
            foreach (var pick in Picks) pick.Remaining = pick.Maximum;
            Position = new Vector2Int(2, -1);
            Target = null; Selected = 0; RunIncome = 0; Deepest = 0;
            State = SessionState.Ready;
            Changed?.Invoke();
        }

        public bool Select(int index)
        {
            if (index < 0 || index >= Picks.Length || Picks[index].Remaining == 0 || State == SessionState.GameOver) return false;
            Selected = index;
            Changed?.Invoke();
            return true;
        }

        // Paths may turn, but may never descend or pass through a solid intermediate cell.
        public bool FindPath(Vector2Int destination, out List<Vector2Int> path)
        {
            path = null;
            if (!CanMine || destination.x < 0 || destination.x >= GameSettings.Columns || destination.y < -1 ||
                destination.y > Position.y || Position.y - destination.y > GameSettings.ClimbLimit) return false;
            var queue = new Queue<Vector2Int>();
            var previous = new Dictionary<Vector2Int, Vector2Int>();
            queue.Enqueue(Position); previous[Position] = Position;
            var directions = new[] { Vector2Int.left, Vector2Int.right, Vector2Int.down };
            // Row indices grow downwards; Vector2Int.down is the upward neighbor on screen.
            while (queue.Count > 0)
            {
                var current = queue.Dequeue();
                if (current == destination)
                {
                    path = new List<Vector2Int>();
                    for (var p = current; p != Position; p = previous[p]) path.Add(p);
                    path.Reverse();
                    return true;
                }
                foreach (var direction in directions)
                {
                    var next = current + direction;
                    if (next.x < 0 || next.x >= GameSettings.Columns || next.y < -1 || next.y < Position.y - GameSettings.ClimbLimit ||
                        previous.ContainsKey(next) || (next != destination && Health(next) > 0)) continue;
                    previous[next] = current; queue.Enqueue(next);
                }
            }
            return false;
        }

        public bool Drop(Vector2Int destination)
        {
            if (!FindPath(destination, out var path)) return false;
            Target = null;
            for (int i = 0; i < path.Count; i++)
            {
                if (Health(path[i]) > 0) { Target = path[i]; break; }
                Position = path[i];
            }
            Changed?.Invoke();
            return true;
        }

        public bool Strike()
        {
            if (!CanMine) return false;
            var target = Target ?? (Position + Vector2Int.up);
            // Normal downward mining passes through already excavated space for free.
            if (!Target.HasValue)
                while (Health(target) == 0) { Position = target; target += Vector2Int.up; }
            var pick = Picks[Selected];
            int damage = Math.Min(pick.Power, Math.Min(Health(target), pick.Remaining));
            if (damage <= 0) return false;
            Target = target;
            health[target] = Health(target) - damage;
            pick.Remaining -= damage;
            bool broken = health[target] == 0;
            if (broken)
            {
                Position = target; Target = null;
                Deepest = Math.Max(Deepest, Position.y + 1);
                int awarded = Math.Min(reward, int.MaxValue - Money);
                Money += awarded; RunIncome += awarded;
                MoneyChanged?.Invoke(Money);
            }
            if (Array.TrueForAll(Picks, p => p.Remaining == 0)) State = SessionState.GameOver;
            Impact?.Invoke(target, damage, broken);
            Changed?.Invoke();
            return true;
        }
    }
}
