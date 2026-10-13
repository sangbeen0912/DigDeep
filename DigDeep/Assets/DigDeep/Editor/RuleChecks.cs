using UnityEditor;
namespace DigDeep.Editor
{
    public static class RuleChecks
    {
        [MenuItem("DigDeep/Validate game rules")]
        public static void Run() => MiningFeatureChecks.Rules();
    }
}
