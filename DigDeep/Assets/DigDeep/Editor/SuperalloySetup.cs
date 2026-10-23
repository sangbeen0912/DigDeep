using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
namespace DigDeep.Editor
{
    public static class SuperalloySetup
    {
        [MenuItem("DigDeep/Apply superalloy and mining speed (no build)")]
        public static void ApplyAndVerify()
        {
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "-digdeep-revision-test") < 0) throw new Exception("Isolated verification flag required");
            AssetDatabase.Refresh();
            RefiningContentSetup.Import("SuperalloyPick", true, 504f / 479, 512);
            Connect(); AssetDatabase.SaveAssets();
            GoldAlloyFeatureChecks.ApplyAndVerify(); UiRecordFeatureChecks.Run();
            RefiningFeatureChecks.Run(); ReadabilityChecks.Run(); SuperalloyChecks.Run();
            Debug.Log("DIGDEEP_SUPERALLOY_VERIFIED_NO_BUILD");
        }
        static void Connect()
        {
            var sprite = AssetDatabase.LoadAllAssetsAtPath("Assets/DigDeep/Art/SuperalloyPick.png").OfType<Sprite>().Single();
            const string path = "Assets/DigDeep/Prefabs/DigDeepGame.prefab";
            var prefab = PrefabUtility.LoadPrefabContents(path);
            try { prefab.GetComponent<GameManager>().superalloyPick = sprite; PrefabUtility.SaveAsPrefabAsset(prefab, path); }
            finally { PrefabUtility.UnloadPrefabContents(prefab); }
            EditorSceneManager.OpenScene(DigDeepSetup.ScenePath);
            var instance = UnityEngine.Object.FindFirstObjectByType<GameManager>(); instance.superalloyPick = sprite;
            PrefabUtility.RecordPrefabInstancePropertyModifications(instance);
            EditorSceneManager.MarkSceneDirty(instance.gameObject.scene); EditorSceneManager.SaveOpenScenes();
        }
    }
}
