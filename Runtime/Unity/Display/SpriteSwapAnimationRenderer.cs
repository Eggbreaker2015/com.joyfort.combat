using System;
using UnityEngine;

namespace Combat.Unity.Display
{
    internal sealed class SpriteSwapAnimationRenderer : ISpriteAnimationRenderer
    {
        private readonly SpriteRenderer _spriteRenderer;

        public SpriteSwapAnimationRenderer(SpriteRenderer spriteRenderer)
        {
            _spriteRenderer = spriteRenderer != null ? spriteRenderer : throw new ArgumentNullException(nameof(spriteRenderer));
        }

        public void ApplyFrame(SpriteAnimationClipAsset clip, int frameIndex)
        {
            if (clip == null || frameIndex < 0 || frameIndex >= clip.Frames.Count)
            {
                _spriteRenderer.sprite = null;
                return;
            }

            _spriteRenderer.sprite = clip.Frames[frameIndex];
        }

        public void Clear()
        {
            _spriteRenderer.sprite = null;
        }
    }
}
