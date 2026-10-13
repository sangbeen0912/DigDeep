using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.U2D.Sprites;
using UnityEngine;
namespace DigDeep.Editor
{
    public static class MiningContentSetup
    {
        [MenuItem("DigDeep/Apply mining content and validate (no build)")]
        public static void ApplyAndVerify()
        {
            AssetDatabase.Refresh();
            ImportArt(); ApplySettings(); ConnectArt();
            AssetDatabase.SaveAssets();
            MiningFeatureChecks.Rules();
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "-digdeep-revision-test") >= 0) MiningFeatureChecks.RunIntegration();
            Debug.Log("DIGDEEP_MINING_CONTENT_VERIFIED_NO_BUILD");
        }
        static void ApplySettings()
        {
            var s = AssetDatabase.LoadAssetAtPath<GameSettings>("Assets/DigDeep/GameSettings.asset");
            if (s == null) throw new Exception("Game settings asset missing");
            s.blockHealth = 1; s.blockReward = 1; s.coalHealth = 5; s.ironOreHealth = 15; s.goldOreHealth = 15; s.goldSellPrice = 40;
            s.woodPower = 1; s.woodDurability = 20; s.stonePower = 1; s.stoneDurability = 30; s.ironPower = 2; s.ironDurability = 50; s.alloyPower = 2.5f; s.alloyDurability = 80;
            s.purchasePrices = new[] { 100, 1000, 10000 }; s.stoneUpgradeCost = 50; s.ironUpgradeCost = 200; s.ironUpgradeOre = 7;
            s.stoneUpgradeCoal = 3; s.ironUpgradeCoal = 14;
            s.alloyUpgradeCost = 500; s.alloyUpgradeCoal = 30; s.alloyUpgradeIron = 10; s.alloyUpgradeGold = 5;
            s.spawnLines = new[] {
                new SpawnLine { name = "1라인", firstRow = 2, lastRow = 10, coalPercent = 5, guaranteeOne = true },
                new SpawnLine { name = "2라인", firstRow = 11, lastRow = 25, coalPercent = 5, ironPercent = 5, guaranteeOne = true },
                new SpawnLine { name = "3라인", firstRow = 26, lastRow = 45, coalPercent = 1, ironPercent = 7, goldPercent = .8f, guaranteeOne = true }
            };
            EditorUtility.SetDirty(s);
        }
        static TextureImporter Importer(string path, SpriteImportMode mode)
        {
            var i = (TextureImporter)AssetImporter.GetAtPath(path);
            if (i == null) throw new Exception("Art missing: " + path);
            i.textureType = TextureImporterType.Sprite; i.spriteImportMode = mode;
            i.filterMode = FilterMode.Point; i.mipmapEnabled = false; i.alphaIsTransparency = true;
            i.textureCompression = TextureImporterCompression.Uncompressed; i.maxTextureSize = 4096;
            return i;
        }
        public static void ImportArt()
        {
            const string path = "Assets/DigDeep/Art/Pickaxes-v3.png";
            var i = Importer(path, SpriteImportMode.Multiple); i.isReadable = true; i.SaveAndReimport();
            var t = AssetDatabase.LoadAssetAtPath<Texture2D>(path); var pixels = t.GetPixels32();
            var factory = new SpriteDataProviderFactories(); factory.Init();
            var provider = factory.GetSpriteEditorDataProviderFromObject(i); provider.InitSpriteEditorDataProvider();
            var existing = provider.GetSpriteRects(); var rects = new SpriteRect[4];
            // Identical sprite dimensions keep every material at the same displayed proportions.
            // This slices the original atlas; no source pixels are modified.
            var bounds = new RectInt[4]; int commonWidth = 0, commonHeight = 0;
            for (int column = 0; column < 4; column++)
            {
                int start = column * t.width / 4, end = (column + 1) * t.width / 4;
                int x0 = end, x1 = start, y0 = t.height, y1 = 0;
                for (int y = 0; y < t.height; y++) for (int x = start; x < end; x++)
                    if (pixels[y * t.width + x].a > 165) { x0 = Math.Min(x0, x); x1 = Math.Max(x1, x); y0 = Math.Min(y0, y); y1 = Math.Max(y1, y); }
                if (x0 > x1) throw new Exception("Empty pickaxe sprite column");
                bounds[column] = new RectInt(x0, y0, x1 - x0 + 1, y1 - y0 + 1);
                commonWidth = Math.Max(commonWidth, bounds[column].width); commonHeight = Math.Max(commonHeight, bounds[column].height);
            }
            string[] names = { "SymmetricWood", "SymmetricStone", "SymmetricIron", "SymmetricAlloy" };
            for (int column = 0; column < 4; column++)
            {
                var b = bounds[column]; int x = b.x + (b.width - commonWidth) / 2, y = b.y + (b.height - commonHeight) / 2;
                rects[column] = new SpriteRect { name = names[column], rect = new Rect(x, y, commonWidth, commonHeight),
                    alignment = SpriteAlignment.Center, pivot = new Vector2(.5f, .5f),
                    spriteID = existing.FirstOrDefault(s => s.name == names[column])?.spriteID ?? GUID.Generate() };
            }
            provider.SetSpriteRects(rects);
            provider.GetDataProvider<ISpriteNameFileIdDataProvider>().SetNameFileIdPairs(rects.Select(s => new SpriteNameFileIdPair(s.name, s.spriteID)));
            provider.Apply(); i.isReadable = false; i.SaveAndReimport();
            const string orePath = "Assets/DigDeep/Art/Ores-v2.png";
            var oreImporter = Importer(orePath, SpriteImportMode.Multiple); oreImporter.SaveAndReimport();
            var oreTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(orePath);
            var oreProvider = factory.GetSpriteEditorDataProviderFromObject(oreImporter); oreProvider.InitSpriteEditorDataProvider();
            var previous = oreProvider.GetSpriteRects();
            var oreNames = new[] { "CoalOre", "IronOre", "GoldOre" };
            var oreRects = new SpriteRect[3];
            for (int column = 0; column < 3; column++)
                oreRects[column] = new SpriteRect { name = oreNames[column], rect = new Rect(column * oreTexture.width / 3, 0, oreTexture.width / 3, oreTexture.height),
                    alignment = SpriteAlignment.Center, pivot = new Vector2(.5f, .5f),
                    spriteID = previous.FirstOrDefault(s => s.name == oreNames[column])?.spriteID ?? GUID.Generate() };
            oreProvider.SetSpriteRects(oreRects);
            oreProvider.GetDataProvider<ISpriteNameFileIdDataProvider>().SetNameFileIdPairs(oreRects.Select(s => new SpriteNameFileIdPair(s.name, s.spriteID)));
            oreProvider.Apply(); oreImporter.SaveAndReimport();
        }
        public static void ConnectArt()
        {
            var sprites = AssetDatabase.LoadAllAssetsAtPath("Assets/DigDeep/Art/Pickaxes-v3.png").OfType<Sprite>().ToArray();
            var ores = AssetDatabase.LoadAllAssetsAtPath("Assets/DigDeep/Art/Ores-v2.png").OfType<Sprite>().ToArray();
            void Assign(GameManager m)
            {
                m.wood = sprites.First(s => s.name == "SymmetricWood"); m.stonePick = sprites.First(s => s.name == "SymmetricStone");
                m.iron = sprites.First(s => s.name == "SymmetricIron"); m.alloy = sprites.First(s => s.name == "SymmetricAlloy");
                m.coalOre = ores.First(s => s.name == "CoalOre"); m.ironOre = ores.First(s => s.name == "IronOre"); m.goldOre = ores.First(s => s.name == "GoldOre");
            }
            const string prefabPath = "Assets/DigDeep/Prefabs/DigDeepGame.prefab";
            var prefab = PrefabUtility.LoadPrefabContents(prefabPath);
            try { Assign(prefab.GetComponent<GameManager>()); PrefabUtility.SaveAsPrefabAsset(prefab, prefabPath); }
            finally { PrefabUtility.UnloadPrefabContents(prefab); }
            EditorSceneManager.OpenScene(DigDeepSetup.ScenePath);
            var manager = UnityEngine.Object.FindFirstObjectByType<GameManager>(); Assign(manager);
            PrefabUtility.RecordPrefabInstancePropertyModifications(manager);
            EditorSceneManager.MarkSceneDirty(manager.gameObject.scene); EditorSceneManager.SaveOpenScenes();
        }
    }
}
