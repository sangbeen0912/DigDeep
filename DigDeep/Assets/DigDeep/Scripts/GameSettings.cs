using System;
using UnityEngine;
namespace DigDeep
{
    [Serializable]
    public sealed class SpawnLine
    {
        public string name;
        [Min(1)] public int firstRow, lastRow;
        [Range(0, 100)] public float coalPercent, ironPercent, goldPercent;
        public int RowCount => Math.Max(0, lastRow - firstRow + 1);
        public bool guaranteeOne = true;
    }
    [CreateAssetMenu(menuName = "DigDeep/Game Settings")]
    public sealed class GameSettings : ScriptableObject
    {
        [Header("Blocks and ore")]
        [Min(1)] public int blockHealth = 1;
        [Min(0)] public int blockReward = 1;
        [Min(1)] public int coalHealth = 5, ironOreHealth = 15, goldOreHealth = 15;
        [Min(0)] public int goldSellPrice = 40;
        public SpawnLine[] spawnLines = {
            new SpawnLine { name = "1라인", firstRow = 2, lastRow = 10, coalPercent = 5 },
            new SpawnLine { name = "2라인", firstRow = 11, lastRow = 25, coalPercent = 5, ironPercent = 5 },
            new SpawnLine { name = "3라인", firstRow = 26, lastRow = 45, coalPercent = 1, ironPercent = 7, goldPercent = .8f }
        };
        [Header("Pickaxes")]
        [Min(1)] public float woodPower = 1, stonePower = 1, ironPower = 2, alloyPower = 2.5f;
        [Min(1)] public int woodDurability = 20, stoneDurability = 30, ironDurability = 50, alloyDurability = 80;
        public int[] purchasePrices = { 100, 1000, 10000 };
        [Min(0)] public int stoneUpgradeCost = 50, ironUpgradeCost = 200;
        [Min(0)] public int ironUpgradeOre = 7;
        [Min(0)] public int stoneUpgradeCoal = 3, ironUpgradeCoal = 14;
        [Min(0)] public int alloyUpgradeCost = 500, alloyUpgradeCoal = 30, alloyUpgradeIron = 10, alloyUpgradeGold = 5;
        [Header("Controls")]
        [Min(.1f)] public float holdDelay = .28f, strikeInterval = .34f;
        [Min(1)] public float dragThreshold = 10f;
        public const int Columns = 5, ClimbLimit = 5, MaximumPicks = 4;
        public const string MoneyKey = "DigDeep.Money.v1";
        public int OreHealth(Mineral kind) => kind == Mineral.Coal ? Math.Max(1, coalHealth) : kind == Mineral.Iron ? Math.Max(1, ironOreHealth) : kind == Mineral.Gold ? Math.Max(1, goldOreHealth) : Math.Max(1, blockHealth);
        public int SellPrice(Mineral kind) => kind == Mineral.Stone ? 0 : kind == Mineral.Gold ? Math.Max(0, goldSellPrice) : (int)((long)OreHealth(kind) * 125 / 100);
        public float Power(string id) => id == "ALLOY" ? alloyPower : id == "IRON" ? ironPower : id == "STONE" ? stonePower : woodPower;
        public int Durability(string id) => id == "ALLOY" ? alloyDurability : id == "IRON" ? ironDurability : id == "STONE" ? stoneDurability : woodDurability;
        public static int Tier(string id) => id == "ALLOY" ? 3 : id == "IRON" ? 2 : id == "STONE" ? 1 : 0;
        public static string NextPick(string id) => id == "WOOD" ? "STONE" : id == "STONE" ? "IRON" : id == "IRON" ? "ALLOY" : null;
    }
}
