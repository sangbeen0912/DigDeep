using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
namespace DigDeep.Editor
{
    public static class EquipmentSetup
    {
        [MenuItem("DigDeep/Apply equipment branches and verify (no build)")]
        public static void ApplyAndVerify()
        {
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "-digdeep-revision-test") < 0) throw new Exception("Isolated verification flag required");
            AssetDatabase.Refresh();
            foreach (var name in new[] { "Drill", "Crusher", "Dynamite" }) RefiningContentSetup.Import(name, true, 0, 128);
            RefiningContentSetup.Import("TungstenPick", true, 504f / 479, 128);
            var s = AssetDatabase.LoadAssetAtPath<GameSettings>("Assets/DigDeep/GameSettings.asset");
            s.equipmentCost = 5000; s.equipmentEnergyTenths = 1000;
            s.drillPower = 3; s.drillDurability = 60; s.drillSpeed = 3;
            s.tungstenPower = 7; s.tungstenDurability = 250; s.tungstenSpeed = 1.5f;
            s.crusherPower = 20; s.crusherDurability = 20; s.crusherSpeed = .33f; s.crusherExtraDrops = 1;
            s.dynamitePower = 500; s.dynamiteDurability = 5; EditorUtility.SetDirty(s);
            Connect(); AssetDatabase.SaveAssets();
            GoldAlloyFeatureChecks.ApplyAndVerify(); UiRecordFeatureChecks.Run(); RefiningFeatureChecks.Run(); ReadabilityChecks.Run(); SuperalloyChecks.Run();
            EquipmentChecks.Run(); Debug.Log("DIGDEEP_EQUIPMENT_VERIFIED_NO_BUILD");
        }
        public static void BuildVerifiedWeb() { EquipmentChecks.Run(); DigDeepSetup.BuildWeb(); }
        static void Assign(GameManager game)
        {
            Sprite Load(string name) => AssetDatabase.LoadAllAssetsAtPath("Assets/DigDeep/Art/" + name + ".png").OfType<Sprite>().Single();
            game.drill = Load("Drill"); game.tungsten = Load("TungstenPick"); game.crusher = Load("Crusher"); game.dynamite = Load("Dynamite");
            var helper = game.GetComponentInChildren<DigDeep.Presentation.PresentationResourcesButton>(true);
            if (helper != null)
            {
                helper.transform.name = "TestingTools";
                helper.money = 100000; helper.coal = 500; helper.iron = 500; helper.gold = 100;
                helper.diamond = 200; helper.oil = 50; helper.energyTenths = 5000;
            }
        }
        static void Connect()
        {
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
