using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.U2D.Sprites;
using UnityEngine;
namespace DigDeep.Editor
{
    public static class RefiningContentSetup
    {
        [MenuItem("DigDeep/Apply line four and refinery (no build)")]
        public static void ApplyAndVerify()
        {
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "-digdeep-revision-test") < 0) throw new Exception("Isolated verification flag required");
            AssetDatabase.Refresh();
            Import("DiamondOre", false);
            Import("DiamondPick", true, 504f / 479);
            Import("EnergyConverter", true);
            Connect(); AssetDatabase.SaveAssets();
            GoldAlloyFeatureChecks.ApplyAndVerify();
            UiRecordFeatureChecks.Run();
            RefiningFeatureChecks.Run();
            Debug.Log("DIGDEEP_REFINING_VERIFIED_NO_BUILD");
        }
        public static void Import(string name, bool trim, float aspect = 0, int maxSize = 512)
        {
            string path = "Assets/DigDeep/Art/" + name + ".png";
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            if (importer == null) throw new Exception("Missing art: " + path);
            importer.textureType = TextureImporterType.Sprite; importer.spriteImportMode = SpriteImportMode.Multiple;
            importer.npotScale = TextureImporterNPOTScale.None; importer.filterMode = FilterMode.Point;
            importer.textureCompression = TextureImporterCompression.Uncompressed; importer.mipmapEnabled = false;
            importer.wrapMode = TextureWrapMode.Clamp; importer.alphaIsTransparency = true;
            importer.maxTextureSize = 4096; importer.isReadable = true; importer.SaveAndReimport();
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            Rect area = new Rect(0, 0, texture.width, texture.height);
            if (trim)
            {
                var pixels = texture.GetPixels32(); int x0 = texture.width, x1 = 0, y0 = texture.height, y1 = 0;
                for (int y = 0; y < texture.height; y++) for (int x = 0; x < texture.width; x++)
                    if (pixels[y * texture.width + x].a > 165) { x0 = Math.Min(x0, x); x1 = Math.Max(x1, x); y0 = Math.Min(y0, y); y1 = Math.Max(y1, y); }
                if (x0 > x1) throw new Exception("Empty sprite " + name);
                area = new Rect(x0, y0, x1 - x0 + 1, y1 - y0 + 1);
                if (aspect > 0)
                {
                    var center = area.center;
                    float width = Mathf.Max(area.width, area.height * aspect), height = width / aspect;
                    area = new Rect(Mathf.Clamp(center.x - width / 2, 0, texture.width - width), Mathf.Clamp(center.y - height / 2, 0, texture.height - height), width, height);
                }
            }
            var factory = new SpriteDataProviderFactories(); factory.Init();
            var provider = factory.GetSpriteEditorDataProviderFromObject(importer); provider.InitSpriteEditorDataProvider();
            var previous = provider.GetSpriteRects().FirstOrDefault(s => s.name == name);
            var rect = new SpriteRect { name = name, rect = area, alignment = SpriteAlignment.Center, pivot = new Vector2(.5f, .5f), spriteID = previous?.spriteID ?? GUID.Generate() };
            provider.SetSpriteRects(new[] { rect });
            provider.GetDataProvider<ISpriteNameFileIdDataProvider>().SetNameFileIdPairs(new[] { new SpriteNameFileIdPair(name, rect.spriteID) });
            provider.Apply(); importer.maxTextureSize = maxSize; importer.isReadable = false; importer.SaveAndReimport();
        }
        static void Connect()
        {
            Sprite Load(string name) => AssetDatabase.LoadAllAssetsAtPath("Assets/DigDeep/Art/" + name + ".png").OfType<Sprite>().Single();
            void Assign(GameManager manager) { manager.diamondOre = Load("DiamondOre"); manager.diamondPick = Load("DiamondPick"); manager.energyConverter = Load("EnergyConverter"); }
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
