using UnityEngine;

namespace DigDeep
{
    [CreateAssetMenu(menuName = "DigDeep/Game Settings")]
    public sealed class GameSettings : ScriptableObject
    {
        [Header("Prototype values — tune after play testing")]
        [Min(1)] public int blockHealth = 12;
        [Min(0)] public int blockReward = 2;
        [Min(1)] public int woodPower = 4;
        [Min(1)] public int woodDurability = 120;
        [Min(1)] public int ironPower = 6;
        [Min(1)] public int ironDurability = 180;
        [Min(0.1f)] public float holdDelay = 0.28f;
        [Min(0.1f)] public float strikeInterval = 0.34f;
        [Min(1)] public float dragThreshold = 10f;
        public const int Columns = 5;
        public const int ClimbLimit = 5;
        public const string MoneyKey = "DigDeep.Money.v1";
    }
}
