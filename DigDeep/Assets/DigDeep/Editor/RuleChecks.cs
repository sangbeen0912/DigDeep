using System;
using UnityEditor;
using UnityEngine;

namespace DigDeep.Editor
{
    public static class RuleChecks
    {
        static int count;
        static void Check(bool value, string message)
        { count++; if (!value) throw new Exception("Rule check failed: " + message); }

        [MenuItem("DigDeep/Validate game rules")]
        public static void Run()
        {
            count = 0;
            var config = ScriptableObject.CreateInstance<GameSettings>();
            config.blockHealth = 12; config.blockReward = 2; config.woodPower = 8;
            config.woodDurability = 5; config.ironPower = 10; config.ironDurability = 19;
            var session = new MiningSession(config, 37);
            Check(!session.Strike(), "cannot mine before PLAY");
            session.Start(); session.Strike();
            Check(session.Health(new Vector2Int(2, 0)) == 7, "damage capped by remaining durability");
            Check(session.Picks[0].Remaining == 0 && !session.CanMine, "exhausted tool disabled");
            Check(session.State == SessionState.Playing, "another usable pick prevents game over");
            Check(session.Position == new Vector2Int(2, -1), "partial block keeps pick before target");
            Check(session.Select(1) && session.Target == new Vector2Int(2, 0), "manual switch preserves target");
            session.Strike();
            Check(session.Picks[1].Remaining == 12 && session.Money == 39, "overkill does not overcharge durability");
            Check(session.Position == new Vector2Int(2, 0), "destroyed target becomes position");
            Check(!session.Drop(new Vector2Int(0, 0)), "cannot cross blocked intermediate cell");
            Check(!session.Drop(new Vector2Int(2, 1)), "drag cannot descend");
            Check(session.Drop(new Vector2Int(1, 0)), "adjacent solid destination can be mined");
            session.Strike(); session.Strike();
            Check(session.State == SessionState.GameOver && session.Money == 41, "final hit grants reward before game over");
            session.Restart();
            Check(session.Money == 41 && session.Picks[0].Remaining == 5 && session.Picks[1].Remaining == 19, "restart retains money and restores all tools");
            Check(session.Health(new Vector2Int(2, 0)) == 12 && session.State == SessionState.Ready, "restart resets terrain and awaits PLAY");
            config.woodPower = 12; config.woodDurability = 1200;
            var deep = new MiningSession(config, 0); deep.Start();
            for (int i = 0; i < 8; i++) deep.Strike();
            int before = deep.Picks[0].Remaining;
            Check(!deep.FindPath(new Vector2Int(2, 1), out _), "more than five rows up rejected");
            Check(deep.Drop(new Vector2Int(2, 2)), "five rows up accepted through empty path");
            Check(deep.Picks[0].Remaining == before, "empty movement costs no durability");
            Check(deep.Drop(new Vector2Int(1, 2)), "move toward side target"); deep.Strike();
            Check(deep.Drop(new Vector2Int(1, 1)), "move toward upward target"); deep.Strike();
            Check(deep.FindPath(new Vector2Int(2, 0), out var route) && route.Count == 2, "turning connected path discovered");
            Check(!deep.FindPath(new Vector2Int(-1, 1), out _), "outside grid rejected");
            UnityEngine.Object.DestroyImmediate(config);
            Debug.Log("DIGDEEP_RULE_CHECKS_PASSED " + count);
        }
    }
}
