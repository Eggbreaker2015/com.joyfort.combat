using System;
using System.Collections.Generic;
using UnityEngine;

namespace Combat.Unity.Display
{
    [CreateAssetMenu(menuName = "Combat/Display/Sprite Animation Clip", fileName = "SpriteAnimationClip")]
    public sealed class SpriteAnimationClipAsset : ScriptableObject
    {
        [SerializeField] private Sprite[] _frames = Array.Empty<Sprite>();
        [SerializeField] private float _framesPerSecond = 8f;
        [SerializeField] private bool _loop = true;
        [SerializeField] private SpriteAnimationKey _fallbackKey = SpriteAnimationKey.None;

        public IReadOnlyList<Sprite> Frames => _frames ?? Array.Empty<Sprite>();
        public int FrameCount => Frames.Count;
        public float FramesPerSecond => _framesPerSecond;
        public bool Loop => _loop;
        public SpriteAnimationKey FallbackKey => _fallbackKey;

        internal void Configure(Sprite[] frames, float framesPerSecond, bool loop, SpriteAnimationKey fallbackKey)
        {
            _frames = frames ?? Array.Empty<Sprite>();
            _framesPerSecond = framesPerSecond;
            _loop = loop;
            _fallbackKey = fallbackKey;
        }

        internal void ConfigureForTests(Sprite[] frames, float framesPerSecond, bool loop, SpriteAnimationKey fallbackKey)
        {
            Configure(frames, framesPerSecond, loop, fallbackKey);
        }
    }
}
