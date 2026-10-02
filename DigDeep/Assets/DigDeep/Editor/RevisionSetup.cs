using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.U2D.Sprites;
using UnityEngine;

namespace DigDeep.Editor
{
    public static class RevisionSetup
    {
        public static void ApplyAndVerify()
        {
            AssetDatabase.Refresh();
            const string path = "Assets/DigDeep/Art/CompactPickaxes.png";
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Multiple;
            importer.filterMode = FilterMode.Point; importer.mipmapEnabled = false;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.alphaIsTransparency = true; importer.isReadable = true; importer.maxTextureSize = 2048;
            importer.SaveAndReimport();
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            var pixels = texture.GetPixels32();
            var factory = new SpriteDataProviderFactories(); factory.Init();
            var provider = factory.GetSpriteEditorDataProviderFromObject(importer); provider.InitSpriteEditorDataProvider();
            var previous = provider.GetSpriteRects(); var rects = new SpriteRect[2];
            for (int i = 0; i < 2; i++)
            {
                int left = i * texture.width / 2, minX = left + texture.width / 2, maxX = left, minY = texture.height, maxY = 0;
                for (int y = 0; y < texture.height; y++)
                    for (int x = left; x < left + texture.width / 2; x++)
                        if (pixels[y * texture.width + x].a > 165)
                        { minX = Math.Min(minX, x); maxX = Math.Max(maxX, x); minY = Math.Min(minY, y); maxY = Math.Max(maxY, y); }
                string name = i == 0 ? "CompactWood" : "CompactIron";
                rects[i] = new SpriteRect { name = name, rect = new Rect(minX, minY, maxX - minX + 1, maxY - minY + 1),
                    pivot = new Vector2(.5f, .5f), alignment = SpriteAlignment.Center,
                    spriteID = previous.FirstOrDefault(p => p.name == name)?.spriteID ?? GUID.Generate() };
            }
            provider.SetSpriteRects(rects);
            provider.GetDataProvider<ISpriteNameFileIdDataProvider>().SetNameFileIdPairs(rects.Select(r => new SpriteNameFileIdPair(r.name, r.spriteID)));
            provider.Apply(); importer.SaveAndReimport();
            var sprites = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().ToArray();
            const string prefabPath = "Assets/DigDeep/Prefabs/DigDeepGame.prefab";
            var prefab = PrefabUtility.LoadPrefabContents(prefabPath);
            var manager = prefab.GetComponent<GameManager>();
            manager.wood = sprites.First(s => s.name == "CompactWood"); manager.iron = sprites.First(s => s.name == "CompactIron");
            PrefabUtility.SaveAsPrefabAsset(prefab, prefabPath); PrefabUtility.UnloadPrefabContents(prefab);
            EditorSceneManager.OpenScene(DigDeepSetup.ScenePath);
            manager = UnityEngine.Object.FindFirstObjectByType<GameManager>();
            manager.wood = sprites.First(s => s.name == "CompactWood"); manager.iron = sprites.First(s => s.name == "CompactIron");
            PrefabUtility.RecordPrefabInstancePropertyModifications(manager);
            EditorSceneManager.MarkSceneDirty(manager.gameObject.scene); EditorSceneManager.SaveOpenScenes();
            AssetDatabase.SaveAssets();
            RuleChecks.Run();
            RevisionChecks.Run();
            Debug.Log("DIGDEEP_REVISION_VERIFIED_NO_BUILD");
        }
    }
}
