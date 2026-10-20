using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
namespace DigDeep.Editor
{
    public static class ReadabilitySetup
    {
        [MenuItem("DigDeep/Apply currency icons and tutorial readability (no build)")]
        public static void ApplyAndVerify()
        {
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "-digdeep-revision-test") < 0) throw new Exception("Isolated verification flag required");
            AssetDatabase.Refresh();
            RefiningContentSetup.Import("MoneyIcon", true, 0, 128);
            RefiningContentSetup.Import("EnergyIcon", true, 0, 128);
            Connect(); AssetDatabase.SaveAssets();
            GoldAlloyFeatureChecks.ApplyAndVerify(); UiRecordFeatureChecks.Run(); RefiningFeatureChecks.Run();
            ReadabilityChecks.Run(); Debug.Log("DIGDEEP_READABILITY_VERIFIED_NO_BUILD");
        }
        static void Connect()
        {
            Sprite Load(string name) => AssetDatabase.LoadAllAssetsAtPath("Assets/DigDeep/Art/" + name + ".png").OfType<Sprite>().Single();
            void Assign(GameManager manager) { manager.moneyIcon = Load("MoneyIcon"); manager.energyIcon = Load("EnergyIcon"); }
            const string path = "Assets/DigDeep/Prefabs/DigDeepGame.prefab";
            var prefab = PrefabUtility.LoadPrefabContents(path);
            try { Assign(prefab.GetComponent<GameManager>()); PrefabUtility.SaveAsPrefabAsset(prefab, path); }
            finally { PrefabUtility.UnloadPrefabContents(prefab); }
            EditorSceneManager.OpenScene(DigDeepSetup.ScenePath);
            var instance = UnityEngine.Object.FindFirstObjectByType<GameManager>(); Assign(instance);
            PrefabUtility.RecordPrefabInstancePropertyModifications(instance);
            EditorSceneManager.MarkSceneDirty(instance.gameObject.scene); EditorSceneManager.SaveOpenScenes();
        }
    }
}
