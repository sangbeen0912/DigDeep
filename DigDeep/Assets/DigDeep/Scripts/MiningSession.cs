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
        public int Diamond { get; private set; }
        public int Oil { get; private set; }
        public int EnergyTenths { get; private set; }
        public int RunIncome { get; private set; }
        public int RunCoal { get; private set; }
        public int RunIron { get; private set; }
        public int RunGold { get; private set; }
        public int RunDiamond { get; private set; }
        public int RunOil { get; private set; }
        public int Deepest { get; private set; }
        public int BestDepth { get; private set; }
        public TutorialProgress Tutorial { get; }
        public DropFailure LastDropFailure { get; private set; }
        public int HealthMaximum => Math.Max(1, settings.blockHealth);
        public bool CanMine => State == SessionState.Playing && Picks[Selected].Remaining > 0;
        public bool CanTrade => State != SessionState.Playing;
        public event Action Changed, GrowthChanged;
        public event Action<Vector2Int, float, bool> Impact;
        public event Action<int> MoneyChanged, RecordBroken;
        public event Action<Vector2Int> Exploded;
        public int LastRewardAmount { get; private set; }
        public bool IsDynamite => Picks[Selected].Name == "DYNAMITE";
        bool recordCelebrated;
        readonly GameSettings settings;
        OreSpawnManager ores;
        readonly Dictionary<Vector2Int, float> health = new Dictionary<Vector2Int, float>();
        readonly Queue<Vector2Int> pathQueue = new Queue<Vector2Int>(32);
        readonly Dictionary<Vector2Int, Vector2Int> pathPrevious = new Dictionary<Vector2Int, Vector2Int>(32);
        readonly List<Vector2Int> pathResult = new List<Vector2Int>(24);
        static readonly Vector2Int[] PathDirections = { Vector2Int.left, Vector2Int.right, Vector2Int.down, Vector2Int.up };
        readonly HashSet<Mineral> discovered = new HashSet<Mineral>();
        public MiningSession(GameSettings settings, int money, int? seed = null) : this(settings, ProgressData.Initial(settings, money), seed) { }
        public MiningSession(GameSettings settings, ProgressData progress, int? seed = null)
        {
            this.settings = settings; Money = Math.Max(0, progress.money);
            BestDepth = Math.Max(0, progress.bestDepth);
            Tutorial = (progress.tutorial ?? new TutorialProgress()).Copy();
            Coal = Math.Max(0, progress.coal); Iron = Math.Max(0, progress.iron); Gold = Math.Max(0, progress.gold);
            Diamond = Math.Max(0, progress.diamond); Oil = Math.Max(0, progress.oil); EnergyTenths = Math.Max(0, progress.energyTenths);
            if (progress.discovered != null) foreach (var id in progress.discovered)
            { var entry = OreCatalog.FromId(id); if (entry != null) discovered.Add(entry.Kind); }
            if (Coal > 0) discovered.Add(Mineral.Coal);
            if (Iron > 0) discovered.Add(Mineral.Iron);
            if (Gold > 0) discovered.Add(Mineral.Gold);
            if (Diamond > 0) discovered.Add(Mineral.Diamond);
            if (Oil > 0) discovered.Add(Mineral.Oil);
            Picks = progress.picks.ConvertAll(p => new Pickaxe(p.id, settings.Power(p.id), settings.Durability(p.id), GameSettings.Tier(p.id))).ToArray();
            ores = new OreSpawnManager(settings, seed ?? UnityEngine.Random.Range(1, int.MaxValue));
        }
        public ProgressData Growth()
        {
            var data = new ProgressData { money = Money, coal = Coal, iron = Iron, gold = Gold, diamond = Diamond, oil = Oil, energyTenths = EnergyTenths, bestDepth = BestDepth, tutorial = Tutorial.Copy() };
            foreach (var entry in OreCatalog.All) if (discovered.Contains(entry.Kind)) data.discovered.Add(entry.Id);
            foreach (var p in Picks) data.picks.Add(new PickProgress { id = p.Name, power = p.Power, maximum = p.Maximum, upgrade = p.Upgrade });
            return data;
        }
        public Mineral Kind(Vector2Int cell) => ores.Kind(cell);
        public int MaximumHealth(Vector2Int cell) => settings.OreHealth(Kind(cell));
        public float Health(Vector2Int cell) => cell.y < 0 ? 0 : health.TryGetValue(cell, out float value) ? value : MaximumHealth(cell);
        public int Stock(Mineral ore) => ore == Mineral.Coal ? Coal : ore == Mineral.Iron ? Iron : ore == Mineral.Gold ? Gold : ore == Mineral.Diamond ? Diamond : ore == Mineral.Oil ? Oil : 0;
        void ChangeStock(Mineral ore, int delta)
        {
            if (ore == Mineral.Coal) Coal += delta;
            else if (ore == Mineral.Iron) Iron += delta;
            else if (ore == Mineral.Gold) Gold += delta;
            else if (ore == Mineral.Diamond) Diamond += delta;
            else if (ore == Mineral.Oil) Oil += delta;
        }
        public bool IsDiscovered(Mineral ore) => discovered.Contains(ore);
        public void Start() { if (State != SessionState.Ready) return; State = SessionState.Playing; Changed?.Invoke(); }
        public void Restart() { if (State == SessionState.GameOver) PrepareNewRun(); }
        public void Regroup() { if (State == SessionState.Playing) PrepareNewRun(); }
        void PrepareNewRun()
        {
            health.Clear(); ores = new OreSpawnManager(settings, UnityEngine.Random.Range(1, int.MaxValue));
            foreach (var p in Picks) p.Remaining = p.Maximum;
            Position = new Vector2Int(2, -1); Target = null; Selected = 0;
            recordCelebrated = false;
            RunIncome = RunCoal = RunIron = RunGold = RunDiamond = RunOil = Deepest = 0; State = SessionState.Ready; Changed?.Invoke();
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
            if (Math.Abs(destination.x - Position.x) > GameSettings.ClimbLimit || Math.Abs(destination.y - Position.y) > GameSettings.VerticalClimbLimit) { failure = DropFailure.OutOfRange; return false; }
            var queue = pathQueue; var previous = pathPrevious; queue.Clear(); previous.Clear();
            queue.Enqueue(Position); previous[Position] = Position;
            var directions = PathDirections;
            while (queue.Count > 0)
            {
                var current = queue.Dequeue();
                if (current == destination)
                {
                    path = pathResult; path.Clear();
                    for (var p = current; p != Position; p = previous[p]) path.Add(p);
                    path.Reverse(); return true;
                }
                foreach (var direction in directions)
                {
                    var next = current + direction;
                    if (next.x < 0 || next.x >= GameSettings.Columns || next.y < -1 ||
                        Math.Abs(next.x - Position.x) > GameSettings.ClimbLimit || Math.Abs(next.y - Position.y) > GameSettings.VerticalClimbLimit ||
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
            if (RecordPosition()) GrowthChanged?.Invoke(); Changed?.Invoke(); return true;
        }
        bool RecordPosition()
        {
            Deepest = Math.Max(Deepest, Position.y + 1);
            if (Deepest <= BestDepth) return false;
            BestDepth = Deepest;
            if (!recordCelebrated) { recordCelebrated = true; RecordBroken?.Invoke(BestDepth); }
            return true;
        }
        static int AddCapped(int current, int amount) => (int)Math.Min(int.MaxValue, (long)current + amount);
        void RewardBlock(Vector2Int cell, int bonus)
        {
            var kind = Kind(cell); LastRewardAmount = 0;
            if (OreCatalog.Find(kind) != null)
            {
                discovered.Add(kind);
                int amount = (int)Math.Min(int.MaxValue, 1L + bonus);
                int actual = Math.Min(amount, int.MaxValue - Stock(kind)); LastRewardAmount = actual;
                ChangeStock(kind, actual);
                if (kind == Mineral.Coal) RunCoal = AddCapped(RunCoal, actual);
                else if (kind == Mineral.Iron) RunIron = AddCapped(RunIron, actual);
                else if (kind == Mineral.Gold) RunGold = AddCapped(RunGold, actual);
                else if (kind == Mineral.Diamond) RunDiamond = AddCapped(RunDiamond, actual);
                else if (kind == Mineral.Oil) RunOil = AddCapped(RunOil, actual);
            }
            else
            {
                int earned = Math.Min(Math.Max(0, settings.blockReward), int.MaxValue - Money);
                Money += earned; RunIncome = AddCapped(RunIncome, earned); MoneyChanged?.Invoke(Money);
            }
        }
        void CheckExhaustion()
        {
            for (int i = 0; i < Picks.Length; i++) if (Picks[i].Remaining > 0) return;
            State = SessionState.GameOver;
        }
        public bool Strike()
        {
            if (!CanMine || IsDynamite) return false;
            var target = Target ?? (Position + Vector2Int.up);
            if (!Target.HasValue) while (Health(target) == 0) { Position = target; target += Vector2Int.up; }
            var pick = Picks[Selected]; float damage = Math.Min(pick.Power, Health(target));
            if (damage <= 0) return false;
            Target = target; health[target] = Health(target) - damage; pick.Remaining--;
            bool broken = health[target] == 0; LastRewardAmount = 0;
            if (broken) { Position = target; Target = null; RecordPosition(); RewardBlock(target, settings.ExtraDrops(pick.Name)); GrowthChanged?.Invoke(); }
            CheckExhaustion(); Impact?.Invoke(target, damage, broken); Changed?.Invoke(); return true;
        }
        public bool CanBlast(Vector2Int cell) => CanMine && IsDynamite && cell.x >= 0 && cell.x < GameSettings.Columns && cell.y >= 0 && Health(cell) > 0;
        public bool Blast(Vector2Int cell)
        {
            if (!CanBlast(cell)) return false;
            var pick = Picks[Selected]; pick.Remaining--; Target = null;
            for (int y = Math.Max(0, cell.y - 1); y <= cell.y + 1; y++)
                for (int x = Math.Max(0, cell.x - 1); x <= Math.Min(GameSettings.Columns - 1, cell.x + 1); x++)
                {
                    var target = new Vector2Int(x, y); float hp = Health(target);
                    if (hp <= 0) continue;
                    float damage = Math.Min(pick.Power, hp); health[target] = hp - damage;
                    bool broken = health[target] <= 0; LastRewardAmount = 0;
                    if (broken) RewardBlock(target, 0);
                    Impact?.Invoke(target, damage, broken);
                }
            // Remote destruction never moves the equipment or advances depth records.
            CheckExhaustion(); GrowthChanged?.Invoke(); Exploded?.Invoke(cell); Changed?.Invoke(); return true;
        }
        public string ResultSummary() => "이번 판 도달 깊이\n" + Deepest + "행";
        public bool GrantResources(int money, int coal, int iron, int gold, int diamond = 0, int oil = 0, int energyTenths = 0)
        {
            if (State != SessionState.Ready || money < 0 || coal < 0 || iron < 0 || gold < 0 || diamond < 0 || oil < 0 || energyTenths < 0) return false;
            Money = (int)Math.Min(int.MaxValue, (long)Money + money);
            Coal = (int)Math.Min(int.MaxValue, (long)Coal + coal);
            Iron = (int)Math.Min(int.MaxValue, (long)Iron + iron);
            Gold = (int)Math.Min(int.MaxValue, (long)Gold + gold);
            Diamond = AddCapped(Diamond, diamond); Oil = AddCapped(Oil, oil); EnergyTenths = AddCapped(EnergyTenths, energyTenths);
            if (diamond > 0) discovered.Add(Mineral.Diamond);
            if (oil > 0) discovered.Add(Mineral.Oil);
            if (coal > 0) discovered.Add(Mineral.Coal);
            if (iron > 0) discovered.Add(Mineral.Iron);
            if (gold > 0) discovered.Add(Mineral.Gold);
            GrowthChanged?.Invoke(); Changed?.Invoke(); return true;
        }
        public int PurchasePrice => Picks.Length >= GameSettings.MaximumPicks ? -1 : settings.purchasePrices[Picks.Length - 1];
        public bool CanBuy => CanTrade && PurchasePrice >= 0 && Money >= PurchasePrice;
        public bool Buy()
        {
            if (!CanBuy) return false;
            Money -= PurchasePrice; var list = new List<Pickaxe>(Picks) { new Pickaxe("WOOD", settings.woodPower, settings.woodDurability) };
            Picks = list.ToArray(); GrowthChanged?.Invoke(); Changed?.Invoke(); return true;
        }
        public bool HasEquipment(string id) { for (int i = 0; i < Picks.Length; i++) if (Picks[i].Name == id) return true; return false; }
        bool ValidChoice(int index, string choice) => index >= 0 && index < Picks.Length && Picks[index].Name == "SUPERALLOY" && GameSettings.IsEquipment(choice);
        public int UpgradeCost(int index, string choice = null) => choice != null ? (ValidChoice(index, choice) ? settings.equipmentCost : -1) : index < 0 || index >= Picks.Length || GameSettings.NextPick(Picks[index].Name) == null ? -1 : Picks[index].Name == "WOOD" ? settings.stoneUpgradeCost : Picks[index].Name == "STONE" ? settings.ironUpgradeCost : Picks[index].Name == "IRON" ? settings.alloyUpgradeCost : Picks[index].Name == "ALLOY" ? settings.diamondUpgradeCost : settings.superalloyUpgradeCost;
        public int UpgradeOre(int index, string choice = null) => choice != null ? 0 : UpgradeCost(index) < 0 ? 0 : Picks[index].Name == "STONE" ? settings.ironUpgradeOre : Picks[index].Name == "IRON" ? settings.alloyUpgradeIron : Picks[index].Name == "DIAMOND" ? settings.superalloyUpgradeIron : 0;
        public int UpgradeCoal(int index, string choice = null) => choice != null ? 0 : UpgradeCost(index) < 0 ? 0 : Picks[index].Name == "WOOD" ? settings.stoneUpgradeCoal : Picks[index].Name == "STONE" ? settings.ironUpgradeCoal : Picks[index].Name == "IRON" ? settings.alloyUpgradeCoal : 0;
        public int UpgradeGold(int index, string choice = null) => choice != null ? 0 : UpgradeCost(index) >= 0 && Picks[index].Name == "IRON" ? settings.alloyUpgradeGold : 0;
        public int UpgradeDiamond(int index, string choice = null) => choice != null ? 0 : UpgradeCost(index) < 0 ? 0 : Picks[index].Name == "ALLOY" ? settings.diamondUpgradeOre : Picks[index].Name == "DIAMOND" ? settings.superalloyUpgradeDiamond : 0;
        public int UpgradeEnergy(int index, string choice = null) => choice != null ? (ValidChoice(index, choice) ? settings.equipmentEnergyTenths : 0) : UpgradeCost(index) < 0 ? 0 : Picks[index].Name == "ALLOY" ? settings.diamondUpgradeEnergyTenths : Picks[index].Name == "DIAMOND" ? settings.superalloyUpgradeEnergyTenths : 0;
        public bool CanUpgrade(int index, string choice = null) => CanTrade && (choice == null || !HasEquipment(choice)) && UpgradeCost(index, choice) >= 0 && Money >= UpgradeCost(index, choice) && Iron >= UpgradeOre(index, choice) && Coal >= UpgradeCoal(index, choice) && Gold >= UpgradeGold(index, choice) && Diamond >= UpgradeDiamond(index, choice) && EnergyTenths >= UpgradeEnergy(index, choice);
        public bool UpgradePick(int index, string choice = null)
        {
            if (!CanUpgrade(index, choice)) return false;
            int cost = UpgradeCost(index, choice), ore = UpgradeOre(index, choice), coal = UpgradeCoal(index, choice), gold = UpgradeGold(index, choice); string id = choice ?? GameSettings.NextPick(Picks[index].Name);
            Money -= cost; Iron -= ore; Coal -= coal; Gold -= gold; Diamond -= UpgradeDiamond(index, choice); EnergyTenths -= UpgradeEnergy(index, choice);
            Picks[index] = new Pickaxe(id, settings.Power(id), settings.Durability(id), GameSettings.Tier(id));
            GrowthChanged?.Invoke(); Changed?.Invoke(); return true;
        }
        public bool Sell(Mineral kind, int amount)
        {
            if (!CanSell(kind, amount)) return false;
            long earned = (long)settings.SellPrice(kind) * amount;
            if (earned > int.MaxValue - Money) return false;
            ChangeStock(kind, -amount);
            Money += (int)earned; GrowthChanged?.Invoke(); Changed?.Invoke(); return true;
        }
        public bool CanSell(Mineral kind, int amount) => CanTrade && OreCatalog.Find(kind) != null && IsDiscovered(kind)
            && amount > 0 && amount <= Stock(kind) && (long)settings.SellPrice(kind) * amount <= int.MaxValue - Money;
        public int MaximumConvertible(Mineral kind)
        {
            int rate = settings.ConversionRate(kind);
            return rate <= 0 || !IsDiscovered(kind) ? 0 : Math.Min(Stock(kind), (int.MaxValue - EnergyTenths) / rate);
        }
        public bool CanConvert(Mineral kind, int amount) => CanTrade && amount > 0 && amount <= MaximumConvertible(kind);
        public bool Convert(Mineral kind, int amount)
        {
            if (!CanConvert(kind, amount)) return false;
            int gain = settings.ConversionRate(kind) * amount;
            ChangeStock(kind, -amount); EnergyTenths += gain;
            GrowthChanged?.Invoke(); Changed?.Invoke(); return true;
        }
    }
}
