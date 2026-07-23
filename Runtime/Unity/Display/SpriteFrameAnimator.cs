using System;
using UnityEngine;

namespace Combat.Unity.Display
{
    [DisallowMultipleComponent]
    public sealed class SpriteFrameAnimator : MonoBehaviour
    {
        private SpriteRenderer _spriteRenderer;
        private ISpriteAnimationRenderer _renderer;
        private SpriteAnimationSetAsset _animationSet;
        private SpriteAnimationKey _currentKey = SpriteAnimationKey.None;
        private SpriteAnimationClipAsset _currentClip;
        private int _frameIndex;
        private float _elapsedInFrame;
        private bool _isPlaying;

        public event Action<SpriteAnimationKey> Completed;

        public bool IsPlaying => _isPlaying;
        public SpriteAnimationKey CurrentKey => _currentKey;

        internal SpriteAnimationKey CurrentKeyForTests => CurrentKey;

        public void Configure(SpriteAnimationSetAsset animationSet)
        {
            Configure(animationSet, null);
        }

        public void Configure(SpriteAnimationSetAsset animationSet, ISpriteAnimationRenderer renderer)
        {
            _animationSet = animationSet;
            _renderer = renderer ?? CreateDefaultRenderer();
            Stop();
        }

        public bool Play(SpriteAnimationKey key, bool restart = false)
        {
            if (_animationSet == null || key == SpriteAnimationKey.None || !_animationSet.TryGetClip(key, out SpriteAnimationClipAsset clip) || !HasFrames(clip))
            {
                return false;
            }

            return Play(clip, key, restart);
        }

        public bool Play(SpriteAnimationClipAsset clip, SpriteAnimationKey key, bool restart = false)
        {
            if (!restart && _isPlaying && _currentKey == key && _currentClip == clip)
            {
                return true;
            }

            if (key == SpriteAnimationKey.None || !HasFrames(clip))
            {
                return false;
            }

            _currentKey = key;
            _currentClip = clip;
            _frameIndex = 0;
            _elapsedInFrame = 0f;
            _isPlaying = true;
            ApplyCurrentFrame();
            return true;
        }

        public void Stop()
        {
            _currentKey = SpriteAnimationKey.None;
            _currentClip = null;
            _frameIndex = 0;
            _elapsedInFrame = 0f;
            _isPlaying = false;
            _renderer?.Clear();
        }

        public void Tick(float deltaSeconds)
        {
            if (!_isPlaying || _currentClip == null || deltaSeconds <= 0f)
            {
                return;
            }

            float frameDuration = 1f / Mathf.Max(0.001f, _currentClip.FramesPerSecond);
            _elapsedInFrame += deltaSeconds;
            while (_elapsedInFrame >= frameDuration && _isPlaying)
            {
                _elapsedInFrame -= frameDuration;
                AdvanceFrame();
            }
        }

        private void Update()
        {
            Tick(Time.deltaTime);
        }

        private void AdvanceFrame()
        {
            int frameCount = _currentClip.FrameCount;
            if (_frameIndex + 1 < frameCount)
            {
                _frameIndex++;
                ApplyCurrentFrame();
                return;
            }

            if (_currentClip.Loop)
            {
                _frameIndex = 0;
                ApplyCurrentFrame();
                return;
            }

            SpriteAnimationKey completedKey = _currentKey;
            SpriteAnimationClipAsset completedClip = _currentClip;
            SpriteAnimationKey fallbackKey = completedClip.FallbackKey;
            Completed?.Invoke(completedKey);
            if (_currentKey != completedKey || _currentClip != completedClip)
            {
                return;
            }

            if (fallbackKey != SpriteAnimationKey.None && fallbackKey != _currentKey)
            {
                Play(fallbackKey, restart: true);
                return;
            }

            _isPlaying = false;
        }

        private void ApplyCurrentFrame()
        {
            _renderer?.ApplyFrame(_currentClip, _frameIndex);
        }

        private ISpriteAnimationRenderer CreateDefaultRenderer()
        {
            if (_spriteRenderer == null)
            {
                _spriteRenderer = GetComponent<SpriteRenderer>();
                if (_spriteRenderer == null)
                {
                    _spriteRenderer = gameObject.AddComponent<SpriteRenderer>();
                }
            }

            return new SpriteSwapAnimationRenderer(_spriteRenderer);
        }

        private static bool HasFrames(SpriteAnimationClipAsset clip)
        {
            return clip != null && clip.FrameCount > 0;
        }
    }
}
