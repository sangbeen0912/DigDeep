using System;
namespace DigDeep
{
    public sealed class OreEntry
    {
        public readonly Mineral Kind;
        public readonly string Name, Id, Action;
        public readonly bool UpgradeMaterial;
        public OreEntry(Mineral kind, string name, bool material = false)
        { Kind = kind; Name = name; Id = kind.ToString(); Action = "sale-" + Id.ToLowerInvariant(); UpgradeMaterial = material; }
    }
    // Sales rows come from this catalog, rather than a separate button per mineral.
    public static class OreCatalog
    {
        public static readonly OreEntry[] All = { new OreEntry(Mineral.Coal, "석탄", true), new OreEntry(Mineral.Iron, "철광석", true), new OreEntry(Mineral.Gold, "금광석", true), new OreEntry(Mineral.Diamond, "다이아몬드", true), new OreEntry(Mineral.Oil, "석유") };
        public static OreEntry Find(Mineral kind) => Array.Find(All, e => e.Kind == kind);
        public static OreEntry FromId(string id) => Array.Find(All, e => e.Id == id);
        public static OreEntry FromAction(string action) => Array.Find(All, e => e.Action == action);
    }
}
