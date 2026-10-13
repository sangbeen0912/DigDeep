using System;
using System.Collections.Generic;
using UnityEngine;
namespace DigDeep
{
    public enum SessionState { Ready, Playing, GameOver }
    public enum DropFailure { None, Unavailable, OutOfBounds, OutOfRange, Blocked }
    public sealed class Pickaxe
    {
        public readonly string Name;
        public readonly float Power;
        public readonly int Maximum, Upgrade;
        public int Remaining;
        public Pickaxe(string name, float power, int maximum, int upgrade = 0)
        { Name = name; Power = Math.Max(1, power); Maximum = Math.Max(1, maximum); Upgrade = Math.Max(0, upgrade); Remaining = Maximum; }
    }
    public sealed class MiningSession
    {
        public Pickaxe[] Picks { get; private set; }
        public SessionState State { get; private set; } = SessionState.Ready;
        public Vector2Int Position { get; private set; } = new Vector2Int(2, -1);
        public Vector2Int? Target { get; private set; }
        public int Selected { get; private set; }
        public int Money { get; private set; }
        public int Coal { get; private set; }
        public int Iron { get; private set; }
        public int Gold { get; private set; }
        public int RunIncome { get; private set; }
        public int RunCoal { get; private set; }
        public int RunIron { get; private set; }
        public int RunGold { get; private set; }
        public int Deepest { get; private set; }
        public TutorialProgress Tutorial { get; }
        public DropFailure LastDropFailure { get; private set; }
        public int HealthMaximum => Math.Max(1, settings.blockHealth);
        public bool CanMine => State == SessionState.Playing && Picks[Selected].Remaining > 0;
        public bool CanTrade => State != SessionState.Playing;
        public event Action Changed, GrowthChanged;
        public event Action<Vector2Int, float, bool> Impact;
        public event Action<int> MoneyChanged;
        readonly GameSettings settings;
        OreSpawnManager ores;
        readonly Dictionary<Vector2Int, float> health = new Dictionary<Vector2Int, float>();
        readonly HashSet<Mineral> discovered = new HashSet<Mineral>();
        public MiningSession(GameSettings settings, int money, int? seed = null) : this(settings, ProgressData.Initial(settings, money), seed) { }
        public MiningSession(GameSettings settings, ProgressData progress, int? seed = null)
        {
            this.settings = settings; Money = Math.Max(0, progress.money);
            Tutorial = (progress.tutorial ?? new TutorialProgress()).Copy();
            Coal = Math.Max(0, progress.coal); Iron = Math.Max(0, progress.iron); Gold = Math.Max(0, progress.gold);
            if (progress.discovered != null) foreach (var id in progress.discovered)
            { var entry = OreCatalog.FromId(id); if (entry != null) discovered.Add(entry.Kind); }
            if (Coal > 0) discovered.Add(Mineral.Coal);
            if (Iron > 0) discovered.Add(Mineral.Iron);
            if (Gold > 0) discovered.Add(Mineral.Gold);
            Picks = progress.picks.ConvertAll(p => new Pickaxe(p.id, settings.Power(p.id), settings.Durability(p.id), GameSettings.Tier(p.id))).ToArray();
            ores = new OreSpawnManager(settings, seed ?? UnityEngine.Random.Range(1, int.MaxValue));
        }
        public ProgressData Growth()
        {
            var data = new ProgressData { money = Money, coal = Coal, iron = Iron, gold = Gold, tutorial = Tutorial.Copy() };
            foreach (var entry in OreCatalog.All) if (discovered.Contains(entry.Kind)) data.discovered.Add(entry.Id);
            foreach (var p in Picks) data.picks.Add(new PickProgress { id = p.Name, power = p.Power, maximum = p.Maximum, upgrade = p.Upgrade });
            return data;
        }
        public Mineral Kind(Vector2Int cell) => ores.Kind(cell);
        public int MaximumHealth(Vector2Int cell) => settings.OreHealth(Kind(cell));
        public float Health(Vector2Int cell) => cell.y < 0 ? 0 : health.TryGetValue(cell, out float value) ? value : MaximumHealth(cell);
        public int Stock(Mineral ore) => ore == Mineral.Coal ? Coal : ore == Mineral.Iron ? Iron : ore == Mineral.Gold ? Gold : 0;
        public bool IsDiscovered(Mineral ore) => discovered.Contains(ore);
        public void Start() { if (State != SessionState.Ready) return; State = SessionState.Playing; Changed?.Invoke(); }
        public void Restart() { if (State == SessionState.GameOver) PrepareNewRun(); }
        public void Regroup() { if (State == SessionState.Playing) PrepareNewRun(); }
        void PrepareNewRun()
        {
            health.Clear(); ores = new OreSpawnManager(settings, UnityEngine.Random.Range(1, int.MaxValue));
            foreach (var p in Picks) p.Remaining = p.Maximum;
            Position = new Vector2Int(2, -1); Target = null; Selected = 0;
            RunIncome = RunCoal = RunIron = RunGold = Deepest = 0; State = SessionState.Ready; Changed?.Invoke();
        }
        public bool Select(int index)
        {
            if (index < 0 || index >= Picks.Length || Picks[index].Remaining == 0 || State == SessionState.GameOver) return false;
            Selected = index; Changed?.Invoke(); return true;
        }
        public bool FindPath(Vector2Int destination, out List<Vector2Int> path)
            => FindPath(destination, out path, out _);
        bool FindPath(Vector2Int destination, out List<Vector2Int> path, out DropFailure failure)
        {
            path = null; failure = DropFailure.None;
            if (!CanMine) { failure = DropFailure.Unavailable; return false; }
            if (destination.x < 0 || destination.x >= GameSettings.Columns || destination.y < -1) { failure = DropFailure.OutOfBounds; return false; }
            if (Math.Abs(destination.x - Position.x) > GameSettings.ClimbLimit || Math.Abs(destination.y - Position.y) > GameSettings.ClimbLimit) { failure = DropFailure.OutOfRange; return false; }
            var queue = new Queue<Vector2Int>(); var previous = new Dictionary<Vector2Int, Vector2Int>();
            queue.Enqueue(Position); previous[Position] = Position;
            var directions = new[] { Vector2Int.left, Vector2Int.right, Vector2Int.down, Vector2Int.up };
            while (queue.Count > 0)
            {
                var current = queue.Dequeue();
                if (current == destination)
                {
                    path = new List<Vector2Int>();
                    for (var p = current; p != Position; p = previous[p]) path.Add(p);
                    path.Reverse(); return true;
                }
                foreach (var direction in directions)
                {
                    var next = current + direction;
                    if (next.x < 0 || next.x >= GameSettings.Columns || next.y < -1 ||
                        Math.Abs(next.x - Position.x) > GameSettings.ClimbLimit || Math.Abs(next.y - Position.y) > GameSettings.ClimbLimit ||
                        previous.ContainsKey(next) || (next != destination && Health(next) > 0)) continue;
                    previous[next] = current; queue.Enqueue(next);
                }
            }
            failure = DropFailure.Blocked; return false;
        }
        public bool Drop(Vector2Int cell)
        {
            if (!FindPath(cell, out var path, out var failure)) { LastDropFailure = failure; return false; }
            LastDropFailure = DropFailure.None;
            Target = null;
            foreach (var step in path) { if (Health(step) > 0) { Target = step; break; } Position = step; }
            Changed?.Invoke(); return true;
        }
        public bool Strike()
        {
            if (!CanMine) return false;
            var target = Target ?? (Position + Vector2Int.up);
            if (!Target.HasValue) while (Health(target) == 0) { Position = target; target += Vector2Int.up; }
            var pick = Picks[Selected]; float damage = Math.Min(pick.Power, Health(target));
            if (damage <= 0) return false;
            Target = target; health[target] = Health(target) - damage; pick.Remaining--;
            bool broken = health[target] == 0;
            if (broken)
            {
                Position = target; Target = null; Deepest = Math.Max(Deepest, target.y + 1);
                var kind = Kind(target);
                if (OreCatalog.Find(kind) != null) discovered.Add(kind);
                if (kind == Mineral.Coal) { Coal = Math.Min(int.MaxValue - 1, Coal) + 1; RunCoal++; }
                else if (kind == Mineral.Iron) { Iron = Math.Min(int.MaxValue - 1, Iron) + 1; RunIron++; }
                else if (kind == Mineral.Gold) { Gold = Math.Min(int.MaxValue - 1, Gold) + 1; RunGold++; }
                else { int earned = Math.Min(Math.Max(0, settings.blockReward), int.MaxValue - Money); Money += earned; RunIncome += earned; MoneyChanged?.Invoke(Money); }
                GrowthChanged?.Invoke();
            }
            if (Array.TrueForAll(Picks, p => p.Remaining == 0)) State = SessionState.GameOver;
            Impact?.Invoke(target, damage, broken); Changed?.Invoke(); return true;
        }
        public int PurchasePrice => Picks.Length >= GameSettings.MaximumPicks ? -1 : settings.purchasePrices[Picks.Length - 1];
        public bool CanBuy => CanTrade && PurchasePrice >= 0 && Money >= PurchasePrice;
        public bool Buy()
        {
            if (!CanBuy) return false;
            Money -= PurchasePrice; var list = new List<Pickaxe>(Picks) { new Pickaxe("WOOD", settings.woodPower, settings.woodDurability) };
            Picks = list.ToArray(); GrowthChanged?.Invoke(); Changed?.Invoke(); return true;
        }
        public int UpgradeCost(int index) => index < 0 || index >= Picks.Length || GameSettings.NextPick(Picks[index].Name) == null ? -1 : Picks[index].Name == "WOOD" ? settings.stoneUpgradeCost : Picks[index].Name == "STONE" ? settings.ironUpgradeCost : settings.alloyUpgradeCost;
        public int UpgradeOre(int index) => UpgradeCost(index) < 0 ? 0 : Picks[index].Name == "STONE" ? settings.ironUpgradeOre : Picks[index].Name == "IRON" ? settings.alloyUpgradeIron : 0;
        public int UpgradeCoal(int index) => UpgradeCost(index) < 0 ? 0 : Picks[index].Name == "WOOD" ? settings.stoneUpgradeCoal : Picks[index].Name == "STONE" ? settings.ironUpgradeCoal : settings.alloyUpgradeCoal;
        public int UpgradeGold(int index) => UpgradeCost(index) >= 0 && Picks[index].Name == "IRON" ? settings.alloyUpgradeGold : 0;
        public bool CanUpgrade(int index) => CanTrade && UpgradeCost(index) >= 0 && Money >= UpgradeCost(index) && Iron >= UpgradeOre(index) && Coal >= UpgradeCoal(index) && Gold >= UpgradeGold(index);
        public bool UpgradePick(int index)
        {
            if (!CanUpgrade(index)) return false;
            int cost = UpgradeCost(index), ore = UpgradeOre(index), coal = UpgradeCoal(index), gold = UpgradeGold(index); string id = GameSettings.NextPick(Picks[index].Name);
            Money -= cost; Iron -= ore; Coal -= coal; Gold -= gold;
            Picks[index] = new Pickaxe(id, settings.Power(id), settings.Durability(id), GameSettings.Tier(id));
            GrowthChanged?.Invoke(); Changed?.Invoke(); return true;
        }
        public bool Sell(Mineral kind, int amount)
        {
            if (!CanSell(kind, amount)) return false;
            long earned = (long)settings.SellPrice(kind) * amount;
            if (earned > int.MaxValue - Money) return false;
            if (kind == Mineral.Coal) Coal -= amount; else if (kind == Mineral.Iron) Iron -= amount; else Gold -= amount;
            Money += (int)earned; GrowthChanged?.Invoke(); Changed?.Invoke(); return true;
        }
        public bool CanSell(Mineral kind, int amount) => CanTrade && OreCatalog.Find(kind) != null && IsDiscovered(kind)
            && amount > 0 && amount <= Stock(kind) && (long)settings.SellPrice(kind) * amount <= int.MaxValue - Money;
    }
}
