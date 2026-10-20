using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.U2D.Sprites;
using UnityEngine;
namespace DigDeep.Editor
{
    public static class UiRecordSetup
    {
        [MenuItem("DigDeep/Apply buttons, records and validate (no build)")]
        public static void ApplyAndVerify()
        {
            AssetDatabase.Refresh();
            Import("Assets/DigDeep/Art/MenuButtons.png", new[] { "ShopButton", "InventoryButton", "UpgradeButton" }, true);
            Import("Assets/DigDeep/Art/DigButton.png", new[] { "DigButton" }, false);
            Connect();
            // The optional presentation folder can be deleted without core compile dependencies.
            var installer = Type.GetType("DigDeep.Presentation.Editor.PresentationSetup, Assembly-CSharp-Editor");
            installer?.GetMethod("Install").Invoke(null, null);
            AssetDatabase.SaveAssets();
            GoldAlloyFeatureChecks.ApplyAndVerify();
            UiRecordFeatureChecks.Run();
            Debug.Log("DIGDEEP_UI_RECORDS_VERIFIED_NO_BUILD");
        }
        static void Import(string path, string[] names, bool preserveIcon)
        {
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            if (importer == null) throw new Exception("Button asset missing: " + path);
            importer.textureType = TextureImporterType.Sprite; importer.spriteImportMode = SpriteImportMode.Multiple;
            importer.filterMode = FilterMode.Point; importer.mipmapEnabled = false; importer.alphaIsTransparency = true;
            importer.textureCompression = TextureImporterCompression.Uncompressed; importer.wrapMode = TextureWrapMode.Clamp;
            // Sprite metadata uses source-image coordinates; scan before runtime downscaling.
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.maxTextureSize = 4096; importer.isReadable = true; importer.SaveAndReimport();
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path); var pixels = texture.GetPixels32();
            var factory = new SpriteDataProviderFactories(); factory.Init();
            var provider = factory.GetSpriteEditorDataProviderFromObject(importer); provider.InitSpriteEditorDataProvider();
            var previous = provider.GetSpriteRects(); var rects = new SpriteRect[names.Length];
            for (int index = 0; index < names.Length; index++)
            {
                int start = index * texture.width / names.Length, end = (index + 1) * texture.width / names.Length;
                int x0 = end, x1 = start, y0 = texture.height, y1 = 0;
                for (int y = 0; y < texture.height; y++) for (int x = start; x < end; x++)
                    if (pixels[y * texture.width + x].a > 165) { x0 = Math.Min(x0, x); x1 = Math.Max(x1, x); y0 = Math.Min(y0, y); y1 = Math.Max(y1, y); }
                if (x0 > x1) throw new Exception("Empty button column: " + index);
                float height = y1 - y0 + 1, border = height * .1f;
                rects[index] = new SpriteRect { name = names[index], rect = new Rect(x0, y0, x1 - x0 + 1, height),
                    border = new Vector4(preserveIcon ? height * .74f : border, border, border, border),
                    alignment = SpriteAlignment.Center, pivot = new Vector2(.5f, .5f),
                    spriteID = previous.FirstOrDefault(r => r.name == names[index])?.spriteID ?? GUID.Generate() };
            }
            provider.SetSpriteRects(rects);
            provider.GetDataProvider<ISpriteNameFileIdDataProvider>().SetNameFileIdPairs(rects.Select(r => new SpriteNameFileIdPair(r.name, r.spriteID)));
            provider.Apply(); importer.maxTextureSize = 1024; importer.isReadable = false; importer.SaveAndReimport();
        }
        static void Connect()
        {
            var menus = AssetDatabase.LoadAllAssetsAtPath("Assets/DigDeep/Art/MenuButtons.png").OfType<Sprite>().ToArray();
            var dig = AssetDatabase.LoadAllAssetsAtPath("Assets/DigDeep/Art/DigButton.png").OfType<Sprite>().Single();
            void Assign(GameManager manager)
            {
                manager.shopSkin = menus.Single(s => s.name == "ShopButton"); manager.inventorySkin = menus.Single(s => s.name == "InventoryButton");
                manager.upgradeSkin = menus.Single(s => s.name == "UpgradeButton"); manager.digSkin = dig;
            }
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
