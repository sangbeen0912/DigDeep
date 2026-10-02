using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEditor.U2D.Sprites;
using UnityEngine;

namespace DigDeep.Editor
{
    public static class DigDeepSetup
    {
        public const string ScenePath = "Assets/DigDeep/Scenes/DigDeep.unity";

        [MenuItem("DigDeep/Setup or refresh prototype scene")]
        public static void Setup()
        {
            Directory.CreateDirectory("Assets/DigDeep/Scenes");
            Directory.CreateDirectory("Assets/DigDeep/Prefabs");
            AssetDatabase.Refresh();
            var importer = (TextureImporter)AssetImporter.GetAtPath("Assets/DigDeep/Art/MiningAtlas.png");
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Multiple;
            importer.filterMode = FilterMode.Point;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.alphaIsTransparency = true;
            importer.isReadable = true;
            importer.mipmapEnabled = false;
            importer.maxTextureSize = 2048;
            importer.SaveAndReimport();
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(importer.assetPath);
            string[] names = { "Stone", "Grass", "Wood", "Iron" };
            var sheet = new SpriteRect[4];
            var factory = new SpriteDataProviderFactories(); factory.Init();
            var provider = factory.GetSpriteEditorDataProviderFromObject(importer);
            provider.InitSpriteEditorDataProvider();
            var existing = provider.GetSpriteRects();
            for (int i = 0; i < 4; i++)
            {
                int x0 = (i % 2) * texture.width / 2;
                int y0 = (i < 2 ? 1 : 0) * texture.height / 2;
                int xmin = x0 + texture.width / 2, ymin = y0 + texture.height / 2, xmax = x0, ymax = y0;
                for (int y = y0; y < y0 + texture.height / 2; y++)
                    for (int x = x0; x < x0 + texture.width / 2; x++)
                        if (texture.GetPixel(x, y).a > .65f)
                        { xmin = Math.Min(xmin, x); ymin = Math.Min(ymin, y); xmax = Math.Max(xmax, x); ymax = Math.Max(ymax, y); }
                // Imported sprite rects slice the original atlas without editing its pixels.
                sheet[i] = new SpriteRect { name = names[i], rect = new Rect(xmin, ymin, xmax - xmin + 1, ymax - ymin + 1),
                    alignment = SpriteAlignment.Center, pivot = new Vector2(.5f, .5f),
                    spriteID = existing.FirstOrDefault(s => s.name == names[i])?.spriteID ?? GUID.Generate() };
            }
            provider.SetSpriteRects(sheet);
            provider.GetDataProvider<ISpriteNameFileIdDataProvider>().SetNameFileIdPairs(sheet.Select(s => new SpriteNameFileIdPair(s.name, s.spriteID)));
            provider.Apply();
            importer.SaveAndReimport();
            var sprites = AssetDatabase.LoadAllAssetsAtPath(importer.assetPath).OfType<Sprite>().ToArray();
            string settingsPath = "Assets/DigDeep/GameSettings.asset";
            var settings = AssetDatabase.LoadAssetAtPath<GameSettings>(settingsPath);
            if (settings == null) { settings = ScriptableObject.CreateInstance<GameSettings>(); AssetDatabase.CreateAsset(settings, settingsPath); }
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var camera = new GameObject("Main Camera").AddComponent<Camera>();
            camera.tag = "MainCamera"; camera.orthographic = true; camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color32(10, 19, 26, 255); camera.transform.position = new Vector3(0, 0, -10);
            var root = new GameObject("DigDeep Game");
            var manager = root.AddComponent<GameManager>();
            manager.settings = settings;
            manager.stone = sprites.First(s => s.name == "Stone"); manager.grass = sprites.First(s => s.name == "Grass");
            manager.wood = sprites.First(s => s.name == "Wood"); manager.iron = sprites.First(s => s.name == "Iron");
            manager.font = AssetDatabase.LoadAssetAtPath<Font>("Assets/DigDeep/Fonts/NotoSansKR.ttf");
            PrefabUtility.SaveAsPrefabAssetAndConnect(root, "Assets/DigDeep/Prefabs/DigDeepGame.prefab", InteractionMode.AutomatedAction);
            EditorSceneManager.SaveScene(scene, ScenePath);
            var otherScenes = EditorBuildSettings.scenes.Where(s => s.path != ScenePath)
                .Select(s => new EditorBuildSettingsScene(s.path, false));
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) }.Concat(otherScenes).ToArray();
            PlayerSettings.productName = "DigDeep";
            PlayerSettings.companyName = "DigDeep";
            PlayerSettings.defaultScreenWidth = 480; PlayerSettings.defaultScreenHeight = 854;
            PlayerSettings.defaultWebScreenWidth = 480; PlayerSettings.defaultWebScreenHeight = 854;
            PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
            PlayerSettings.resizableWindow = true;
            PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Disabled;
            PlayerSettings.WebGL.template = "PROJECT:DigDeep";
            AssetDatabase.SaveAssets();
            RuleChecks.Run();
            Debug.Log("DIGDEEP_SETUP_SUCCESS");
        }

        [MenuItem("DigDeep/Build desktop smoke test")]
        public static void BuildDesktop()
        {
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions { scenes = new[] { ScenePath },
                locationPathName = "Builds/Desktop/DigDeep.exe", target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.Development });
            if (report.summary.result != BuildResult.Succeeded) throw new Exception("Desktop build failed: " + report.summary.result);
            Debug.Log("DIGDEEP_BUILD_SUCCESS");
        }

        [MenuItem("DigDeep/Build mobile web")]
        public static void BuildWeb()
        {
            if (!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.WebGL, BuildTarget.WebGL))
                throw new Exception("Install Web Build Support for Unity 6000.3.23f1 in Unity Hub first.");
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions { scenes = new[] { ScenePath },
                locationPathName = "Builds/Web", target = BuildTarget.WebGL, options = BuildOptions.None });
            if (report.summary.result != BuildResult.Succeeded) throw new Exception("Web build failed: " + report.summary.result);
        }
    }
}
