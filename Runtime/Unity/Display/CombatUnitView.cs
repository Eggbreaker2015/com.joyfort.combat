using Combat.Core.Battle;
using Combat.Runtime.Display;
using UnityEngine;

namespace Combat.Unity.Display
{
    public readonly struct UnitAnimationRequest
    {
        public UnitAnimationRequest(SpriteAnimationKey key, bool restart)
            : this(key, restart, null)
        {
        }

        public UnitAnimationRequest(SpriteAnimationKey key, bool restart, string abilityId)
        {
            Key = key;
            Restart = restart;
            AbilityId = abilityId;
        }

        public SpriteAnimationKey Key { get; }
        public bool Restart { get; }
        public string AbilityId { get; }

        public static UnitAnimationRequest Idle => new UnitAnimationRequest(SpriteAnimationKey.Idle, restart: true);
        public static UnitAnimationRequest Move => new UnitAnimationRequest(SpriteAnimationKey.Move, restart: false);
        public static UnitAnimationRequest Hit => new UnitAnimationRequest(SpriteAnimationKey.Hit, restart: true);

        public static UnitAnimationRequest Action(ActionViewSnapshot snapshot)
        {
            return new UnitAnimationRequest(SpriteAnimationKey.Attack, restart: true, snapshot.AbilityId);
        }
    }

    [DisallowMultipleComponent]
    public sealed class CombatUnitView : MonoBehaviour
    {
        private const float UnitSortDepthPerId = 0.000001f;

        [SerializeField] private SpriteAnimationSetAsset _animationSet;

        private UnityUnitFacingMode _facingMode = UnityUnitFacingMode.Rotate2D;
        private UnityCombatViewSmoothingSettings _smoothingSettings = UnityCombatViewSmoothingSettings.Default;
        private SpriteAnimationSetAsset _runtimeAnimationSet;
        private SpriteFrameAnimator _animator;
        private SpriteRenderer _spriteRenderer;
        private CombatViewTransformSmoother _smoother;
        private float _sortDepth;

        public void Initialize(
            UnitSpawnViewSnapshot snapshot,
            UnityUnitFacingMode facingMode,
            UnityCombatViewSmoothingSettings smoothingSettings,
            Sprite fallbackSprite)
        {
            _facingMode = facingMode;
            _smoothingSettings = smoothingSettings;
            _sortDepth = -snapshot.UnitId.Value * UnitSortDepthPerId;

            Transform unitTransform = transform;
            unitTransform.position = ToVector3(snapshot.Position);
            unitTransform.localScale = Vector3.one;
            ApplyFacing(snapshot.Facing, snap: true);

            _smoother = GetOrAddSmoother();
            _smoother.Configure(_smoothingSettings);
            _smoother.SnapTo(unitTransform.position, unitTransform.localRotation);

            Configure();
            PlayAnimation(UnitAnimationRequest.Idle, fallbackSprite);
        }

        public void Configure()
        {
            _runtimeAnimationSet = _animationSet;

            if (_runtimeAnimationSet == null)
            {
                if (_animator != null)
                {
                    _animator.Configure(null);
                }

                return;
            }

            _animator = GetOrAddAnimator();
            _animator.Configure(_runtimeAnimationSet);
        }

        public void ApplyMove(BattleVector2 position)
        {
            GetOrAddSmoother().SetTargetPosition(ToVector3(position));
            PlayAnimation(UnitAnimationRequest.Move);
        }

        public void StopMovement()
        {
            GetOrAddSmoother().SnapPosition(transform.position);
        }

        public void ApplyFacing(BattleVector2 facing)
        {
            ApplyFacing(facing, snap: false);
        }

        public bool PlayAnimation(UnitAnimationRequest request)
        {
            return PlayAnimation(request, null);
        }

        public bool PlayAnimation(UnitAnimationRequest request, Sprite fallbackSprite)
        {
            if (request.Key == SpriteAnimationKey.Attack
                && _runtimeAnimationSet != null
                && _runtimeAnimationSet.TryGetAbilityClip(request.AbilityId, out SpriteAnimationClipAsset abilityClip)
                && GetOrAddAnimator().Play(abilityClip, SpriteAnimationKey.Attack, request.Restart))
            {
                return true;
            }

            if (Play(request.Key, request.Restart))
            {
                return true;
            }

            if (fallbackSprite != null)
            {
                GetOrAddSpriteRenderer().sprite = fallbackSprite;
            }

            return false;
        }

        public void ResetForPool()
        {
            _facingMode = UnityUnitFacingMode.Rotate2D;
            _smoothingSettings = UnityCombatViewSmoothingSettings.Immediate;
            _runtimeAnimationSet = null;
            _sortDepth = 0f;
            if (_animator != null)
            {
                _animator.Configure(null);
            }

            if (_smoother != null)
            {
                _smoother.ResetForPool();
            }
        }

        internal void ConfigureForTests(SpriteAnimationSetAsset animationSet)
        {
            _animationSet = animationSet;
        }

        private void ApplyFacing(BattleVector2 facing, bool snap)
        {
            switch (_facingMode)
            {
                case UnityUnitFacingMode.Rotate2D:
                    Quaternion rotation = ToFacingRotation(facing);
                    if (!snap)
                    {
                        GetOrAddSmoother().SetTargetLocalRotation(rotation);
                    }
                    else
                    {
                        transform.localRotation = rotation;
                        _smoother?.SnapLocalRotation(rotation);
                    }

                    break;
                case UnityUnitFacingMode.SideScrollerFlip:
                    ApplySideScrollerFacing(facing);
                    GetOrAddSmoother().SnapLocalRotation(transform.localRotation);
                    break;
                default:
                    throw new System.InvalidOperationException($"Unsupported unit facing mode: {_facingMode}.");
            }
        }

        private bool Play(SpriteAnimationKey key, bool restart)
        {
            return _runtimeAnimationSet != null && GetOrAddAnimator().Play(key, restart);
        }

        private CombatViewTransformSmoother GetOrAddSmoother()
        {
            if (_smoother == null)
            {
                _smoother = GetComponent<CombatViewTransformSmoother>();
                if (_smoother == null)
                {
                    _smoother = gameObject.AddComponent<CombatViewTransformSmoother>();
                }

                _smoother.Configure(_smoothingSettings);
            }

            return _smoother;
        }

        private SpriteFrameAnimator GetOrAddAnimator()
        {
            if (_animator == null)
            {
                _animator = GetComponent<SpriteFrameAnimator>();
                if (_animator == null)
                {
                    _animator = gameObject.AddComponent<SpriteFrameAnimator>();
                }
            }

            return _animator;
        }

        private SpriteRenderer GetOrAddSpriteRenderer()
        {
            if (_spriteRenderer == null)
            {
                _spriteRenderer = GetComponent<SpriteRenderer>();
                if (_spriteRenderer == null)
                {
                    _spriteRenderer = gameObject.AddComponent<SpriteRenderer>();
                }
            }

            return _spriteRenderer;
        }

        private void ApplySideScrollerFacing(BattleVector2 facing)
        {
            transform.localRotation = Quaternion.identity;
            if (facing.X > 0.00001f)
            {
                SetLocalScaleXSign(1f);
            }
            else if (facing.X < -0.00001f)
            {
                SetLocalScaleXSign(-1f);
            }
        }

        private void SetLocalScaleXSign(float sign)
        {
            Vector3 scale = transform.localScale;
            float magnitude = Mathf.Abs(scale.x) <= 0.00001f ? 1f : Mathf.Abs(scale.x);
            transform.localScale = new Vector3(sign * magnitude, scale.y, scale.z);
        }

        private Vector3 ToVector3(BattleVector2 position)
        {
            return new Vector3(position.X, position.Y, _sortDepth);
        }

        private static Quaternion ToFacingRotation(BattleVector2 facing)
        {
            BattleVector2 direction = facing.SqrMagnitude <= 0.00001f ? BattleVector2.Right : facing.Normalized;
            float angleDegrees = Mathf.Atan2(direction.Y, direction.X) * Mathf.Rad2Deg;
            return Quaternion.Euler(0f, 0f, angleDegrees);
        }
    }
}
