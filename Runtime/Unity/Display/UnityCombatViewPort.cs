using System;
using System.Collections.Generic;
using Combat.Core.Battle;
using Combat.Foundation.Diagnostics;
using Combat.Runtime.Display;
using Combat.Runtime.Runner;
using Combat.Unity.Diagnostics;
using Combat.Unity.Pooling;
using UnityEngine;

namespace Combat.Unity.Display
{
    public enum UnityUnitFacingMode
    {
        Rotate2D,
        SideScrollerFlip
    }

    public sealed class UnityCombatViewPort : ICombatViewPort
    {
        private const float UnitSortOrderYPrecision = 100f;
        private const int MinimumSpriteSortingOrder = -32768;
        private const int MaximumSpriteSortingOrder = 32767;

        private static Sprite s_unitSprite;

        private readonly Transform _root;
        private readonly Transform _unitPoolRoot;
        private readonly GameObjectPool _fallbackUnitPool;
        private readonly GameObjectPool _projectilePool;
        private readonly GameObjectPool _feedbackPool;
        private readonly CombatLogger _logger;
        private readonly UnityUnitFacingMode _unitFacingMode;
        private readonly CombatantPresentationCatalogAsset _combatantPresentationCatalog;
        private readonly SpriteAnimationSetAsset _projectileAnimationSet;
        private readonly SpriteAnimationSetAsset _feedbackAnimationSet;
        private readonly UnityCombatViewSmoothingSettings _smoothingSettings;
        private readonly bool _enableRuntimeObserver;
        private readonly Dictionary<int, UnitViewHandle> _unitObjects = new Dictionary<int, UnitViewHandle>();
        private readonly Dictionary<GameObject, GameObjectPool> _unitPrefabPools = new Dictionary<GameObject, GameObjectPool>();
        private readonly Dictionary<int, ProjectileViewHandle> _projectileObjects = new Dictionary<int, ProjectileViewHandle>();
        private IBattleRuntimeSnapshotSource _snapshotSource;

        public UnityCombatViewPort(Transform root)
            : this(root, UnityCombatLogFactory.CreateDefault())
        {
        }

        public UnityCombatViewPort(Transform root, UnityUnitFacingMode unitFacingMode)
            : this(root, UnityCombatLogFactory.CreateDefault(), null, unitFacingMode)
        {
        }

        public UnityCombatViewPort(Transform root, UnityCombatViewSmoothingSettings smoothingSettings)
            : this(root, UnityCombatLogFactory.CreateDefault(), null, UnityUnitFacingMode.Rotate2D, null, null, null, smoothingSettings)
        {
        }

        public UnityCombatViewPort(Transform root, bool enableRuntimeObserver)
            : this(root, UnityCombatLogFactory.CreateDefault(), null, UnityUnitFacingMode.Rotate2D, null, null, null, null, enableRuntimeObserver)
        {
        }

        public UnityCombatViewPort(Transform root, UnityUnitFacingMode unitFacingMode, UnityCombatViewSmoothingSettings smoothingSettings)
            : this(root, UnityCombatLogFactory.CreateDefault(), null, unitFacingMode, null, null, null, smoothingSettings)
        {
        }

        public UnityCombatViewPort(
            Transform root,
            UnityUnitFacingMode unitFacingMode,
            CombatantPresentationCatalogAsset combatantPresentationCatalog,
            SpriteAnimationSetAsset projectileAnimationSet = null,
            SpriteAnimationSetAsset feedbackAnimationSet = null)
            : this(root, UnityCombatLogFactory.CreateDefault(), null, unitFacingMode, combatantPresentationCatalog, projectileAnimationSet, feedbackAnimationSet)
        {
        }

        public UnityCombatViewPort(
            Transform root,
            UnityUnitFacingMode unitFacingMode,
            CombatantPresentationCatalogAsset combatantPresentationCatalog,
            SpriteAnimationSetAsset projectileAnimationSet,
            SpriteAnimationSetAsset feedbackAnimationSet,
            UnityCombatViewSmoothingSettings smoothingSettings)
            : this(root, UnityCombatLogFactory.CreateDefault(), null, unitFacingMode, combatantPresentationCatalog, projectileAnimationSet, feedbackAnimationSet, smoothingSettings)
        {
        }

        public UnityCombatViewPort(Transform root, CombatLogger logger)
            : this(root, logger, null)
        {
        }

        public UnityCombatViewPort(Transform root, CombatLogger logger, UnityUnitFacingMode unitFacingMode)
            : this(root, logger, null, unitFacingMode)
        {
        }

        public UnityCombatViewPort(Transform root, CombatLogger logger, IBattleRuntimeSnapshotSource snapshotSource)
            : this(root, logger, snapshotSource, UnityUnitFacingMode.Rotate2D)
        {
        }

        public UnityCombatViewPort(
            Transform root,
            CombatLogger logger,
            IBattleRuntimeSnapshotSource snapshotSource,
            UnityUnitFacingMode unitFacingMode,
            CombatantPresentationCatalogAsset combatantPresentationCatalog = null,
            SpriteAnimationSetAsset projectileAnimationSet = null,
            SpriteAnimationSetAsset feedbackAnimationSet = null,
            UnityCombatViewSmoothingSettings? smoothingSettings = null,
            bool enableRuntimeObserver = false)
        {
            _root = root != null ? root : throw new ArgumentNullException(nameof(root));
            _logger = logger ?? CombatLogger.Disabled;
            _snapshotSource = snapshotSource;
            _unitFacingMode = ValidateFacingMode(unitFacingMode);
            _combatantPresentationCatalog = combatantPresentationCatalog;
            _projectileAnimationSet = projectileAnimationSet;
            _feedbackAnimationSet = feedbackAnimationSet;
            _smoothingSettings = smoothingSettings ?? UnityCombatViewSmoothingSettings.Default;
            _enableRuntimeObserver = enableRuntimeObserver;

            Transform pooledViewsRoot = CreatePoolRoot(_root, "PooledViews");
            _unitPoolRoot = CreatePoolRoot(pooledViewsRoot, "Units");
            Transform projectilePoolRoot = CreatePoolRoot(pooledViewsRoot, "Projectiles");
            Transform feedbackPoolRoot = CreatePoolRoot(pooledViewsRoot, "Feedback");

            _fallbackUnitPool = new GameObjectPool("UnitViews_Default", _unitPoolRoot, CreateSpriteViewObject, ResetPooledView);
            _projectilePool = new GameObjectPool("ProjectileViews", projectilePoolRoot, CreateSpriteViewObject, ResetPooledView);
            _feedbackPool = new GameObjectPool("FeedbackViews", feedbackPoolRoot, CreateSpriteViewObject, ResetPooledView);
        }

        public void SetRuntimeSnapshotSource(IBattleRuntimeSnapshotSource snapshotSource)
        {
            _snapshotSource = snapshotSource;
#if UNITY_EDITOR
            if (!_enableRuntimeObserver)
            {
                return;
            }

            foreach (UnitViewHandle handle in _unitObjects.Values)
            {
                GameObject unitObject = handle.GameObject;
                if (unitObject == null)
                {
                    continue;
                }

                CombatUnitRuntimeObserver observer = unitObject.GetComponent<CombatUnitRuntimeObserver>();
                if (observer != null)
                {
                    observer.SetSnapshotSource(_snapshotSource);
                }
            }
#endif
        }

        public void CreateUnit(UnitSpawnViewSnapshot snapshot)
        {
            int unitId = snapshot.UnitId.Value;
            if (_unitObjects.ContainsKey(unitId))
            {
                ReleaseUnitObject(unitId);
            }

            GameObject presentationPrefab = ResolvePresentationPrefab(snapshot.DefinitionId);
            GameObjectPool unitPool = GetUnitPool(presentationPrefab);
            Transform unitTransform = null;
            CombatUnitView unitView = null;
            SpriteRenderer spriteRenderer = null;
            GameObject unitObject = unitPool.Get(_root, instance =>
            {
                instance.name = $"Unit_{unitId}";

                unitTransform = instance.transform;
                spriteRenderer = GetOrAddSpriteRenderer(instance);
                bool usesPresentationPrefab = presentationPrefab != null;
                Sprite fallbackSprite = usesPresentationPrefab ? null : UnitSprite;
                unitView = GetOrAddUnitView(instance);
                unitView.Initialize(snapshot, _unitFacingMode, _smoothingSettings, fallbackSprite);

                spriteRenderer.color = snapshot.TeamId.Value == 1 ? Color.cyan : Color.red;
                ApplyUnitSorting(spriteRenderer, snapshot.UnitId, snapshot.Position);

#if UNITY_EDITOR
                if (_enableRuntimeObserver)
                {
                    GetOrAddRuntimeObserver(instance).Bind(snapshot.UnitId, _snapshotSource);
                }
#endif
            });

            _unitObjects[unitId] = new UnitViewHandle(
                unitObject,
                unitTransform,
                unitView,
                spriteRenderer,
                unitPool);
        }

        public void MoveUnit(UnitId unitId, BattleVector2 position)
        {
            if (_unitObjects.TryGetValue(unitId.Value, out UnitViewHandle handle) && handle.GameObject != null)
            {
                handle.View.ApplyMove(position);
                ApplyUnitSorting(handle.SpriteRenderer, unitId, position);
            }
        }

        public void StopUnitMovement(UnitId unitId)
        {
            if (_unitObjects.TryGetValue(unitId.Value, out UnitViewHandle handle) && handle.GameObject != null)
            {
                handle.View.StopMovement();
            }
        }

        public void FaceUnit(UnitId unitId, BattleVector2 facing)
        {
            if (_unitObjects.TryGetValue(unitId.Value, out UnitViewHandle handle) && handle.GameObject != null)
            {
                handle.View.ApplyFacing(facing);
            }
        }

        public void SetUnitVisibility(UnitId unitId, bool isVisible)
        {
            if (_unitObjects.TryGetValue(unitId.Value, out UnitViewHandle handle) && handle.GameObject != null)
            {
                handle.GameObject.SetActive(isVisible);
            }
        }

        public void PlayAction(ActionViewSnapshot snapshot)
        {
            if (_unitObjects.TryGetValue(snapshot.SourceUnitId.Value, out UnitViewHandle handle) && handle.GameObject != null)
            {
                handle.View.PlayAnimation(UnitAnimationRequest.Action(snapshot));
            }
        }

        public void PlayHit(DamageViewSnapshot snapshot)
        {
            UnitId targetUnitId = snapshot.TargetUnitId;
            int amount = snapshot.Amount;
            if (!_unitObjects.TryGetValue(targetUnitId.Value, out UnitViewHandle handle) || handle.GameObject == null)
            {
                return;
            }

            handle.View.PlayAnimation(UnitAnimationRequest.Hit);

            float scale = Mathf.Clamp(0.25f + Mathf.Max(0, amount) * 0.015f, 0.3f, 0.65f);
            CreateFeedback(
                $"DamageFeedback_{targetUnitId.Value}",
                handle.Transform.position + new Vector3(0f, 0.65f, 0f),
                new Vector3(scale, scale, 1f),
                new Color(1f, 0.35f, 0.2f, 1f),
                SpriteAnimationKey.Hit);
        }

        public void PlayHeal(HealingViewSnapshot snapshot)
        {
            UnitId targetUnitId = snapshot.TargetUnitId;
            if (!_unitObjects.TryGetValue(targetUnitId.Value, out UnitViewHandle handle) || handle.GameObject == null)
            {
                return;
            }

            float scale = Mathf.Clamp(0.25f + Mathf.Max(0, snapshot.Amount) * 0.015f, 0.3f, 0.65f);
            CreateFeedback(
                $"HealFeedback_{targetUnitId.Value}",
                handle.Transform.position + new Vector3(0f, 0.9f, 0f),
                new Vector3(scale, scale, 1f),
                new Color(0.25f, 1f, 0.45f, 1f),
                SpriteAnimationKey.StatusApplied);
        }

        public void DestroyUnit(UnitId unitId)
        {
            if (_unitObjects.ContainsKey(unitId.Value))
            {
                ReleaseUnitObject(unitId.Value);
            }
        }

        public void CreateProjectile(ProjectileViewSnapshot snapshot)
        {
            int projectileId = snapshot.ProjectileId.Value;
            if (_projectileObjects.ContainsKey(projectileId))
            {
                ReleaseProjectileObject(projectileId);
            }

            Transform projectileTransform = null;
            CombatViewTransformSmoother projectileSmoother = null;
            SpriteFrameAnimator projectileAnimator = null;
            GameObject projectileObject = _projectilePool.Get(_root, instance =>
            {
                instance.name = $"Projectile_{projectileId}";

                projectileTransform = instance.transform;
                projectileTransform.position = ToVector3(snapshot.Position);
                projectileTransform.localRotation = Quaternion.identity;
                projectileTransform.localScale = new Vector3(0.25f, 0.25f, 1f);
                projectileSmoother = GetOrAddSmoother(instance);
                projectileSmoother.Configure(_smoothingSettings);
                projectileSmoother.SnapTo(projectileTransform.position, projectileTransform.localRotation);

                SpriteRenderer spriteRenderer = GetOrAddSpriteRenderer(instance);
                projectileAnimator = ConfigureAnimator(instance, _projectileAnimationSet);
                if (projectileAnimator == null || !projectileAnimator.Play(SpriteAnimationKey.ProjectileFly, restart: true))
                {
                    spriteRenderer.sprite = UnitSprite;
                }

                spriteRenderer.color = snapshot.TeamId.Value == 1 ? Color.white : Color.yellow;
            });

            _projectileObjects[projectileId] = new ProjectileViewHandle(
                projectileObject,
                projectileTransform,
                projectileSmoother,
                projectileAnimator);
        }

        public void MoveProjectile(ProjectileId projectileId, BattleVector2 position)
        {
            if (_projectileObjects.TryGetValue(projectileId.Value, out ProjectileViewHandle handle) && handle.GameObject != null)
            {
                handle.Smoother.SetTargetPosition(ToVector3(position));
            }
        }

        public void PlayProjectileHit(ProjectileHitViewSnapshot snapshot)
        {
            CreateFeedback(
                $"ProjectileHitFeedback_{snapshot.ProjectileId.Value}",
                ToVector3(snapshot.Position),
                new Vector3(0.35f, 0.35f, 1f),
                new Color(1f, 0.9f, 0.2f, 1f),
                SpriteAnimationKey.ProjectileHit);
        }

        public void DestroyProjectile(ProjectileId projectileId)
        {
            if (_projectileObjects.ContainsKey(projectileId.Value))
            {
                ReleaseProjectileObject(projectileId.Value);
            }
        }

        public void PlayStatusApplied(StatusViewSnapshot snapshot)
        {
            if (!_unitObjects.TryGetValue(snapshot.UnitId.Value, out UnitViewHandle handle) || handle.GameObject == null)
            {
                return;
            }

            CreateFeedback(
                $"StatusAppliedFeedback_{snapshot.UnitId.Value}_{SanitizeName(snapshot.StatusId)}",
                handle.Transform.position + new Vector3(-0.35f, 0.85f, 0f),
                new Vector3(0.3f, 0.3f, 1f),
                StatusColor(snapshot.Polarity, expired: false),
                SpriteAnimationKey.StatusApplied);
        }

        public void PlayStatusExpired(StatusViewSnapshot snapshot)
        {
            if (!_unitObjects.TryGetValue(snapshot.UnitId.Value, out UnitViewHandle handle) || handle.GameObject == null)
            {
                return;
            }

            CreateFeedback(
                $"StatusExpiredFeedback_{snapshot.UnitId.Value}_{SanitizeName(snapshot.StatusId)}",
                handle.Transform.position + new Vector3(0.35f, 0.85f, 0f),
                new Vector3(0.3f, 0.3f, 1f),
                StatusColor(snapshot.Polarity, expired: true),
                SpriteAnimationKey.StatusExpired);
        }

        public void ShowBattleResult(BattleResult result)
        {
            _logger.Debug(CombatLogTags.View, () => $"Battle finished. Winning team: {result.WinningTeamId.Value}");
        }

        private static Vector3 ToVector3(BattleVector2 position)
        {
            return new Vector3(position.X, position.Y, 0f);
        }

        private static UnityUnitFacingMode ValidateFacingMode(UnityUnitFacingMode mode)
        {
            switch (mode)
            {
                case UnityUnitFacingMode.Rotate2D:
                case UnityUnitFacingMode.SideScrollerFlip:
                    return mode;
                default:
                    throw new ArgumentOutOfRangeException(nameof(mode), mode, "Unsupported unit facing mode.");
            }
        }

        private static Transform CreatePoolRoot(Transform parent, string name)
        {
            var poolRoot = new GameObject(name);
            poolRoot.transform.SetParent(parent, worldPositionStays: false);
            poolRoot.SetActive(false);
            return poolRoot.transform;
        }

        private GameObject ResolvePresentationPrefab(string definitionId)
        {
            if (_combatantPresentationCatalog != null
                && _combatantPresentationCatalog.TryGetPrefab(definitionId, out GameObject prefab))
            {
                return prefab;
            }

            return null;
        }

        private GameObjectPool GetUnitPool(GameObject prefab)
        {
            if (prefab == null)
            {
                return _fallbackUnitPool;
            }

            if (_unitPrefabPools.TryGetValue(prefab, out GameObjectPool pool))
            {
                return pool;
            }

            pool = new GameObjectPool(
                $"UnitViews_{SanitizeName(prefab.name)}",
                _unitPoolRoot,
                () => CreatePresentationUnitViewObject(prefab),
                ResetPooledView);
            _unitPrefabPools.Add(prefab, pool);
            return pool;
        }

        private static GameObject CreatePresentationUnitViewObject(GameObject prefab)
        {
            GameObject instance = UnityEngine.Object.Instantiate(prefab);
            instance.SetActive(false);
            return instance;
        }

        private static CombatUnitView GetOrAddUnitView(GameObject gameObject)
        {
            CombatUnitView unitView = gameObject.GetComponent<CombatUnitView>();
            if (unitView == null)
            {
                unitView = gameObject.AddComponent<CombatUnitView>();
            }

            return unitView;
        }

        private void ReleaseUnitObject(int unitId)
        {
            if (!_unitObjects.TryGetValue(unitId, out UnitViewHandle handle))
            {
                return;
            }

            _unitObjects.Remove(unitId);
            GameObject unitObject = handle.GameObject;
#if UNITY_EDITOR
            CombatUnitRuntimeObserver observer = unitObject != null ? unitObject.GetComponent<CombatUnitRuntimeObserver>() : null;
            if (observer != null)
            {
                observer.ClearBinding();
            }
#endif

            handle.Pool.Release(unitObject);
        }

        private void ReleaseProjectileObject(int projectileId)
        {
            if (!_projectileObjects.TryGetValue(projectileId, out ProjectileViewHandle handle))
            {
                return;
            }

            _projectileObjects.Remove(projectileId);
            _projectilePool.Release(handle.GameObject);
        }

        private void CreateFeedback(string name, Vector3 position, Vector3 scale, Color color, SpriteAnimationKey animationKey)
        {
            GameObject feedbackObject = null;
            SpriteFrameAnimator feedbackAnimator = null;
            Action<SpriteAnimationKey> completedHandler = null;
            var releaseImmediately = false;

            feedbackObject = _feedbackPool.Get(_root, instance =>
            {
                instance.name = name;

                Transform feedbackTransform = instance.transform;
                feedbackTransform.position = position;
                feedbackTransform.localRotation = Quaternion.identity;
                feedbackTransform.localScale = scale;

                SpriteRenderer spriteRenderer = GetOrAddSpriteRenderer(instance);
                SpriteFrameAnimator animator = ConfigureAnimator(instance, _feedbackAnimationSet);
                spriteRenderer.color = color;
                if (animator == null)
                {
                    releaseImmediately = true;
                    return;
                }

                feedbackAnimator = animator;
                completedHandler = _ => ReleaseFeedbackObject(instance, animator, completedHandler);
                animator.Completed += completedHandler;
                releaseImmediately = !animator.Play(animationKey, restart: true);
            });

            if (releaseImmediately)
            {
                ReleaseFeedbackObject(feedbackObject, feedbackAnimator, completedHandler);
            }
        }

        private void ReleaseFeedbackObject(GameObject feedbackObject, SpriteFrameAnimator animator, Action<SpriteAnimationKey> completedHandler)
        {
            if (animator != null && completedHandler != null)
            {
                animator.Completed -= completedHandler;
            }

            _feedbackPool.Release(feedbackObject);
        }

        private static GameObject CreateSpriteViewObject()
        {
            var gameObject = new GameObject("PooledView");
            gameObject.SetActive(false);
            gameObject.AddComponent<SpriteRenderer>();
            return gameObject;
        }

        private static void ResetPooledView(GameObject gameObject)
        {
            if (gameObject == null)
            {
                return;
            }

            gameObject.name = "PooledView";

            Transform transform = gameObject.transform;
            transform.localPosition = Vector3.zero;
            transform.localRotation = Quaternion.identity;
            transform.localScale = Vector3.one;

            SpriteRenderer spriteRenderer = gameObject.GetComponent<SpriteRenderer>();
            if (spriteRenderer != null)
            {
                spriteRenderer.sprite = null;
                spriteRenderer.color = Color.white;
                spriteRenderer.sortingOrder = 0;
            }

#if UNITY_EDITOR
            CombatUnitRuntimeObserver observer = gameObject.GetComponent<CombatUnitRuntimeObserver>();
            if (observer != null)
            {
                observer.ClearBinding();
            }
#endif

            SpriteFrameAnimator animator = gameObject.GetComponent<SpriteFrameAnimator>();
            if (animator != null)
            {
                animator.Configure(null);
            }

            CombatUnitView unitView = gameObject.GetComponent<CombatUnitView>();
            if (unitView != null)
            {
                unitView.ResetForPool();
            }

            CombatViewTransformSmoother smoother = gameObject.GetComponent<CombatViewTransformSmoother>();
            if (smoother != null)
            {
                smoother.ResetForPool();
            }
        }

        private static SpriteRenderer GetOrAddSpriteRenderer(GameObject gameObject)
        {
            SpriteRenderer spriteRenderer = gameObject.GetComponent<SpriteRenderer>();
            if (spriteRenderer == null)
            {
                spriteRenderer = gameObject.AddComponent<SpriteRenderer>();
            }

            return spriteRenderer;
        }

        private static void ApplyUnitSorting(SpriteRenderer spriteRenderer, UnitId unitId, BattleVector2 position)
        {
            if (spriteRenderer == null)
            {
                return;
            }

            int yOrder = -Mathf.RoundToInt(position.Y * UnitSortOrderYPrecision);
            spriteRenderer.sortingOrder = Mathf.Clamp(yOrder, MinimumSpriteSortingOrder, MaximumSpriteSortingOrder);
        }

#if UNITY_EDITOR
        private static CombatUnitRuntimeObserver GetOrAddRuntimeObserver(GameObject gameObject)
        {
            CombatUnitRuntimeObserver observer = gameObject.GetComponent<CombatUnitRuntimeObserver>();
            if (observer == null)
            {
                observer = gameObject.AddComponent<CombatUnitRuntimeObserver>();
            }

            return observer;
        }
#endif

        private static SpriteFrameAnimator ConfigureAnimator(GameObject gameObject, SpriteAnimationSetAsset animationSet)
        {
            if (animationSet == null)
            {
                SpriteFrameAnimator existingAnimator = gameObject.GetComponent<SpriteFrameAnimator>();
                if (existingAnimator != null)
                {
                    existingAnimator.Configure(null);
                }

                return null;
            }

            SpriteFrameAnimator animator = gameObject.GetComponent<SpriteFrameAnimator>();
            if (animator == null)
            {
                animator = gameObject.AddComponent<SpriteFrameAnimator>();
            }

            animator.Configure(animationSet);
            return animator;
        }

        private CombatViewTransformSmoother GetOrAddSmoother(GameObject gameObject)
        {
            CombatViewTransformSmoother smoother = gameObject.GetComponent<CombatViewTransformSmoother>();
            if (smoother == null)
            {
                smoother = gameObject.AddComponent<CombatViewTransformSmoother>();
                smoother.Configure(_smoothingSettings);
            }

            return smoother;
        }

        private readonly struct UnitViewHandle
        {
            public UnitViewHandle(
                GameObject gameObject,
                Transform transform,
                CombatUnitView view,
                SpriteRenderer spriteRenderer,
                GameObjectPool pool)
            {
                GameObject = gameObject;
                Transform = transform;
                View = view;
                SpriteRenderer = spriteRenderer;
                Pool = pool;
            }

            public GameObject GameObject { get; }
            public Transform Transform { get; }
            public CombatUnitView View { get; }
            public SpriteRenderer SpriteRenderer { get; }
            public GameObjectPool Pool { get; }
        }

        private readonly struct ProjectileViewHandle
        {
            public ProjectileViewHandle(
                GameObject gameObject,
                Transform transform,
                CombatViewTransformSmoother smoother,
                SpriteFrameAnimator animator)
            {
                GameObject = gameObject;
                Transform = transform;
                Smoother = smoother;
                Animator = animator;
            }

            public GameObject GameObject { get; }
            public Transform Transform { get; }
            public CombatViewTransformSmoother Smoother { get; }
            public SpriteFrameAnimator Animator { get; }
        }

        private static Color StatusColor(StatusPolarity polarity, bool expired)
        {
            switch (polarity)
            {
                case StatusPolarity.Buff:
                    return expired ? new Color(0.15f, 0.5f, 0.25f, 1f) : new Color(0.25f, 1f, 0.45f, 1f);
                case StatusPolarity.Debuff:
                    return expired ? new Color(0.45f, 0.2f, 0.55f, 1f) : new Color(0.85f, 0.25f, 1f, 1f);
                default:
                    return expired ? new Color(0.45f, 0.45f, 0.45f, 1f) : new Color(0.85f, 0.85f, 0.85f, 1f);
            }
        }

        private static string SanitizeName(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return "unknown";
            }

            char[] characters = value.ToCharArray();
            for (var i = 0; i < characters.Length; i++)
            {
                char character = characters[i];
                if (!char.IsLetterOrDigit(character) && character != '_' && character != '-')
                {
                    characters[i] = '_';
                }
            }

            return new string(characters);
        }

        private static Sprite UnitSprite
        {
            get
            {
                if (s_unitSprite == null)
                {
                    s_unitSprite = Sprite.Create(Texture2D.whiteTexture, new Rect(0f, 0f, 1f, 1f), new Vector2(0.5f, 0.5f), 1f);
                }

                return s_unitSprite;
            }
        }
    }
}
