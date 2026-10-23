using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
namespace DigDeep.Presentation.Editor
{
    public static class PresentationSetup
    {
        public static void Install()
        {
            const string path = "Assets/DigDeep/Prefabs/DigDeepGame.prefab";
            var prefab = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var child = prefab.transform.Find("TestingTools") ?? prefab.transform.Find("PresentationTools");
                if (child == null) { child = new GameObject("TestingTools").transform; child.SetParent(prefab.transform, false); }
                child.name = "TestingTools";
                if (child.GetComponent<PresentationResourcesButton>() == null) child.gameObject.AddComponent<PresentationResourcesButton>();
                PrefabUtility.SaveAsPrefabAsset(prefab, path);
            }
            finally { PrefabUtility.UnloadPrefabContents(prefab); }
            EditorSceneManager.OpenScene(DigDeep.Editor.DigDeepSetup.ScenePath);
        }
    }
}
