using System;
using UnityEngine;
namespace DigDeep
{
    [Serializable]
    public sealed class SpawnLine
    {
        public string name;
        [Min(1)] public int firstRow, lastRow;
        [Range(0, 100)] public float coalPercent, ironPercent, goldPercent, diamondPercent, oilPercent;
        public int RowCount => Math.Max(0, lastRow - firstRow + 1);
        public bool guaranteeOne = true;
    }
    [Serializable]
    public sealed class ConversionRecipe
    {
        public Mineral mineral;
        [Min(1)] public int energyTenthsPerOre = 1;
    }
    [CreateAssetMenu(menuName = "DigDeep/Game Settings")]
    public sealed class GameSettings : ScriptableObject
    {
        [Header("Blocks and ore")]
        [Min(1)] public int blockHealth = 1;
        [Min(0)] public int blockReward = 1;
        [Min(1)] public int coalHealth = 5, ironOreHealth = 15, goldOreHealth = 15, diamondOreHealth = 30, oilHealth = 30;
        [Min(0)] public int goldSellPrice = 40, oilSellPrice = 60;
        public SpawnLine[] spawnLines = {
            new SpawnLine { name = "1라인", firstRow = 2, lastRow = 10, coalPercent = 5 },
            new SpawnLine { name = "2라인", firstRow = 11, lastRow = 25, coalPercent = 5, ironPercent = 5 },
            new SpawnLine { name = "3라인", firstRow = 26, lastRow = 45, coalPercent = 1, ironPercent = 7, goldPercent = .8f },
            new SpawnLine { name = "4라인", firstRow = 46, lastRow = 65, coalPercent = 1, ironPercent = 10, goldPercent = 2, diamondPercent = .5f, oilPercent = .1f }
        };
        [Header("Pickaxes")]
        [Min(1)] public float woodPower = 1, stonePower = 1, ironPower = 2, alloyPower = 2.5f, diamondPower = 3;
        [Min(1)] public int woodDurability = 20, stoneDurability = 30, ironDurability = 50, alloyDurability = 80, diamondDurability = 110;
        public int[] purchasePrices = { 100, 1000, 10000 };
        [Min(0)] public int stoneUpgradeCost = 50, ironUpgradeCost = 200;
        [Min(0)] public int ironUpgradeOre = 7;
        [Min(0)] public int stoneUpgradeCoal = 3, ironUpgradeCoal = 14;
        [Min(0)] public int alloyUpgradeCost = 500, alloyUpgradeCoal = 30, alloyUpgradeIron = 10, alloyUpgradeGold = 5;
        [Min(0)] public int diamondUpgradeCost = 500, diamondUpgradeOre = 20, diamondUpgradeEnergyTenths = 50;
        [Header("Mineral to energy conversion (integer tenths)")]
        public ConversionRecipe[] conversionRecipes = { new ConversionRecipe { mineral = Mineral.Coal, energyTenthsPerOre = 1 } };
        [Header("Controls")]
        [Min(.1f)] public float holdDelay = .28f, strikeInterval = .34f;
        [Min(1)] public float dragThreshold = 10f;
        public const int Columns = 5, ClimbLimit = 5, VerticalClimbLimit = 2, MaximumPicks = 4;
        public const string MoneyKey = "DigDeep.Money.v1";
        public int OreHealth(Mineral kind) => kind == Mineral.Coal ? Math.Max(1, coalHealth) : kind == Mineral.Iron ? Math.Max(1, ironOreHealth) : kind == Mineral.Gold ? Math.Max(1, goldOreHealth) : kind == Mineral.Diamond ? Math.Max(1, diamondOreHealth) : kind == Mineral.Oil ? Math.Max(1, oilHealth) : Math.Max(1, blockHealth);
        public int SellPrice(Mineral kind) => kind == Mineral.Stone ? 0 : kind == Mineral.Gold ? Math.Max(0, goldSellPrice) : kind == Mineral.Oil ? Math.Max(0, oilSellPrice) : (int)((long)OreHealth(kind) * 125 / 100);
        public float Power(string id) => id == "DIAMOND" ? diamondPower : id == "ALLOY" ? alloyPower : id == "IRON" ? ironPower : id == "STONE" ? stonePower : woodPower;
        public int Durability(string id) => id == "DIAMOND" ? diamondDurability : id == "ALLOY" ? alloyDurability : id == "IRON" ? ironDurability : id == "STONE" ? stoneDurability : woodDurability;
        public static int Tier(string id) => id == "DIAMOND" ? 4 : id == "ALLOY" ? 3 : id == "IRON" ? 2 : id == "STONE" ? 1 : 0;
        public static string NextPick(string id) => id == "WOOD" ? "STONE" : id == "STONE" ? "IRON" : id == "IRON" ? "ALLOY" : id == "ALLOY" ? "DIAMOND" : null;
        public int ConversionRate(Mineral kind) => OreCatalog.Find(kind) == null || conversionRecipes == null ? 0 : Math.Max(0, Array.Find(conversionRecipes, r => r != null && r.mineral == kind)?.energyTenthsPerOre ?? 0);
        public static string FormatEnergy(int tenths) => (tenths / 10).ToString("N0") + "." + Math.Abs(tenths % 10);
    }
}
