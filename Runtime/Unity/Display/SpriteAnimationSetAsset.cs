using System;
using UnityEngine;

namespace Combat.Unity.Display
{
    [Serializable]
    public struct SpriteAnimationEntry
    {
        [SerializeField] private SpriteAnimationKey _key;
        [SerializeField] private SpriteAnimationClipAsset _clip;

        public SpriteAnimationEntry(SpriteAnimationKey key, SpriteAnimationClipAsset clip)
        {
            _key = key;
            _clip = clip;
        }

        public SpriteAnimationKey Key => _key;
        public SpriteAnimationClipAsset Clip => _clip;
    }

    [Serializable]
    public struct SpriteAbilityAnimationEntry
    {
        [SerializeField] private string _abilityId;
        [SerializeField] private SpriteAnimationClipAsset _clip;

        public SpriteAbilityAnimationEntry(string abilityId, SpriteAnimationClipAsset clip)
        {
            _abilityId = abilityId;
            _clip = clip;
        }

        public string AbilityId => _abilityId;
        public SpriteAnimationClipAsset Clip => _clip;
    }

    [CreateAssetMenu(menuName = "Combat/Display/Sprite Animation Set", fileName = "SpriteAnimationSet")]
    public sealed class SpriteAnimationSetAsset : ScriptableObject
    {
        [SerializeField] private SpriteAnimationEntry[] _animations = Array.Empty<SpriteAnimationEntry>();
        [SerializeField] private SpriteAbilityAnimationEntry[] _abilityAnimations = Array.Empty<SpriteAbilityAnimationEntry>();

        public bool TryGetClip(SpriteAnimationKey key, out SpriteAnimationClipAsset clip)
        {
            if (_animations != null)
            {
                for (var i = 0; i < _animations.Length; i++)
                {
                    SpriteAnimationEntry entry = _animations[i];
                    if (entry.Key == key && entry.Clip != null)
                    {
                        clip = entry.Clip;
                        return true;
                    }
                }
            }

            clip = null;
            return false;
        }

        public bool TryGetAbilityClip(string abilityId, out SpriteAnimationClipAsset clip)
        {
            if (!string.IsNullOrWhiteSpace(abilityId) && _abilityAnimations != null)
            {
                for (var i = 0; i < _abilityAnimations.Length; i++)
                {
                    SpriteAbilityAnimationEntry entry = _abilityAnimations[i];
                    if (string.Equals(entry.AbilityId, abilityId, StringComparison.Ordinal) && entry.Clip != null)
                    {
                        clip = entry.Clip;
                        return true;
                    }
                }
            }

            clip = null;
            return false;
        }

        internal void Configure(params SpriteAnimationEntry[] entries)
        {
            _animations = entries ?? Array.Empty<SpriteAnimationEntry>();
        }

        internal void Configure(SpriteAnimationEntry[] entries, SpriteAbilityAnimationEntry[] abilityEntries)
        {
            _animations = entries ?? Array.Empty<SpriteAnimationEntry>();
            _abilityAnimations = abilityEntries ?? Array.Empty<SpriteAbilityAnimationEntry>();
        }

        internal void Upsert(SpriteAnimationEntry entry)
        {
            if (_animations == null || _animations.Length == 0)
            {
                _animations = new[] { entry };
                return;
            }

            var replaced = false;
            var animations = new SpriteAnimationEntry[_animations.Length + 1];
            for (var i = 0; i < _animations.Length; i++)
            {
                SpriteAnimationEntry current = _animations[i];
                if (current.Key == entry.Key)
                {
                    animations[i] = entry;
                    replaced = true;
                }
                else
                {
                    animations[i] = current;
                }
            }

            if (replaced)
            {
                Array.Resize(ref animations, _animations.Length);
            }
            else
            {
                animations[_animations.Length] = entry;
            }

            _animations = animations;
        }

        internal void ConfigureForTests(params SpriteAnimationEntry[] entries)
        {
            Configure(entries);
        }

        internal void ConfigureForTests(SpriteAnimationEntry[] entries, SpriteAbilityAnimationEntry[] abilityEntries)
        {
            Configure(entries, abilityEntries);
        }
    }
}
