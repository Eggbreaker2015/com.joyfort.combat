using System;
using System.Collections.Generic;
using Combat.Unity.Display;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Combat.Tests.Unity
{
    public sealed class SpriteFrameAnimatorTests
    {
        [Test]
        public void SpriteAnimationKey_AttackStaysBetweenMoveAndHit()
        {
            Assert.AreEqual(0, (int)SpriteAnimationKey.None);
            Assert.AreEqual(1, (int)SpriteAnimationKey.Idle);
            Assert.AreEqual(2, (int)SpriteAnimationKey.Move);
            Assert.AreEqual(3, (int)SpriteAnimationKey.Attack);
            Assert.AreEqual(4, (int)SpriteAnimationKey.Hit);
            Assert.AreEqual(5, (int)SpriteAnimationKey.Death);
            Assert.AreEqual(6, (int)SpriteAnimationKey.ProjectileFly);
            Assert.AreEqual(7, (int)SpriteAnimationKey.ProjectileHit);
            Assert.AreEqual(8, (int)SpriteAnimationKey.StatusApplied);
            Assert.AreEqual(9, (int)SpriteAnimationKey.StatusExpired);
        }

        [Test]
        public void Play_AdvancesFramesAndLoopsByConfiguredFrameRate()
        {
            var gameObject = new GameObject("AnimatedView");
            SpriteAnimationClipAsset clip = null;
            SpriteAnimationSetAsset animationSet = null;
            try
            {
                Sprite firstFrame = CreateSprite();
                Sprite secondFrame = CreateSprite();
                var spriteRenderer = gameObject.AddComponent<SpriteRenderer>();
                var animator = gameObject.AddComponent<SpriteFrameAnimator>();
                clip = CreateClip(2f, loop: true, SpriteAnimationKey.None, firstFrame, secondFrame);
                animationSet = CreateSet(new SpriteAnimationEntry(SpriteAnimationKey.Idle, clip));

                animator.Configure(animationSet);
                animator.Play(SpriteAnimationKey.Idle);

                Assert.AreSame(firstFrame, spriteRenderer.sprite);

                animator.Tick(0.49f);
                Assert.AreSame(firstFrame, spriteRenderer.sprite);

                animator.Tick(0.01f);
                Assert.AreSame(secondFrame, spriteRenderer.sprite);

                animator.Tick(0.5f);
                Assert.AreSame(firstFrame, spriteRenderer.sprite);
            }
            finally
            {
                Object.DestroyImmediate(gameObject);
                Object.DestroyImmediate(clip);
                Object.DestroyImmediate(animationSet);
            }
        }

        [Test]
        public void NonLoopingClip_ReturnsToFallbackAnimation()
        {
            var gameObject = new GameObject("AnimatedView");
            SpriteAnimationClipAsset idleClip = null;
            SpriteAnimationClipAsset hitClip = null;
            SpriteAnimationSetAsset animationSet = null;
            try
            {
                Sprite idleFrame = CreateSprite();
                Sprite hitStartFrame = CreateSprite();
                Sprite hitEndFrame = CreateSprite();
                var spriteRenderer = gameObject.AddComponent<SpriteRenderer>();
                var animator = gameObject.AddComponent<SpriteFrameAnimator>();
                idleClip = CreateClip(4f, loop: true, SpriteAnimationKey.None, idleFrame);
                hitClip = CreateClip(4f, loop: false, SpriteAnimationKey.Idle, hitStartFrame, hitEndFrame);
                animationSet = CreateSet(
                    new SpriteAnimationEntry(SpriteAnimationKey.Idle, idleClip),
                    new SpriteAnimationEntry(SpriteAnimationKey.Hit, hitClip));

                animator.Configure(animationSet);
                animator.Play(SpriteAnimationKey.Hit);

                Assert.AreSame(hitStartFrame, spriteRenderer.sprite);

                animator.Tick(0.25f);
                Assert.AreSame(hitEndFrame, spriteRenderer.sprite);

                animator.Tick(0.25f);
                Assert.AreEqual(SpriteAnimationKey.Idle, animator.CurrentKeyForTests);
                Assert.AreSame(idleFrame, spriteRenderer.sprite);
            }
            finally
            {
                Object.DestroyImmediate(gameObject);
                Object.DestroyImmediate(idleClip);
                Object.DestroyImmediate(hitClip);
                Object.DestroyImmediate(animationSet);
            }
        }

        [Test]
        public void Play_UsesConfiguredRendererBackendForFrameApplication()
        {
            var gameObject = new GameObject("AnimatedView");
            SpriteAnimationClipAsset clip = null;
            SpriteAnimationSetAsset animationSet = null;
            try
            {
                Sprite firstFrame = CreateSprite();
                Sprite secondFrame = CreateSprite();
                var animator = gameObject.AddComponent<SpriteFrameAnimator>();
                var renderer = new RecordingAnimationRenderer();
                clip = CreateClip(2f, loop: true, SpriteAnimationKey.None, firstFrame, secondFrame);
                animationSet = CreateSet(new SpriteAnimationEntry(SpriteAnimationKey.Idle, clip));

                animator.Configure(animationSet, renderer);
                renderer.ClearRecordedCalls();
                animator.Play(SpriteAnimationKey.Idle);
                animator.Tick(0.5f);

                Assert.AreEqual(0, renderer.AppliedFrames[0]);
                Assert.AreEqual(1, renderer.AppliedFrames[1]);
                Assert.AreSame(clip, renderer.AppliedClips[0]);
                Assert.AreSame(clip, renderer.AppliedClips[1]);

                animator.Stop();

                Assert.AreEqual(1, renderer.ClearCount);
            }
            finally
            {
                Object.DestroyImmediate(gameObject);
                Object.DestroyImmediate(clip);
                Object.DestroyImmediate(animationSet);
            }
        }

        [Test]
        public void NonLoopingClip_RaisesCompletedBeforeFallbackAndKeepsPlayingFallback()
        {
            var gameObject = new GameObject("AnimatedView");
            SpriteAnimationClipAsset idleClip = null;
            SpriteAnimationClipAsset hitClip = null;
            SpriteAnimationSetAsset animationSet = null;
            try
            {
                Sprite idleFrame = CreateSprite();
                Sprite hitFrame = CreateSprite();
                var spriteRenderer = gameObject.AddComponent<SpriteRenderer>();
                var animator = gameObject.AddComponent<SpriteFrameAnimator>();
                idleClip = CreateClip(4f, loop: true, SpriteAnimationKey.None, idleFrame);
                hitClip = CreateClip(4f, loop: false, SpriteAnimationKey.Idle, hitFrame);
                animationSet = CreateSet(
                    new SpriteAnimationEntry(SpriteAnimationKey.Idle, idleClip),
                    new SpriteAnimationEntry(SpriteAnimationKey.Hit, hitClip));
                SpriteAnimationKey completedKey = SpriteAnimationKey.None;
                SpriteAnimationKey keyAtCompletion = SpriteAnimationKey.None;
                var completedCount = 0;
                animator.Completed += key =>
                {
                    completedCount++;
                    completedKey = key;
                    keyAtCompletion = animator.CurrentKey;
                };

                animator.Configure(animationSet);
                Assert.IsFalse(animator.IsPlaying);

                animator.Play(SpriteAnimationKey.Hit);
                Assert.IsTrue(animator.IsPlaying);

                animator.Tick(0.25f);

                Assert.AreEqual(1, completedCount);
                Assert.AreEqual(SpriteAnimationKey.Hit, completedKey);
                Assert.AreEqual(SpriteAnimationKey.Hit, keyAtCompletion);
                Assert.AreEqual(SpriteAnimationKey.Idle, animator.CurrentKey);
                Assert.IsTrue(animator.IsPlaying);
                Assert.AreSame(idleFrame, spriteRenderer.sprite);
            }
            finally
            {
                Object.DestroyImmediate(gameObject);
                Object.DestroyImmediate(idleClip);
                Object.DestroyImmediate(hitClip);
                Object.DestroyImmediate(animationSet);
            }
        }

        [Test]
        public void StopAndInterruptedPlay_DoNotRaiseCompleted()
        {
            var gameObject = new GameObject("AnimatedView");
            SpriteAnimationClipAsset idleClip = null;
            SpriteAnimationClipAsset hitClip = null;
            SpriteAnimationSetAsset animationSet = null;
            try
            {
                Sprite idleFrame = CreateSprite();
                Sprite hitFrame = CreateSprite();
                var animator = gameObject.AddComponent<SpriteFrameAnimator>();
                idleClip = CreateClip(4f, loop: true, SpriteAnimationKey.None, idleFrame);
                hitClip = CreateClip(4f, loop: false, SpriteAnimationKey.None, hitFrame);
                animationSet = CreateSet(
                    new SpriteAnimationEntry(SpriteAnimationKey.Idle, idleClip),
                    new SpriteAnimationEntry(SpriteAnimationKey.Hit, hitClip));
                var completedCount = 0;
                animator.Completed += _ => completedCount++;

                animator.Configure(animationSet);
                animator.Play(SpriteAnimationKey.Hit);
                animator.Stop();
                Assert.IsFalse(animator.IsPlaying);

                animator.Play(SpriteAnimationKey.Hit);
                animator.Play(SpriteAnimationKey.Idle, restart: true);

                Assert.AreEqual(0, completedCount);
                Assert.AreEqual(SpriteAnimationKey.Idle, animator.CurrentKey);
                Assert.IsTrue(animator.IsPlaying);
            }
            finally
            {
                Object.DestroyImmediate(gameObject);
                Object.DestroyImmediate(idleClip);
                Object.DestroyImmediate(hitClip);
                Object.DestroyImmediate(animationSet);
            }
        }

        [Test]
        public void Play_MissingConfiguredKey_ReturnsFalseWithoutClearingCurrentAnimation()
        {
            var gameObject = new GameObject("AnimatedView");
            SpriteAnimationClipAsset idleClip = null;
            SpriteAnimationSetAsset animationSet = null;
            try
            {
                Sprite idleFrame = CreateSprite();
                var spriteRenderer = gameObject.AddComponent<SpriteRenderer>();
                var animator = gameObject.AddComponent<SpriteFrameAnimator>();
                idleClip = CreateClip(4f, loop: true, SpriteAnimationKey.None, idleFrame);
                animationSet = CreateSet(new SpriteAnimationEntry(SpriteAnimationKey.Idle, idleClip));

                animator.Configure(animationSet);
                Assert.IsTrue(animator.Play(SpriteAnimationKey.Idle));
                Assert.AreSame(idleFrame, spriteRenderer.sprite);

                bool played = animator.Play(SpriteAnimationKey.Hit, restart: true);

                Assert.IsFalse(played);
                Assert.IsTrue(animator.IsPlaying);
                Assert.AreEqual(SpriteAnimationKey.Idle, animator.CurrentKeyForTests);
                Assert.AreSame(idleFrame, spriteRenderer.sprite);
            }
            finally
            {
                Object.DestroyImmediate(gameObject);
                Object.DestroyImmediate(idleClip);
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
            return Sprite.Create(Texture2D.whiteTexture, new Rect(0f, 0f, 1f, 1f), new Vector2(0.5f, 0.5f), 1f);
        }

        private sealed class RecordingAnimationRenderer : ISpriteAnimationRenderer
        {
            public readonly List<SpriteAnimationClipAsset> AppliedClips = new List<SpriteAnimationClipAsset>();
            public readonly List<int> AppliedFrames = new List<int>();

            public int ClearCount { get; private set; }

            public void ApplyFrame(SpriteAnimationClipAsset clip, int frameIndex)
            {
                AppliedClips.Add(clip);
                AppliedFrames.Add(frameIndex);
            }

            public void Clear()
            {
                ClearCount++;
            }

            public void ClearRecordedCalls()
            {
                AppliedClips.Clear();
                AppliedFrames.Clear();
                ClearCount = 0;
            }
        }
    }
}
