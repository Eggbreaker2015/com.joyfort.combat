using Combat.Unity.Display;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Combat.Tests.Unity
{
    public sealed class SpriteAnimationAutoPlayerTests
    {
        [Test]
        public void PlayConfiguredAnimation_ConfiguresAnimatorAndStartsConfiguredKey()
        {
            var gameObject = new GameObject("AnimatedView");
            SpriteAnimationClipAsset clip = null;
            SpriteAnimationSetAsset animationSet = null;
            try
            {
                Sprite firstFrame = CreateSprite();
                var spriteRenderer = gameObject.AddComponent<SpriteRenderer>();
                var animator = gameObject.AddComponent<SpriteFrameAnimator>();
                var autoPlayer = gameObject.AddComponent<SpriteAnimationAutoPlayer>();
                clip = CreateClip(8f, loop: true, SpriteAnimationKey.None, firstFrame);
                animationSet = CreateSet(new SpriteAnimationEntry(SpriteAnimationKey.Idle, clip));
                var serializedObject = new UnityEditor.SerializedObject(autoPlayer);
                serializedObject.FindProperty("_animationSet").objectReferenceValue = animationSet;
                serializedObject.FindProperty("_animationKey").enumValueIndex = (int)SpriteAnimationKey.Idle;
                serializedObject.ApplyModifiedPropertiesWithoutUndo();

                bool started = autoPlayer.PlayConfiguredAnimation();

                Assert.IsTrue(started);
                Assert.IsTrue(animator.IsPlaying);
                Assert.AreEqual(SpriteAnimationKey.Idle, animator.CurrentKeyForTests);
                Assert.AreSame(firstFrame, spriteRenderer.sprite);
            }
            finally
            {
                Object.DestroyImmediate(gameObject);
                Object.DestroyImmediate(clip);
                Object.DestroyImmediate(animationSet);
            }
        }

        private static SpriteAnimationClipAsset CreateClip(float framesPerSecond, bool loop, SpriteAnimationKey fallbackKey, params Sprite[] frames)
        {
            var clip = ScriptableObject.CreateInstance<SpriteAnimationClipAsset>();
            clip.ConfigureForTests(frames, framesPerSecond, loop, fallbackKey);
            return clip;
        }

        private static SpriteAnimationSetAsset CreateSet(params SpriteAnimationEntry[] entries)
        {
            var animationSet = ScriptableObject.CreateInstance<SpriteAnimationSetAsset>();
            animationSet.ConfigureForTests(entries);
            return animationSet;
        }

        private static Sprite CreateSprite()
        {
            var texture = new Texture2D(1, 1);
            texture.SetPixel(0, 0, Color.white);
            texture.Apply();
            return Sprite.Create(texture, new Rect(0, 0, 1, 1), Vector2.one * 0.5f);
        }
    }
}
