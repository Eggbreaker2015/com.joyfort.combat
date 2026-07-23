#if UNITY_EDITOR
using System.IO;
using Combat.Unity.Display;
using Combat.Unity.Editor;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Combat.Tests.Unity.Editor
{
    public sealed class SpriteSheetAnimationBuilderTests
    {
        private const string GeneratedRoot = "Assets/__CombatPackageTests";
        private const string TestRoot = GeneratedRoot + "/Generated/SpriteSheetAnimationBuilder";

        [TearDown]
        public void TearDown()
        {
            AssetDatabase.DeleteAsset(GeneratedRoot);
            AssetDatabase.Refresh();
        }

        [Test]
        public void Build_SlicesRegularSheetAndCreatesAnimationAssets()
        {
            string texturePath = TestRoot + "/TestSheet.png";
            string outputFolder = TestRoot + "/Animation";
            CreateTestSheet(texturePath, width: 4, height: 4);
            var request = new SpriteSheetAnimationBuildRequest
            {
                TextureAssetPath = texturePath,
                Columns = 2,
                Rows = 2,
                FramesPerSecond = 12f,
                Loop = false,
                AnimationKey = SpriteAnimationKey.Hit,
                OutputFolder = outputFolder,
                AssetPrefix = "TestSheetHit"
            };

            SpriteSheetAnimationBuildResult result = SpriteSheetAnimationBuilder.Build(request);

            Assert.AreEqual(4, result.SpriteCount, "Builder result should expose four sliced sprites.");
            for (var i = 0; i < result.Sprites.Count; i++)
            {
                Assert.NotNull(result.Sprites[i]);
                Assert.AreEqual("TestSheetHit_" + i.ToString("00"), result.Sprites[i].name);
            }

            Assert.AreEqual(outputFolder + "/TestSheetHit.asset", result.ClipPath);
            Assert.AreEqual(outputFolder + "/TestSheetHitSet.asset", result.AnimationSetPath);

            SpriteAnimationClipAsset clip = result.Clip;
            Assert.NotNull(clip);
            Assert.AreEqual(result.ClipPath, AssetDatabase.GetAssetPath(clip));
            Assert.IsTrue(File.Exists(result.ClipPath));
            var clipObject = new SerializedObject(clip);
            SerializedProperty frames = clipObject.FindProperty("_frames");
            Assert.AreEqual(4, frames.arraySize, "Generated clip should serialize four frames.");
            for (var i = 0; i < frames.arraySize; i++)
            {
                Assert.AreSame(result.Sprites[i], frames.GetArrayElementAtIndex(i).objectReferenceValue);
            }

            Assert.AreEqual(12f, clipObject.FindProperty("_framesPerSecond").floatValue);
            Assert.IsFalse(clipObject.FindProperty("_loop").boolValue);
            Assert.AreEqual((int)SpriteAnimationKey.None, clipObject.FindProperty("_fallbackKey").enumValueIndex);

            SpriteAnimationSetAsset animationSet = result.AnimationSet;
            Assert.NotNull(animationSet);
            Assert.AreEqual(result.AnimationSetPath, AssetDatabase.GetAssetPath(animationSet));
            Assert.IsTrue(File.Exists(result.AnimationSetPath));
            var setObject = new SerializedObject(animationSet);
            SerializedProperty animations = setObject.FindProperty("_animations");
            Assert.AreEqual(1, animations.arraySize);
            SerializedProperty entry = animations.GetArrayElementAtIndex(0);
            Assert.AreEqual((int)SpriteAnimationKey.Hit, entry.FindPropertyRelative("_key").enumValueIndex);
            Assert.AreSame(clip, entry.FindPropertyRelative("_clip").objectReferenceValue);

            TextureImporter importer = AssetImporter.GetAtPath(texturePath) as TextureImporter;
            Assert.NotNull(importer);
            Assert.AreEqual(SpriteImportMode.Multiple, importer.spriteImportMode);
            Assert.AreEqual(4, importer.spritesheet.Length, "Texture importer should persist four sprite sheet entries.");
        }

        [Test]
        public void Build_WhenTargetAnimationSetHasMatchingKey_ReplacesOnlyThatEntry()
        {
            string texturePath = TestRoot + "/ReplaceSheet.png";
            string outputFolder = TestRoot + "/Animation";
            string setPath = outputFolder + "/ExistingSet.asset";
            CreateTestSheet(texturePath, width: 4, height: 4);
            SpriteAnimationClipAsset oldIdleClip = CreateClipAsset(outputFolder + "/OldIdle.asset");
            SpriteAnimationClipAsset preservedHitClip = CreateClipAsset(outputFolder + "/PreservedHit.asset");
            SpriteAnimationSetAsset animationSet = CreateAnimationSetAsset(
                setPath,
                new SpriteAnimationEntry(SpriteAnimationKey.Idle, oldIdleClip),
                new SpriteAnimationEntry(SpriteAnimationKey.Hit, preservedHitClip));
            var request = new SpriteSheetAnimationBuildRequest
            {
                TextureAssetPath = texturePath,
                Columns = 2,
                Rows = 2,
                FramesPerSecond = 10f,
                Loop = true,
                AnimationKey = SpriteAnimationKey.Idle,
                OutputFolder = outputFolder,
                AssetPrefix = "ReplacementIdle",
                TargetAnimationSetAssetPath = setPath
            };

            SpriteSheetAnimationBuildResult result = SpriteSheetAnimationBuilder.Build(request);

            Assert.AreSame(animationSet, result.AnimationSet);
            Assert.AreEqual(setPath, result.AnimationSetPath);
            Assert.IsTrue(animationSet.TryGetClip(SpriteAnimationKey.Idle, out SpriteAnimationClipAsset idleClip));
            Assert.AreSame(result.Clip, idleClip);
            Assert.AreNotSame(oldIdleClip, idleClip);
            Assert.IsTrue(animationSet.TryGetClip(SpriteAnimationKey.Hit, out SpriteAnimationClipAsset hitClip));
            Assert.AreSame(preservedHitClip, hitClip);
            AssertAnimationSetEntries(
                animationSet,
                (SpriteAnimationKey.Idle, result.Clip),
                (SpriteAnimationKey.Hit, preservedHitClip));
        }

        [Test]
        public void Build_WhenTargetAnimationSetDoesNotHaveMatchingKey_AppendsEntry()
        {
            string texturePath = TestRoot + "/AppendSheet.png";
            string outputFolder = TestRoot + "/Animation";
            string setPath = outputFolder + "/ExistingSet.asset";
            CreateTestSheet(texturePath, width: 4, height: 4);
            SpriteAnimationClipAsset preservedIdleClip = CreateClipAsset(outputFolder + "/PreservedIdle.asset");
            SpriteAnimationSetAsset animationSet = CreateAnimationSetAsset(
                setPath,
                new SpriteAnimationEntry(SpriteAnimationKey.Idle, preservedIdleClip));
            var request = new SpriteSheetAnimationBuildRequest
            {
                TextureAssetPath = texturePath,
                Columns = 2,
                Rows = 2,
                FramesPerSecond = 10f,
                Loop = true,
                AnimationKey = SpriteAnimationKey.Hit,
                OutputFolder = outputFolder,
                AssetPrefix = "AddedHit",
                TargetAnimationSetAssetPath = setPath
            };

            SpriteSheetAnimationBuildResult result = SpriteSheetAnimationBuilder.Build(request);

            Assert.AreSame(animationSet, result.AnimationSet);
            Assert.AreEqual(setPath, result.AnimationSetPath);
            Assert.IsTrue(animationSet.TryGetClip(SpriteAnimationKey.Idle, out SpriteAnimationClipAsset idleClip));
            Assert.AreSame(preservedIdleClip, idleClip);
            Assert.IsTrue(animationSet.TryGetClip(SpriteAnimationKey.Hit, out SpriteAnimationClipAsset hitClip));
            Assert.AreSame(result.Clip, hitClip);
            AssertAnimationSetEntries(
                animationSet,
                (SpriteAnimationKey.Idle, preservedIdleClip),
                (SpriteAnimationKey.Hit, result.Clip));
        }

        private static void CreateTestSheet(string texturePath, int width, int height)
        {
            EnsureFolder(Path.GetDirectoryName(texturePath).Replace('\\', '/'));
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, mipChain: false);
            for (var y = 0; y < height; y++)
            {
                for (var x = 0; x < width; x++)
                {
                    texture.SetPixel(x, y, new Color(x / (float)width, y / (float)height, 1f, 1f));
                }
            }

            texture.Apply();
            File.WriteAllBytes(texturePath, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(texturePath, ImportAssetOptions.ForceUpdate);
        }

        private static SpriteAnimationClipAsset CreateClipAsset(string clipPath)
        {
            EnsureFolder(Path.GetDirectoryName(clipPath).Replace('\\', '/'));
            var clip = ScriptableObject.CreateInstance<SpriteAnimationClipAsset>();
            clip.ConfigureForTests(new Sprite[0], framesPerSecond: 8f, loop: true, fallbackKey: SpriteAnimationKey.None);
            AssetDatabase.CreateAsset(clip, clipPath);
            return clip;
        }

        private static SpriteAnimationSetAsset CreateAnimationSetAsset(
            string setPath,
            params SpriteAnimationEntry[] entries)
        {
            EnsureFolder(Path.GetDirectoryName(setPath).Replace('\\', '/'));
            var animationSet = ScriptableObject.CreateInstance<SpriteAnimationSetAsset>();
            animationSet.ConfigureForTests(entries);
            AssetDatabase.CreateAsset(animationSet, setPath);
            return animationSet;
        }

        private static void AssertAnimationSetEntries(
            SpriteAnimationSetAsset animationSet,
            params (SpriteAnimationKey Key, SpriteAnimationClipAsset Clip)[] expectedEntries)
        {
            var setObject = new SerializedObject(animationSet);
            SerializedProperty animations = setObject.FindProperty("_animations");
            Assert.AreEqual(expectedEntries.Length, animations.arraySize);
            for (var i = 0; i < expectedEntries.Length; i++)
            {
                SerializedProperty entry = animations.GetArrayElementAtIndex(i);
                Assert.AreEqual((int)expectedEntries[i].Key, entry.FindPropertyRelative("_key").enumValueIndex);
                Assert.AreSame(expectedEntries[i].Clip, entry.FindPropertyRelative("_clip").objectReferenceValue);
            }
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
    }
}
#endif
