using UnityEditor;
namespace DigDeep.Editor
{
    public static class RevisionChecks
    {
        [MenuItem("DigDeep/Validate growth and mining rules")]
        public static void Run() => MiningFeatureChecks.Rules();
    }
}
