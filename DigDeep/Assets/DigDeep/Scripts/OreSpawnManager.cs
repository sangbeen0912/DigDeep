using System;
using System.Collections.Generic;
using UnityEngine;
namespace DigDeep
{
    public enum Mineral { Stone, Coal, Iron, Gold }
    // Generate each entire depth band once. Viewing a cell never rerolls it.
    public sealed class OreSpawnManager
    {
        readonly GameSettings settings;
        readonly System.Random random;
        readonly Dictionary<int, Mineral[,]> generated = new Dictionary<int, Mineral[,]>();
        public OreSpawnManager(GameSettings settings, int seed) { this.settings = settings; random = new System.Random(seed); }
        public Mineral Kind(Vector2Int cell)
        {
            if (cell.y < 0 || cell.x < 0 || cell.x >= GameSettings.Columns) return Mineral.Stone;
            int row = cell.y + 1;
            for (int i = 0; i < settings.spawnLines.Length; i++)
            {
                var line = settings.spawnLines[i];
                if (line == null || row < line.firstRow || row > line.lastRow) continue;
                if (!generated.TryGetValue(i, out var map)) { map = Generate(line); generated[i] = map; }
                return map[row - line.firstRow, cell.x];
            }
            return Mineral.Stone; // Unconfigured depth is ordinary rock, not a new line.
        }
        Mineral[,] Generate(SpawnLine line)
        {
            int rows = line.lastRow - line.firstRow + 1;
            if (rows < 1 || rows > 10000) throw new ArgumentException("광물 라인 범위를 확인해 주세요.");
            var map = new Mineral[rows, GameSettings.Columns];
            bool found = false;
            for (int y = 0; y < rows; y++) for (int x = 0; x < GameSettings.Columns; x++)
            {
                bool coal = random.NextDouble() * 100 < line.coalPercent;
                bool iron = random.NextDouble() * 100 < line.ironPercent;
                bool gold = random.NextDouble() * 100 < line.goldPercent;
                map[y, x] = Choose(coal, iron, gold, random);
                found |= map[y, x] != Mineral.Stone;
            }
            if (!found && line.guaranteeOne && (line.coalPercent > 0 || line.ironPercent > 0 || line.goldPercent > 0))
                map[random.Next(rows), random.Next(GameSettings.Columns)] = Choose(line.coalPercent > 0, line.ironPercent > 0, line.goldPercent > 0, random);
            return map;
        }
        public static Mineral Choose(bool coal, bool iron, System.Random random)
            => Choose(coal, iron, false, random);
        public static Mineral Choose(bool coal, bool iron, bool gold, System.Random random)
        {
            int count = (coal ? 1 : 0) + (iron ? 1 : 0) + (gold ? 1 : 0);
            if (count == 0) return Mineral.Stone;
            int selected = random.Next(count);
            if (coal && selected-- == 0) return Mineral.Coal;
            if (iron && selected-- == 0) return Mineral.Iron;
            return Mineral.Gold;
        }
    }
}
