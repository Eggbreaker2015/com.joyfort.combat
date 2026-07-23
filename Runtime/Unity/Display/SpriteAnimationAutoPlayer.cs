using UnityEngine;

namespace Combat.Unity.Display
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(SpriteFrameAnimator))]
    public sealed class SpriteAnimationAutoPlayer : MonoBehaviour
    {
        [SerializeField] private SpriteAnimationSetAsset _animationSet;
        [SerializeField] private SpriteAnimationKey _animationKey = SpriteAnimationKey.Idle;
        [SerializeField] private bool _restart = true;

        private SpriteFrameAnimator _animator;

        public bool PlayConfiguredAnimation()
        {
            SpriteFrameAnimator animator = GetAnimator();
            if (animator == null)
            {
                return false;
            }

            animator.Configure(_animationSet);
            return animator.Play(_animationKey, _restart);
        }

        private void Start()
        {
            PlayConfiguredAnimation();
        }

        private SpriteFrameAnimator GetAnimator()
        {
            if (_animator == null)
            {
                _animator = GetComponent<SpriteFrameAnimator>();
            }

            return _animator;
        }
    }
}
