#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using Combat.Unity.Display;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Combat.Unity.Editor
{
    public sealed class SpriteSheetAnimationBuildRequest
    {
        public string TextureAssetPath { get; set; }
        public int Columns { get; set; } = 4;
        public int Rows { get; set; } = 4;
        public float FramesPerSecond { get; set; } = 8f;
        public bool Loop { get; set; } = true;
        public SpriteAnimationKey AnimationKey { get; set; } = SpriteAnimationKey.Idle;
        public string OutputFolder { get; set; } = "Assets/CombatSamples/Standalone/Config/Animation";
        public string AssetPrefix { get; set; }
        public string TargetAnimationSetAssetPath { get; set; }
        public float PixelsPerUnit { get; set; } = 100f;
    }

    public sealed class SpriteSheetAnimationBuildResult
    {
        public SpriteSheetAnimationBuildResult(
            string clipPath,
            string animationSetPath,
            SpriteAnimationClipAsset clip,
            SpriteAnimationSetAsset animationSet,
            Sprite[] sprites)
        {
            ClipPath = clipPath ?? string.Empty;
            AnimationSetPath = animationSetPath ?? string.Empty;
            Clip = clip;
            AnimationSet = animationSet;
            Sprites = sprites ?? Array.Empty<Sprite>();
        }

        public string ClipPath { get; }
        public string AnimationSetPath { get; }
        public SpriteAnimationClipAsset Clip { get; }
        public SpriteAnimationSetAsset AnimationSet { get; }
        public IReadOnlyList<Sprite> Sprites { get; }
        public int SpriteCount => Sprites.Count;
    }

    public static class SpriteSheetAnimationBuilder
    {
        public static SpriteSheetAnimationBuildResult Build(SpriteSheetAnimationBuildRequest request)
        {
            ValidateRequest(request);

            Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(request.TextureAssetPath);
            TextureImporter importer = AssetImporter.GetAtPath(request.TextureAssetPath) as TextureImporter;
            if (texture == null || importer == null)
            {
                throw new InvalidOperationException($"Texture asset is not importable as a sprite sheet: {request.TextureAssetPath}");
            }

            if (texture.width % request.Columns != 0 || texture.height % request.Rows != 0)
            {
                throw new ArgumentException("Texture dimensions must divide evenly by the requested columns and rows.", nameof(request));
            }

            string assetPrefix = string.IsNullOrWhiteSpace(request.AssetPrefix)
                ? SanitizeAssetName(System.IO.Path.GetFileNameWithoutExtension(request.TextureAssetPath))
                : SanitizeAssetName(request.AssetPrefix);
            string outputFolder = NormalizeAssetPath(request.OutputFolder);
            EnsureFolder(outputFolder);

            Sprite[] sprites = SliceTexture(request, importer, texture, assetPrefix);
            string clipPath = $"{outputFolder}/{assetPrefix}.asset";
            string setPath = ResolveAnimationSetPath(request, outputFolder, assetPrefix);
            SpriteAnimationClipAsset clip = LoadOrCreateClip(clipPath, sprites, request);
            SpriteAnimationSetAsset animationSet = LoadOrCreateAnimationSet(
                setPath,
                request.AnimationKey,
                clip,
                hasExplicitTarget: !string.IsNullOrWhiteSpace(request.TargetAnimationSetAssetPath));

            AssetDatabase.SaveAssets();

            return new SpriteSheetAnimationBuildResult(clipPath, setPath, clip, animationSet, sprites);
        }

        private static void ValidateRequest(SpriteSheetAnimationBuildRequest request)
        {
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            if (string.IsNullOrWhiteSpace(request.TextureAssetPath))
            {
                throw new ArgumentException("Texture asset path is required.", nameof(request));
            }

            if (!request.TextureAssetPath.StartsWith("Assets/", StringComparison.Ordinal))
            {
                throw new ArgumentException("Texture asset path must be under Assets/.", nameof(request));
            }

            if (request.Columns <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(request.Columns), "Columns must be greater than zero.");
            }

            if (request.Rows <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(request.Rows), "Rows must be greater than zero.");
            }

            if (request.FramesPerSecond <= 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(request.FramesPerSecond), "Frames per second must be greater than zero.");
            }

            if (request.PixelsPerUnit <= 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(request.PixelsPerUnit), "Pixels per unit must be greater than zero.");
            }

            if (request.AnimationKey == SpriteAnimationKey.None)
            {
                throw new ArgumentException("Animation key must not be None.", nameof(request));
            }

            if (string.IsNullOrWhiteSpace(request.OutputFolder))
            {
                throw new ArgumentException("Output folder is required.", nameof(request));
            }

            if (!NormalizeAssetPath(request.OutputFolder).StartsWith("Assets/", StringComparison.Ordinal))
            {
                throw new ArgumentException("Output folder must be under Assets/.", nameof(request));
            }

            if (!string.IsNullOrWhiteSpace(request.TargetAnimationSetAssetPath)
                && !NormalizeAssetPath(request.TargetAnimationSetAssetPath).StartsWith("Assets/", StringComparison.Ordinal))
            {
                throw new ArgumentException("Target animation set asset path must be under Assets/.", nameof(request));
            }
        }

        private static Sprite[] SliceTexture(
            SpriteSheetAnimationBuildRequest request,
            TextureImporter importer,
            Texture2D texture,
            string assetPrefix)
        {
            int cellWidth = texture.width / request.Columns;
            int cellHeight = texture.height / request.Rows;
            var metadata = new List<SpriteMetaData>(request.Columns * request.Rows);
            for (var row = 0; row < request.Rows; row++)
            {
                for (var column = 0; column < request.Columns; column++)
                {
                    int index = row * request.Columns + column;
                    metadata.Add(new SpriteMetaData
                    {
                        name = $"{assetPrefix}_{index:00}",
                        rect = new Rect(column * cellWidth, (request.Rows - 1 - row) * cellHeight, cellWidth, cellHeight),
                        alignment = (int)SpriteAlignment.Center,
                        pivot = new Vector2(0.5f, 0.5f)
                    });
                }
            }

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Multiple;
            importer.spritePixelsPerUnit = request.PixelsPerUnit;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.spritesheet = metadata.ToArray();
            EditorUtility.SetDirty(importer);
            importer.SaveAndReimport();
            AssetDatabase.ImportAsset(request.TextureAssetPath, ImportAssetOptions.ForceUpdate);

            return LoadSprites(request.TextureAssetPath, assetPrefix, request.Columns * request.Rows);
        }

        private static Sprite[] LoadSprites(string textureAssetPath, string assetPrefix, int expectedCount)
        {
            Object[] assets = AssetDatabase.LoadAllAssetRepresentationsAtPath(textureAssetPath);
            var sprites = new Sprite[expectedCount];
            for (var i = 0; i < assets.Length; i++)
            {
                Sprite sprite = assets[i] as Sprite;
                if (sprite == null || !sprite.name.StartsWith(assetPrefix + "_", StringComparison.Ordinal))
                {
                    continue;
                }

                string suffix = sprite.name.Substring(assetPrefix.Length + 1);
                if (int.TryParse(suffix, out int index) && index >= 0 && index < expectedCount)
                {
                    sprites[index] = sprite;
                }
            }

            for (var i = 0; i < sprites.Length; i++)
            {
                if (sprites[i] == null)
                {
                    throw new InvalidOperationException($"Missing sliced sprite {assetPrefix}_{i:00}.");
                }
            }

            return sprites;
        }

        private static void ConfigureClip(
            SpriteAnimationClipAsset clip,
            Sprite[] sprites,
            SpriteSheetAnimationBuildRequest request)
        {
            clip.Configure(sprites, request.FramesPerSecond, request.Loop, SpriteAnimationKey.None);
            EditorUtility.SetDirty(clip);
        }

        private static void ConfigureAnimationSet(
            SpriteAnimationSetAsset animationSet,
            SpriteAnimationKey animationKey,
            SpriteAnimationClipAsset clip)
        {
            animationSet.Upsert(new SpriteAnimationEntry(animationKey, clip));
            EditorUtility.SetDirty(animationSet);
        }

        private static SpriteAnimationClipAsset LoadOrCreateClip(
            string assetPath,
            Sprite[] sprites,
            SpriteSheetAnimationBuildRequest request)
        {
            SpriteAnimationClipAsset asset = AssetDatabase.LoadAssetAtPath<SpriteAnimationClipAsset>(assetPath);
            if (asset != null)
            {
                ConfigureClip(asset, sprites, request);
                return asset;
            }

            asset = ScriptableObject.CreateInstance<SpriteAnimationClipAsset>();
            ConfigureClip(asset, sprites, request);
            AssetDatabase.CreateAsset(asset, assetPath);
            return asset;
        }

        private static SpriteAnimationSetAsset LoadOrCreateAnimationSet(
            string assetPath,
            SpriteAnimationKey animationKey,
            SpriteAnimationClipAsset clip,
            bool hasExplicitTarget)
        {
            SpriteAnimationSetAsset asset = AssetDatabase.LoadAssetAtPath<SpriteAnimationSetAsset>(assetPath);
            if (asset != null)
            {
                ConfigureAnimationSet(asset, animationKey, clip);
                return asset;
            }

            if (hasExplicitTarget)
            {
                throw new InvalidOperationException($"Target animation set asset does not exist or is not a SpriteAnimationSetAsset: {assetPath}");
            }

            asset = ScriptableObject.CreateInstance<SpriteAnimationSetAsset>();
            ConfigureAnimationSet(asset, animationKey, clip);
            AssetDatabase.CreateAsset(asset, assetPath);
            return asset;
        }

        private static string ResolveAnimationSetPath(
            SpriteSheetAnimationBuildRequest request,
            string outputFolder,
            string assetPrefix)
        {
            if (!string.IsNullOrWhiteSpace(request.TargetAnimationSetAssetPath))
            {
                return NormalizeAssetPath(request.TargetAnimationSetAssetPath);
            }

            return $"{outputFolder}/{assetPrefix}Set.asset";
        }

        private static void EnsureFolder(string folderPath)
        {
            if (AssetDatabase.IsValidFolder(folderPath))
            {
                return;
            }

            string[] parts = folderPath.Split('/');
            string current = parts[0];
            for (var i = 1; i < parts.Length; i++)
            {
                string next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                {
                    AssetDatabase.CreateFolder(current, parts[i]);
                }

                current = next;
            }
        }

        private static string NormalizeAssetPath(string path)
        {
            return (path ?? string.Empty).Replace('\\', '/').TrimEnd('/');
        }

        private static string SanitizeAssetName(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return "SpriteAnimation";
            }

            char[] invalidChars = System.IO.Path.GetInvalidFileNameChars();
            var chars = name.Trim().ToCharArray();
            for (var i = 0; i < chars.Length; i++)
            {
                for (var invalidIndex = 0; invalidIndex < invalidChars.Length; invalidIndex++)
                {
                    if (chars[i] == invalidChars[invalidIndex])
                    {
                        chars[i] = '_';
                        break;
                    }
                }
            }

            return new string(chars);
        }
    }
}
#endif
