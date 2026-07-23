using Combat.Core.Battle;
using Combat.Runtime.Display;
using Combat.Unity.Authoring;
using Combat.Unity.Display;
using Combat.Unity.Pooling;
using NUnit.Framework;
using UnityEngine;
using BindingFlags = System.Reflection.BindingFlags;
using PropertyInfo = System.Reflection.PropertyInfo;
using Type = System.Type;

namespace Combat.Tests.Unity
{
    public sealed class UnityCombatViewPortTests
    {
        [Test]
        public void CreateMoveDestroy_UsesSceneObjectsWithoutPrefab()
        {
            var root = new GameObject("CombatViewRoot");
            try
            {
                var viewPort = new UnityCombatViewPort(root.transform);
                var unitId = new UnitId(1);

                viewPort.CreateUnit(new UnitSpawnViewSnapshot(unitId, new TeamId(1), "melee", new BattleVector2(2f, 3f)));
                Transform unitTransform = root.transform.Find("Unit_1");
                GameObject unitObject = unitTransform.gameObject;

                Assert.IsNotNull(unitTransform);
                Assert.AreEqual(root.transform, unitTransform.parent);
                Assert.AreEqual(1, CountActiveDirectChildren(root.transform));
                Assert.AreEqual(new Vector3(2f, 3f, -0.000001f), unitTransform.position);
                SpriteRenderer spriteRenderer = unitTransform.GetComponent<SpriteRenderer>();
                Assert.IsNotNull(spriteRenderer);
                Assert.IsNotNull(spriteRenderer.sprite);
                Assert.AreEqual(Color.cyan, spriteRenderer.color);

                viewPort.MoveUnit(unitId, new BattleVector2(4f, 5f));
                CompleteSmoothing(unitTransform);
                Assert.AreEqual(new Vector3(4f, 5f, -0.000001f), unitTransform.position);

                viewPort.DestroyUnit(unitId);
                Assert.AreEqual(0, CountActiveDirectChildren(root.transform));
                Assert.IsFalse(unitObject == null, "Expected pooled object, not destroyed.");
                Assert.IsFalse(unitObject.activeSelf);
                Assert.AreNotEqual(root.transform, unitObject.transform.parent);
                Assert.IsNull(root.transform.Find("Unit_1"));
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void CreateUnit_WithDuplicateUnitId_ReplacesExistingObject()
        {
            var root = new GameObject("CombatViewRoot");
            try
            {
                var viewPort = new UnityCombatViewPort(root.transform);
                var unitId = new UnitId(1);

                viewPort.CreateUnit(new UnitSpawnViewSnapshot(unitId, new TeamId(1), "melee", new BattleVector2(2f, 3f)));
                viewPort.CreateUnit(new UnitSpawnViewSnapshot(unitId, new TeamId(2), "melee", new BattleVector2(4f, 5f)));

                Assert.AreEqual(1, CountActiveDirectChildren(root.transform));
                Transform unitTransform = root.transform.Find("Unit_1");
                Assert.IsNotNull(unitTransform);
                Assert.AreEqual(new Vector3(4f, 5f, -0.000001f), unitTransform.position);
                SpriteRenderer spriteRenderer = unitTransform.GetComponent<SpriteRenderer>();
                Assert.IsNotNull(spriteRenderer);
                Assert.IsNotNull(spriteRenderer.sprite);
                Assert.AreEqual(Color.red, spriteRenderer.color);
                GameObject unitObject = unitTransform.gameObject;

                viewPort.DestroyUnit(unitId);

                Assert.AreEqual(0, CountActiveDirectChildren(root.transform));
                Assert.IsFalse(unitObject == null, "Expected pooled object, not destroyed.");
                Assert.IsFalse(unitObject.activeSelf);
                Assert.AreNotEqual(root.transform, unitObject.transform.parent);
                Assert.IsNull(root.transform.Find("Unit_1"));
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void SetUnitVisibility_HidesAndRestoresTrackedUnitObject()
        {
            var root = new GameObject("CombatViewRoot");
            try
            {
                var viewPort = new UnityCombatViewPort(root.transform);
                var unitId = new UnitId(1);
                viewPort.CreateUnit(new UnitSpawnViewSnapshot(
                    unitId,
                    new TeamId(1),
                    "melee",
                    new BattleVector2(2f, 3f)));
                GameObject unitObject = root.transform.Find("Unit_1").gameObject;

                viewPort.SetUnitVisibility(unitId, false);

                Assert.IsFalse(unitObject.activeSelf);
                Assert.AreEqual(root.transform, unitObject.transform.parent);

                viewPort.SetUnitVisibility(unitId, true);

                Assert.IsTrue(unitObject.activeSelf);
                Assert.AreSame(unitObject, root.transform.Find("Unit_1").gameObject);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void CreateAndMoveUnit_AssignsStableDepthTieBreakerAndYSortOrder()
        {
            var root = new GameObject("CombatViewRoot");
            try
            {
                var viewPort = new UnityCombatViewPort(root.transform);
                var firstUnitId = new UnitId(1);
                var secondUnitId = new UnitId(2);

                viewPort.CreateUnit(new UnitSpawnViewSnapshot(firstUnitId, new TeamId(1), "melee", new BattleVector2(0f, 1f)));
                viewPort.CreateUnit(new UnitSpawnViewSnapshot(secondUnitId, new TeamId(2), "melee", new BattleVector2(0f, 1f)));

                Transform firstTransform = root.transform.Find("Unit_1");
                Transform secondTransform = root.transform.Find("Unit_2");
                SpriteRenderer firstRenderer = firstTransform.GetComponent<SpriteRenderer>();
                SpriteRenderer secondRenderer = secondTransform.GetComponent<SpriteRenderer>();

                Assert.AreEqual(firstRenderer.sortingOrder, secondRenderer.sortingOrder);
                Assert.Less(secondTransform.position.z, firstTransform.position.z);

                viewPort.MoveUnit(firstUnitId, new BattleVector2(0f, -1f));

                Assert.Greater(firstRenderer.sortingOrder, secondRenderer.sortingOrder);
                CompleteSmoothing(firstTransform);
                Assert.That(firstTransform.position.z, Is.EqualTo(-0.000001f).Within(0.0000001f));
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void MoveUnit_DefaultSmoothingInterpolatesToTargetPosition()
        {
            var root = new GameObject("CombatViewRoot");
            try
            {
                var viewPort = new UnityCombatViewPort(root.transform);
                var unitId = new UnitId(1);
                viewPort.CreateUnit(new UnitSpawnViewSnapshot(unitId, new TeamId(1), "melee", new BattleVector2(0f, 0f)));
                Transform unitTransform = root.transform.Find("Unit_1");

                viewPort.MoveUnit(unitId, new BattleVector2(10f, 0f));

                Assert.AreEqual(new Vector3(0f, 0f, -0.000001f), unitTransform.position);
                Component smoother = unitTransform.GetComponent("CombatViewTransformSmoother");
                Assert.IsNotNull(smoother);

                TickSmootherForTests(smoother, 0.06f);
                Assert.That(unitTransform.position.x, Is.GreaterThan(0f).And.LessThan(10f));

                TickSmootherForTests(smoother, 1f);
                Assert.That(unitTransform.position.x, Is.EqualTo(10f).Within(0.001f));
                Assert.That(unitTransform.position.y, Is.EqualTo(0f).Within(0.001f));
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void StopUnitMovement_StopsPendingUnitMoveSmoothing()
        {
            var root = new GameObject("CombatViewRoot");
            try
            {
                var viewPort = new UnityCombatViewPort(root.transform);
                var unitId = new UnitId(1);
                viewPort.CreateUnit(new UnitSpawnViewSnapshot(unitId, new TeamId(1), "melee", new BattleVector2(0f, 0f)));
                Transform sourceTransform = root.transform.Find("Unit_1");
                Component smoother = sourceTransform.GetComponent("CombatViewTransformSmoother");
                Assert.IsNotNull(smoother);

                viewPort.MoveUnit(unitId, new BattleVector2(1f, 0f));
                TickSmootherForTests(smoother, 0.02f);
                Vector3 stoppedPosition = sourceTransform.position;

                viewPort.StopUnitMovement(unitId);
                TickSmootherForTests(smoother, 0.06f);

                Assert.That(sourceTransform.position.x, Is.EqualTo(stoppedPosition.x).Within(0.001f));
                Assert.That(sourceTransform.position.y, Is.EqualTo(stoppedPosition.y).Within(0.001f));
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void MoveProjectile_DefaultSmoothingInterpolatesToTargetPosition()
        {
            var root = new GameObject("CombatViewRoot");
            try
            {
                var viewPort = new UnityCombatViewPort(root.transform);
                var projectileId = new ProjectileId(3);
                viewPort.CreateProjectile(new ProjectileViewSnapshot(projectileId, new TeamId(1), new UnitId(7), new BattleVector2(1f, 2f)));
                Transform projectileTransform = root.transform.Find("Projectile_3");

                viewPort.MoveProjectile(projectileId, new BattleVector2(5f, 2f));

                Assert.AreEqual(new Vector3(1f, 2f, 0f), projectileTransform.position);
                Component smoother = projectileTransform.GetComponent("CombatViewTransformSmoother");
                Assert.IsNotNull(smoother);

                TickSmootherForTests(smoother, 0.06f);
                Assert.That(projectileTransform.position.x, Is.GreaterThan(1f).And.LessThan(5f));

                TickSmootherForTests(smoother, 1f);
                Assert.That(projectileTransform.position.x, Is.EqualTo(5f).Within(0.001f));
                Assert.That(projectileTransform.position.y, Is.EqualTo(2f).Within(0.001f));
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void FaceUnit_Rotate2DDefaultSmoothingInterpolatesRotation()
        {
            var root = new GameObject("CombatViewRoot");
            try
            {
                var viewPort = new UnityCombatViewPort(root.transform);
                var unitId = new UnitId(1);
                viewPort.CreateUnit(new UnitSpawnViewSnapshot(
                    unitId,
                    new TeamId(1),
                    "melee",
                    new BattleVector2(0f, 0f),
                    new BattleVector2(1f, 0f)));
                Transform unitTransform = root.transform.Find("Unit_1");

                viewPort.FaceUnit(unitId, new BattleVector2(0f, 1f));

                Assert.That(NormalizedAngle(unitTransform.localEulerAngles.z), Is.EqualTo(0f).Within(0.001f));
                Component smoother = unitTransform.GetComponent("CombatViewTransformSmoother");
                Assert.IsNotNull(smoother);

                TickSmootherForTests(smoother, 0.04f);
                float midAngle = NormalizedAngle(unitTransform.localEulerAngles.z);
                Assert.That(midAngle, Is.GreaterThan(0f).And.LessThan(90f));

                TickSmootherForTests(smoother, 1f);
                Assert.That(NormalizedAngle(unitTransform.localEulerAngles.z), Is.EqualTo(90f).Within(0.001f));
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void CreateUnit_DoesNotAttachRuntimeObserverByDefault()
        {
            var root = new GameObject("CombatViewRoot");
            try
            {
                var viewPort = new UnityCombatViewPort(root.transform);

                viewPort.CreateUnit(new UnitSpawnViewSnapshot(new UnitId(3), new TeamId(1), "melee", new BattleVector2(2f, 3f)));

                CombatUnitRuntimeObserver observer = root.transform.Find("Unit_3").GetComponent<CombatUnitRuntimeObserver>();
                Assert.IsNull(observer);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void CreateUnit_WithRuntimeObserverEnabledAttachesRuntimeObserverAndBindsUnitId()
        {
            var root = new GameObject("CombatViewRoot");
            try
            {
                var viewPort = new UnityCombatViewPort(root.transform, enableRuntimeObserver: true);

                viewPort.CreateUnit(new UnitSpawnViewSnapshot(new UnitId(3), new TeamId(1), "melee", new BattleVector2(2f, 3f)));

                CombatUnitRuntimeObserver observer = root.transform.Find("Unit_3").GetComponent<CombatUnitRuntimeObserver>();
                Assert.IsNotNull(observer);
                Assert.IsTrue(observer.IsBoundForTests);
                Assert.AreEqual(3, observer.UnitIdForTests);
                Assert.IsFalse(observer.HasSnapshotForTests);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void CombatantPresentationCatalog_CreateUnitUsesPrefabForDefinitionIdAndKeepsPoolsSeparate()
        {
            var root = new GameObject("CombatViewRoot");
            GameObject magePrefab = null;
            GameObject meleePrefab = null;
            CombatantConfigAsset mage = null;
            CombatantConfigAsset melee = null;
            CombatantPresentationCatalogAsset catalog = null;
            try
            {
                magePrefab = CreatePresentationPrefab("MageVisual");
                meleePrefab = CreatePresentationPrefab("MeleeVisual");
                mage = CreateCombatantDefinition("DefaultMagie");
                melee = CreateCombatantDefinition("DefaultMelee");
                catalog = CreateCatalog(
                    new CombatantPresentationCatalogEntry(mage, magePrefab),
                    new CombatantPresentationCatalogEntry(melee, meleePrefab));

                var viewPort = new UnityCombatViewPort(root.transform, UnityUnitFacingMode.SideScrollerFlip, catalog);

                viewPort.CreateUnit(new UnitSpawnViewSnapshot(new UnitId(1), new TeamId(1), "DefaultMagie", new BattleVector2(0f, 0f)));
                Transform mageTransform = root.transform.Find("Unit_1");

                Assert.IsNotNull(mageTransform);
                Assert.IsNotNull(mageTransform.GetComponent<CombatUnitView>());
                Assert.IsNotNull(mageTransform.Find("MageVisual"));
                Assert.IsNull(mageTransform.Find("MeleeVisual"));

                viewPort.DestroyUnit(new UnitId(1));
                viewPort.CreateUnit(new UnitSpawnViewSnapshot(new UnitId(2), new TeamId(1), "DefaultMelee", new BattleVector2(1f, 0f)));
                Transform meleeTransform = root.transform.Find("Unit_2");

                Assert.IsNotNull(meleeTransform);
                Assert.IsNotNull(meleeTransform.GetComponent<CombatUnitView>());
                Assert.IsNotNull(meleeTransform.Find("MeleeVisual"));
                Assert.IsNull(meleeTransform.Find("MageVisual"));
            }
            finally
            {
                Object.DestroyImmediate(root);
                Object.DestroyImmediate(magePrefab);
                Object.DestroyImmediate(meleePrefab);
                Object.DestroyImmediate(mage);
                Object.DestroyImmediate(melee);
                Object.DestroyImmediate(catalog);
            }
        }

        [Test]
        public void UnitAnimationSet_PlaysIdleMoveAttackAndHitFromDisplayCommands()
        {
            var root = new GameObject("CombatViewRoot");
            SpriteAnimationClipAsset idleClip = null;
            SpriteAnimationClipAsset moveClip = null;
            SpriteAnimationClipAsset attackClip = null;
            SpriteAnimationClipAsset hitClip = null;
            SpriteAnimationSetAsset animationSet = null;
            GameObject meleePrefab = null;
            CombatantConfigAsset melee = null;
            CombatantPresentationCatalogAsset catalog = null;
            try
            {
                Sprite idleFrame = CreateSprite();
                Sprite moveFrame = CreateSprite();
                Sprite attackFrame = CreateSprite();
                Sprite hitFrame = CreateSprite();
                idleClip = CreateClip(8f, loop: true, SpriteAnimationKey.None, idleFrame);
                moveClip = CreateClip(8f, loop: true, SpriteAnimationKey.None, moveFrame);
                attackClip = CreateClip(12f, loop: false, SpriteAnimationKey.Idle, attackFrame);
                hitClip = CreateClip(8f, loop: false, SpriteAnimationKey.Idle, hitFrame);
                animationSet = CreateSet(
                    new SpriteAnimationEntry(SpriteAnimationKey.Idle, idleClip),
                    new SpriteAnimationEntry(SpriteAnimationKey.Move, moveClip),
                    new SpriteAnimationEntry(SpriteAnimationKey.Attack, attackClip),
                    new SpriteAnimationEntry(SpriteAnimationKey.Hit, hitClip));
                meleePrefab = CreatePresentationPrefab("MeleeVisual", animationSet);
                melee = CreateCombatantDefinition("melee");
                catalog = CreateCatalog(new CombatantPresentationCatalogEntry(melee, meleePrefab));

                var viewPort = new UnityCombatViewPort(root.transform, UnityUnitFacingMode.SideScrollerFlip, catalog);
                var sourceUnitId = new UnitId(1);
                var targetUnitId = new UnitId(3);

                viewPort.CreateUnit(new UnitSpawnViewSnapshot(sourceUnitId, new TeamId(1), "melee", new BattleVector2(-1f, 0f)));
                viewPort.CreateUnit(new UnitSpawnViewSnapshot(targetUnitId, new TeamId(2), "melee", new BattleVector2(0f, 0f)));
                Transform sourceTransform = root.transform.Find("Unit_1");
                Transform unitTransform = root.transform.Find("Unit_3");
                SpriteFrameAnimator sourceAnimator = sourceTransform.GetComponent<SpriteFrameAnimator>();
                SpriteRenderer sourceSpriteRenderer = sourceTransform.GetComponent<SpriteRenderer>();
                SpriteFrameAnimator animator = unitTransform.GetComponent<SpriteFrameAnimator>();
                SpriteRenderer spriteRenderer = unitTransform.GetComponent<SpriteRenderer>();

                Assert.IsNotNull(sourceAnimator);
                Assert.IsNotNull(animator);
                Assert.AreEqual(SpriteAnimationKey.Idle, animator.CurrentKeyForTests);
                Assert.AreSame(idleFrame, spriteRenderer.sprite);

                viewPort.MoveUnit(targetUnitId, new BattleVector2(1f, 0f));
                Assert.AreEqual(SpriteAnimationKey.Move, animator.CurrentKeyForTests);
                Assert.AreSame(moveFrame, spriteRenderer.sprite);

                viewPort.PlayAction(new ActionViewSnapshot(
                    sourceUnitId,
                    targetUnitId,
                    "basic-slash",
                    BattleEffectSourceKind.BasicAbility));
                Assert.AreEqual(SpriteAnimationKey.Attack, sourceAnimator.CurrentKeyForTests);
                Assert.AreSame(attackFrame, sourceSpriteRenderer.sprite);

                viewPort.PlayHit(new DamageViewSnapshot(
                    sourceUnitId,
                    targetUnitId,
                    6,
                    BattleEffectSourceKind.BasicAbility,
                    true,
                    BattleEffectType.Damage,
                    "basic-attack",
                    null,
                    default,
                    new string[0]));
                Assert.AreEqual(SpriteAnimationKey.Hit, animator.CurrentKeyForTests);
                Assert.AreSame(hitFrame, spriteRenderer.sprite);
            }
            finally
            {
                Object.DestroyImmediate(root);
                Object.DestroyImmediate(idleClip);
                Object.DestroyImmediate(moveClip);
                Object.DestroyImmediate(attackClip);
                Object.DestroyImmediate(hitClip);
                Object.DestroyImmediate(animationSet);
                Object.DestroyImmediate(meleePrefab);
                Object.DestroyImmediate(melee);
                Object.DestroyImmediate(catalog);
            }
        }

        [Test]
        public void UnitAnimationRequest_ActionUsesAttackClipAsCurrentFallback()
        {
            var request = UnitAnimationRequest.Action(new ActionViewSnapshot(
                new UnitId(1),
                new UnitId(2),
                "fireball",
                BattleEffectSourceKind.Ability));

            Assert.AreEqual(SpriteAnimationKey.Attack, request.Key);
            Assert.IsTrue(request.Restart);
            Assert.AreEqual("fireball", request.AbilityId);
        }

        [Test]
        public void UnitAnimationSet_PlayActionUsesAbilitySpecificClipBeforeGenericAttack()
        {
            var root = new GameObject("CombatViewRoot");
            SpriteAnimationClipAsset idleClip = null;
            SpriteAnimationClipAsset genericAttackClip = null;
            SpriteAnimationClipAsset fireballClip = null;
            SpriteAnimationSetAsset animationSet = null;
            GameObject magePrefab = null;
            CombatantConfigAsset mage = null;
            CombatantPresentationCatalogAsset catalog = null;
            try
            {
                Sprite idleFrame = CreateSprite();
                Sprite genericAttackFrame = CreateSprite();
                Sprite fireballFrame = CreateSprite();
                idleClip = CreateClip(8f, loop: true, SpriteAnimationKey.None, idleFrame);
                genericAttackClip = CreateClip(12f, loop: false, SpriteAnimationKey.Idle, genericAttackFrame);
                fireballClip = CreateClip(12f, loop: false, SpriteAnimationKey.Idle, fireballFrame);
                animationSet = CreateSet(
                    new[]
                    {
                        new SpriteAnimationEntry(SpriteAnimationKey.Idle, idleClip),
                        new SpriteAnimationEntry(SpriteAnimationKey.Attack, genericAttackClip),
                    },
                    new[]
                    {
                        new SpriteAbilityAnimationEntry("fireball", fireballClip),
                    });
                magePrefab = CreatePresentationPrefab("MageVisual", animationSet);
                mage = CreateCombatantDefinition("mage");
                catalog = CreateCatalog(new CombatantPresentationCatalogEntry(mage, magePrefab));

                var viewPort = new UnityCombatViewPort(root.transform, UnityUnitFacingMode.SideScrollerFlip, catalog);
                var sourceUnitId = new UnitId(1);
                var targetUnitId = new UnitId(2);

                viewPort.CreateUnit(new UnitSpawnViewSnapshot(sourceUnitId, new TeamId(1), "mage", new BattleVector2(0f, 0f)));
                viewPort.CreateUnit(new UnitSpawnViewSnapshot(targetUnitId, new TeamId(2), "dummy", new BattleVector2(1f, 0f)));
                Transform sourceTransform = root.transform.Find("Unit_1");
                SpriteFrameAnimator sourceAnimator = sourceTransform.GetComponent<SpriteFrameAnimator>();
                SpriteRenderer sourceRenderer = sourceTransform.GetComponent<SpriteRenderer>();

                viewPort.PlayAction(new ActionViewSnapshot(
                    sourceUnitId,
                    targetUnitId,
                    "fireball",
                    BattleEffectSourceKind.Ability));

                Assert.AreEqual(SpriteAnimationKey.Attack, sourceAnimator.CurrentKeyForTests);
                Assert.AreSame(fireballFrame, sourceRenderer.sprite);
            }
            finally
            {
                Object.DestroyImmediate(root);
                Object.DestroyImmediate(idleClip);
                Object.DestroyImmediate(genericAttackClip);
                Object.DestroyImmediate(fireballClip);
                Object.DestroyImmediate(animationSet);
                Object.DestroyImmediate(magePrefab);
                Object.DestroyImmediate(mage);
                Object.DestroyImmediate(catalog);
            }
        }

        [Test]
        public void UnitAnimationSet_PlayActionFallsBackToGenericAttackWhenAbilityClipMissing()
        {
            var root = new GameObject("CombatViewRoot");
            SpriteAnimationClipAsset idleClip = null;
            SpriteAnimationClipAsset genericAttackClip = null;
            SpriteAnimationClipAsset fireballClip = null;
            SpriteAnimationSetAsset animationSet = null;
            GameObject magePrefab = null;
            CombatantConfigAsset mage = null;
            CombatantPresentationCatalogAsset catalog = null;
            try
            {
                Sprite idleFrame = CreateSprite();
                Sprite genericAttackFrame = CreateSprite();
                Sprite fireballFrame = CreateSprite();
                idleClip = CreateClip(8f, loop: true, SpriteAnimationKey.None, idleFrame);
                genericAttackClip = CreateClip(12f, loop: false, SpriteAnimationKey.Idle, genericAttackFrame);
                fireballClip = CreateClip(12f, loop: false, SpriteAnimationKey.Idle, fireballFrame);
                animationSet = CreateSet(
                    new[]
                    {
                        new SpriteAnimationEntry(SpriteAnimationKey.Idle, idleClip),
                        new SpriteAnimationEntry(SpriteAnimationKey.Attack, genericAttackClip),
                    },
                    new[]
                    {
                        new SpriteAbilityAnimationEntry("fireball", fireballClip),
                    });
                magePrefab = CreatePresentationPrefab("MageVisual", animationSet);
                mage = CreateCombatantDefinition("mage");
                catalog = CreateCatalog(new CombatantPresentationCatalogEntry(mage, magePrefab));

                var viewPort = new UnityCombatViewPort(root.transform, UnityUnitFacingMode.SideScrollerFlip, catalog);
                var sourceUnitId = new UnitId(1);
                var targetUnitId = new UnitId(2);

                viewPort.CreateUnit(new UnitSpawnViewSnapshot(sourceUnitId, new TeamId(1), "mage", new BattleVector2(0f, 0f)));
                viewPort.CreateUnit(new UnitSpawnViewSnapshot(targetUnitId, new TeamId(2), "dummy", new BattleVector2(1f, 0f)));
                Transform sourceTransform = root.transform.Find("Unit_1");
                SpriteFrameAnimator sourceAnimator = sourceTransform.GetComponent<SpriteFrameAnimator>();
                SpriteRenderer sourceRenderer = sourceTransform.GetComponent<SpriteRenderer>();

                viewPort.PlayAction(new ActionViewSnapshot(
                    sourceUnitId,
                    targetUnitId,
                    "icebolt",
                    BattleEffectSourceKind.Ability));

                Assert.AreEqual(SpriteAnimationKey.Attack, sourceAnimator.CurrentKeyForTests);
                Assert.AreSame(genericAttackFrame, sourceRenderer.sprite);
            }
            finally
            {
                Object.DestroyImmediate(root);
                Object.DestroyImmediate(idleClip);
                Object.DestroyImmediate(genericAttackClip);
                Object.DestroyImmediate(fireballClip);
                Object.DestroyImmediate(animationSet);
                Object.DestroyImmediate(magePrefab);
                Object.DestroyImmediate(mage);
                Object.DestroyImmediate(catalog);
            }
        }

        [Test]
        public void ProjectileAnimationSet_PlaysFlyOnSpawn()
        {
            var root = new GameObject("CombatViewRoot");
            SpriteAnimationClipAsset flyClip = null;
            SpriteAnimationSetAsset animationSet = null;
            try
            {
                Sprite flyFrame = CreateSprite();
                flyClip = CreateClip(12f, loop: true, SpriteAnimationKey.None, flyFrame);
                animationSet = CreateSet(new SpriteAnimationEntry(SpriteAnimationKey.ProjectileFly, flyClip));

                var viewPort = new UnityCombatViewPort(
                    root.transform,
                    UnityUnitFacingMode.SideScrollerFlip,
                    combatantPresentationCatalog: null,
                    projectileAnimationSet: animationSet);

                viewPort.CreateProjectile(new ProjectileViewSnapshot(new ProjectileId(5), new TeamId(1), new UnitId(3), new BattleVector2(2f, 0f)));
                Transform projectileTransform = root.transform.Find("Projectile_5");
                SpriteFrameAnimator animator = projectileTransform.GetComponent<SpriteFrameAnimator>();

                Assert.IsNotNull(animator);
                Assert.AreEqual(SpriteAnimationKey.ProjectileFly, animator.CurrentKeyForTests);
                Assert.AreSame(flyFrame, projectileTransform.GetComponent<SpriteRenderer>().sprite);
            }
            finally
            {
                Object.DestroyImmediate(root);
                Object.DestroyImmediate(flyClip);
                Object.DestroyImmediate(animationSet);
            }
        }

        [Test]
        public void CreateAndFaceUnit_AppliesFacingRotation()
        {
            var root = new GameObject("CombatViewRoot");
            try
            {
                var viewPort = new UnityCombatViewPort(root.transform);
                var unitId = new UnitId(1);

                viewPort.CreateUnit(new UnitSpawnViewSnapshot(
                    unitId,
                    new TeamId(1),
                    "melee",
                    new BattleVector2(0f, 0f),
                    new BattleVector2(0f, 1f)));
                Transform unitTransform = root.transform.Find("Unit_1");

                Assert.IsNotNull(unitTransform);
                Assert.That(NormalizedAngle(unitTransform.localEulerAngles.z), Is.EqualTo(90f).Within(0.001f));

                viewPort.FaceUnit(unitId, new BattleVector2(-1f, 0f));

                CompleteSmoothing(unitTransform);
                Assert.That(NormalizedAngle(unitTransform.localEulerAngles.z), Is.EqualTo(180f).Within(0.001f));
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void SideScrollerFacingMode_FlipsHorizontallyAndIgnoresVerticalFacing()
        {
            var root = new GameObject("CombatViewRoot");
            try
            {
                var viewPort = new UnityCombatViewPort(root.transform, UnityUnitFacingMode.SideScrollerFlip);
                var unitId = new UnitId(1);

                viewPort.CreateUnit(new UnitSpawnViewSnapshot(
                    unitId,
                    new TeamId(1),
                    "melee",
                    new BattleVector2(0f, 0f),
                    new BattleVector2(0f, 1f)));
                Transform unitTransform = root.transform.Find("Unit_1");

                Assert.IsNotNull(unitTransform);
                Assert.That(NormalizedAngle(unitTransform.localEulerAngles.z), Is.EqualTo(0f).Within(0.001f));
                Assert.AreEqual(Vector3.one, unitTransform.localScale);

                viewPort.FaceUnit(unitId, new BattleVector2(-1f, 0f));
                Assert.That(NormalizedAngle(unitTransform.localEulerAngles.z), Is.EqualTo(0f).Within(0.001f));
                Assert.AreEqual(new Vector3(-1f, 1f, 1f), unitTransform.localScale);

                viewPort.FaceUnit(unitId, new BattleVector2(0f, -1f));
                Assert.AreEqual(new Vector3(-1f, 1f, 1f), unitTransform.localScale);

                viewPort.FaceUnit(unitId, new BattleVector2(1f, 0f));
                Assert.AreEqual(Vector3.one, unitTransform.localScale);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void CreateUnitAndProjectile_UnderRotatedRoot_UseLocalIdentityRotation()
        {
            var root = new GameObject("CombatViewRoot");
            try
            {
                root.transform.rotation = Quaternion.Euler(0f, 0f, 37f);

                var viewPort = new UnityCombatViewPort(root.transform);

                viewPort.CreateUnit(new UnitSpawnViewSnapshot(new UnitId(1), new TeamId(1), "melee", new BattleVector2(2f, 3f)));
                viewPort.CreateProjectile(new ProjectileViewSnapshot(new ProjectileId(3), new TeamId(1), new UnitId(1), new BattleVector2(4f, 5f)));

                Transform unitTransform = root.transform.Find("Unit_1");
                Transform projectileTransform = root.transform.Find("Projectile_3");

                Assert.IsNotNull(unitTransform);
                Assert.IsNotNull(projectileTransform);
                Assert.AreEqual(Quaternion.identity, unitTransform.localRotation);
                Assert.AreEqual(Quaternion.identity, projectileTransform.localRotation);
                Assert.AreEqual(2, CountActiveDirectChildren(root.transform));
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void DestroyThenCreateUnit_ReusesPooledObject()
        {
            var root = new GameObject("CombatViewRoot");
            try
            {
                var viewPort = new UnityCombatViewPort(root.transform);

                viewPort.CreateUnit(new UnitSpawnViewSnapshot(new UnitId(1), new TeamId(1), "melee", new BattleVector2(2f, 3f)));
                GameObject firstObject = root.transform.Find("Unit_1").gameObject;

                viewPort.DestroyUnit(new UnitId(1));
                viewPort.CreateUnit(new UnitSpawnViewSnapshot(new UnitId(2), new TeamId(2), "archer", new BattleVector2(6f, 7f)));

                Transform secondTransform = root.transform.Find("Unit_2");
                Assert.IsNotNull(secondTransform);
                Assert.AreSame(firstObject, secondTransform.gameObject);
                Assert.IsTrue(secondTransform.gameObject.activeSelf);
                Assert.AreEqual(root.transform, secondTransform.parent);
                Assert.AreEqual(new Vector3(6f, 7f, -0.000002f), secondTransform.position);
                Assert.AreEqual(Vector3.one, secondTransform.localScale);
                Assert.AreEqual(Color.red, secondTransform.GetComponent<SpriteRenderer>().color);

                viewPort.MoveUnit(new UnitId(1), new BattleVector2(8f, 9f));
                Assert.AreEqual(new Vector3(6f, 7f, -0.000002f), secondTransform.position);

                viewPort.MoveUnit(new UnitId(2), new BattleVector2(8f, 9f));
                CompleteSmoothing(secondTransform);
                Assert.AreEqual(new Vector3(8f, 9f, -0.000002f), secondTransform.position);
                Assert.AreEqual(1, CountActiveDirectChildren(root.transform));
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void DestroyThenCreateUnit_WithRuntimeObserverEnabledUsesNewUnitId()
        {
            var root = new GameObject("CombatViewRoot");
            try
            {
                var viewPort = new UnityCombatViewPort(root.transform, enableRuntimeObserver: true);

                viewPort.CreateUnit(new UnitSpawnViewSnapshot(new UnitId(1), new TeamId(1), "first", new BattleVector2(0f, 0f)));
                GameObject firstObject = root.transform.Find("Unit_1").gameObject;

                viewPort.DestroyUnit(new UnitId(1));
                viewPort.CreateUnit(new UnitSpawnViewSnapshot(new UnitId(2), new TeamId(1), "second", new BattleVector2(1f, 0f)));

                Assert.AreSame(firstObject, root.transform.Find("Unit_2").gameObject);
                CombatUnitRuntimeObserver observer = firstObject.GetComponent<CombatUnitRuntimeObserver>();
                Assert.IsNotNull(observer);
                Assert.IsTrue(observer.IsBoundForTests);
                Assert.AreEqual(2, observer.UnitIdForTests);
                Assert.IsFalse(observer.HasSnapshotForTests);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void CreateMoveDestroyProjectile_UsesSceneObjectsWithoutPrefab()
        {
            var root = new GameObject("CombatViewRoot");
            try
            {
                var viewPort = new UnityCombatViewPort(root.transform);
                var projectileId = new ProjectileId(3);

                viewPort.CreateProjectile(new ProjectileViewSnapshot(projectileId, new TeamId(1), new UnitId(7), new BattleVector2(1f, 2f)));
                Transform projectileTransform = root.transform.Find("Projectile_3");
                GameObject projectileObject = projectileTransform.gameObject;

                Assert.IsNotNull(projectileTransform);
                Assert.AreEqual(root.transform, projectileTransform.parent);
                Assert.AreEqual(1, CountActiveDirectChildren(root.transform));
                Assert.AreEqual(new Vector3(1f, 2f, 0f), projectileTransform.position);
                Assert.AreEqual(new Vector3(0.25f, 0.25f, 1f), projectileTransform.localScale);
                SpriteRenderer spriteRenderer = projectileTransform.GetComponent<SpriteRenderer>();
                Assert.IsNotNull(spriteRenderer);
                Assert.IsNotNull(spriteRenderer.sprite);
                Assert.AreEqual(Color.white, spriteRenderer.color);

                viewPort.MoveProjectile(projectileId, new BattleVector2(2f, 2f));
                CompleteSmoothing(projectileTransform);
                Assert.AreEqual(new Vector3(2f, 2f, 0f), projectileTransform.position);

                viewPort.DestroyProjectile(projectileId);
                Assert.AreEqual(0, CountActiveDirectChildren(root.transform));
                Assert.IsFalse(projectileObject == null, "Expected pooled object, not destroyed.");
                Assert.IsFalse(projectileObject.activeSelf);
                Assert.AreNotEqual(root.transform, projectileObject.transform.parent);
                Assert.IsNull(root.transform.Find("Projectile_3"));
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void CreateProjectile_WithDuplicateProjectileId_ReplacesExistingObject()
        {
            var root = new GameObject("CombatViewRoot");
            try
            {
                var viewPort = new UnityCombatViewPort(root.transform);
                var projectileId = new ProjectileId(3);

                viewPort.CreateProjectile(new ProjectileViewSnapshot(projectileId, new TeamId(1), new UnitId(7), new BattleVector2(1f, 2f)));
                viewPort.CreateProjectile(new ProjectileViewSnapshot(projectileId, new TeamId(2), new UnitId(8), new BattleVector2(3f, 4f)));

                Assert.AreEqual(1, CountActiveDirectChildren(root.transform));
                Transform projectileTransform = root.transform.Find("Projectile_3");
                Assert.IsNotNull(projectileTransform);
                Assert.AreEqual(new Vector3(3f, 4f, 0f), projectileTransform.position);
                Assert.AreEqual(new Vector3(0.25f, 0.25f, 1f), projectileTransform.localScale);
                SpriteRenderer spriteRenderer = projectileTransform.GetComponent<SpriteRenderer>();
                Assert.IsNotNull(spriteRenderer);
                Assert.IsNotNull(spriteRenderer.sprite);
                Assert.AreEqual(Color.yellow, spriteRenderer.color);
                GameObject projectileObject = projectileTransform.gameObject;

                viewPort.DestroyProjectile(projectileId);

                Assert.AreEqual(0, CountActiveDirectChildren(root.transform));
                Assert.IsFalse(projectileObject == null, "Expected pooled object, not destroyed.");
                Assert.IsFalse(projectileObject.activeSelf);
                Assert.AreNotEqual(root.transform, projectileObject.transform.parent);
                Assert.IsNull(root.transform.Find("Projectile_3"));
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void DestroyThenCreateProjectile_ReusesPooledObject()
        {
            var root = new GameObject("CombatViewRoot");
            try
            {
                var viewPort = new UnityCombatViewPort(root.transform);

                viewPort.CreateProjectile(new ProjectileViewSnapshot(new ProjectileId(3), new TeamId(1), new UnitId(7), new BattleVector2(1f, 2f)));
                GameObject firstObject = root.transform.Find("Projectile_3").gameObject;

                viewPort.DestroyProjectile(new ProjectileId(3));
                viewPort.CreateProjectile(new ProjectileViewSnapshot(new ProjectileId(4), new TeamId(2), new UnitId(8), new BattleVector2(3f, 4f)));

                Transform secondTransform = root.transform.Find("Projectile_4");
                Assert.IsNotNull(secondTransform);
                Assert.AreSame(firstObject, secondTransform.gameObject);
                Assert.IsTrue(secondTransform.gameObject.activeSelf);
                Assert.AreEqual(root.transform, secondTransform.parent);
                Assert.AreEqual(new Vector3(3f, 4f, 0f), secondTransform.position);
                Assert.AreEqual(new Vector3(0.25f, 0.25f, 1f), secondTransform.localScale);
                Assert.AreEqual(Color.yellow, secondTransform.GetComponent<SpriteRenderer>().color);

                viewPort.MoveProjectile(new ProjectileId(3), new BattleVector2(5f, 6f));
                Assert.AreEqual(new Vector3(3f, 4f, 0f), secondTransform.position);

                viewPort.MoveProjectile(new ProjectileId(4), new BattleVector2(5f, 6f));
                CompleteSmoothing(secondTransform);
                Assert.AreEqual(new Vector3(5f, 6f, 0f), secondTransform.position);
                Assert.AreEqual(1, CountActiveDirectChildren(root.transform));
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void PlayHit_WithExistingTargetCreatesDamageFeedback()
        {
            var root = new GameObject("CombatViewRoot");
            SpriteAnimationClipAsset hitClip = null;
            SpriteAnimationSetAsset feedbackAnimationSet = null;
            try
            {
                hitClip = CreateClip(4f, loop: false, SpriteAnimationKey.None, CreateSprite());
                feedbackAnimationSet = CreateSet(new SpriteAnimationEntry(SpriteAnimationKey.Hit, hitClip));
                var viewPort = new UnityCombatViewPort(
                    root.transform,
                    UnityUnitFacingMode.Rotate2D,
                    null,
                    null,
                    feedbackAnimationSet);
                viewPort.CreateUnit(new UnitSpawnViewSnapshot(new UnitId(2), new TeamId(2), "target", new BattleVector2(3f, 4f)));

                viewPort.PlayHit(new DamageViewSnapshot(
                    new UnitId(1),
                    new UnitId(2),
                    7,
                    BattleEffectSourceKind.Ability,
                    true,
                    BattleEffectType.Damage,
                    "slash",
                    null,
                    default,
                    new string[0]));

                Transform feedback = root.transform.Find("DamageFeedback_2");
                Assert.IsNotNull(feedback);
                Assert.AreEqual(root.transform, feedback.parent);
                Assert.AreEqual(new Vector3(3f, 4.65f, -0.000002f), feedback.position);
                Assert.AreEqual(new Color(1f, 0.35f, 0.2f, 1f), feedback.GetComponent<SpriteRenderer>().color);
                Assert.AreEqual(2, CountActiveDirectChildren(root.transform));
                Assert.AreEqual(Color.red, root.transform.Find("Unit_2").GetComponent<SpriteRenderer>().color);
            }
            finally
            {
                Object.DestroyImmediate(root);
                Object.DestroyImmediate(hitClip);
                Object.DestroyImmediate(feedbackAnimationSet);
            }
        }

        [Test]
        public void PlayHeal_CreatesGreenFeedbackAtTarget()
        {
            var root = new GameObject("CombatViewRoot");
            SpriteAnimationClipAsset statusAppliedClip = null;
            SpriteAnimationSetAsset feedbackAnimationSet = null;
            try
            {
                statusAppliedClip = CreateClip(4f, loop: false, SpriteAnimationKey.None, CreateSprite());
                feedbackAnimationSet = CreateSet(new SpriteAnimationEntry(SpriteAnimationKey.StatusApplied, statusAppliedClip));
                var viewPort = new UnityCombatViewPort(
                    root.transform,
                    UnityUnitFacingMode.Rotate2D,
                    null,
                    null,
                    feedbackAnimationSet);
                viewPort.CreateUnit(new UnitSpawnViewSnapshot(new UnitId(2), new TeamId(1), "target", new BattleVector2(1f, 2f)));

                viewPort.PlayHeal(new HealingViewSnapshot(
                    new UnitId(1),
                    new UnitId(2),
                    4,
                    BattleEffectSourceKind.Ability,
                    true,
                    BattleEffectType.Heal,
                    "mend",
                    null,
                    default));

                Transform feedback = root.transform.Find("HealFeedback_2");
                Assert.IsNotNull(feedback);
                Assert.AreEqual(root.transform, feedback.parent);
                Assert.That(feedback.position.x, Is.EqualTo(1f).Within(0.001f));
                Assert.That(feedback.position.y, Is.EqualTo(2.9f).Within(0.001f));
                Assert.AreEqual(new Vector3(0.31f, 0.31f, 1f), feedback.localScale);
                Assert.AreEqual(new Color(0.25f, 1f, 0.45f, 1f), feedback.GetComponent<SpriteRenderer>().color);
                SpriteFrameAnimator animator = feedback.GetComponent<SpriteFrameAnimator>();
                Assert.IsNotNull(animator);
                Assert.IsTrue(animator.IsPlaying);
                Assert.AreEqual(SpriteAnimationKey.StatusApplied, animator.CurrentKeyForTests);
                Assert.AreEqual(2, CountActiveDirectChildren(root.transform));
            }
            finally
            {
                Object.DestroyImmediate(root);
                Object.DestroyImmediate(statusAppliedClip);
                Object.DestroyImmediate(feedbackAnimationSet);
            }
        }

        [Test]
        public void PlayHeal_WithMissingTargetDoesNotCreateFeedback()
        {
            var root = new GameObject("CombatViewRoot");
            try
            {
                var viewPort = new UnityCombatViewPort(root.transform);

                viewPort.PlayHeal(new HealingViewSnapshot(
                    new UnitId(1),
                    new UnitId(99),
                    4,
                    BattleEffectSourceKind.Ability,
                    true,
                    BattleEffectType.Heal,
                    "mend",
                    null,
                    default));

                Assert.IsNull(root.transform.Find("HealFeedback_99"));
                Assert.AreEqual(0, CountActiveDirectChildren(root.transform));
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void PlayHit_WithFeedbackAnimationReleasesFeedbackWhenAnimationCompletes()
        {
            var root = new GameObject("CombatViewRoot");
            SpriteAnimationClipAsset hitClip = null;
            SpriteAnimationSetAsset feedbackAnimationSet = null;
            try
            {
                hitClip = CreateClip(4f, loop: false, SpriteAnimationKey.None, CreateSprite());
                feedbackAnimationSet = CreateSet(new SpriteAnimationEntry(SpriteAnimationKey.Hit, hitClip));
                var viewPort = new UnityCombatViewPort(
                    root.transform,
                    UnityUnitFacingMode.Rotate2D,
                    null,
                    null,
                    feedbackAnimationSet);
                viewPort.CreateUnit(new UnitSpawnViewSnapshot(new UnitId(2), new TeamId(2), "target", new BattleVector2(3f, 4f)));

                viewPort.PlayHit(new DamageViewSnapshot(
                    new UnitId(1),
                    new UnitId(2),
                    7,
                    BattleEffectSourceKind.Ability,
                    true,
                    BattleEffectType.Damage,
                    "slash",
                    null,
                    default,
                    new string[0]));

                Transform feedback = root.transform.Find("DamageFeedback_2");
                Assert.IsNotNull(feedback);
                SpriteFrameAnimator animator = feedback.GetComponent<SpriteFrameAnimator>();
                Assert.IsNotNull(animator);

                animator.Tick(0.25f);

                Assert.IsNull(root.transform.Find("DamageFeedback_2"));
                Assert.AreEqual(1, CountActiveDirectChildren(root.transform));
                Transform pooledFeedbackRoot = root.transform.Find("PooledViews/Feedback");
                Assert.IsNotNull(pooledFeedbackRoot);
                Assert.AreEqual(1, pooledFeedbackRoot.childCount);
                Assert.IsFalse(pooledFeedbackRoot.GetChild(0).gameObject.activeSelf);
            }
            finally
            {
                Object.DestroyImmediate(root);
                Object.DestroyImmediate(hitClip);
                Object.DestroyImmediate(feedbackAnimationSet);
            }
        }

        [Test]
        public void PlayHit_WithoutFeedbackAnimationDoesNotLeaveFallbackFeedback()
        {
            var root = new GameObject("CombatViewRoot");
            try
            {
                var viewPort = new UnityCombatViewPort(root.transform);
                viewPort.CreateUnit(new UnitSpawnViewSnapshot(new UnitId(2), new TeamId(2), "target", new BattleVector2(3f, 4f)));

                viewPort.PlayHit(new DamageViewSnapshot(
                    new UnitId(1),
                    new UnitId(2),
                    7,
                    BattleEffectSourceKind.Ability,
                    true,
                    BattleEffectType.Damage,
                    "slash",
                    null,
                    default,
                    new string[0]));

                Assert.IsNull(root.transform.Find("DamageFeedback_2"));
                Assert.AreEqual(1, CountActiveDirectChildren(root.transform));
                Transform pooledFeedbackRoot = root.transform.Find("PooledViews/Feedback");
                Assert.IsNotNull(pooledFeedbackRoot);
                Assert.AreEqual(1, pooledFeedbackRoot.childCount);
                Assert.IsFalse(pooledFeedbackRoot.GetChild(0).gameObject.activeSelf);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void PlayHit_WithMissingTargetDoesNotCreateFeedback()
        {
            var root = new GameObject("CombatViewRoot");
            try
            {
                var viewPort = new UnityCombatViewPort(root.transform);

                viewPort.PlayHit(new DamageViewSnapshot(
                    new UnitId(1),
                    new UnitId(99),
                    7,
                    BattleEffectSourceKind.Ability,
                    true,
                    BattleEffectType.Damage,
                    "slash",
                    null,
                    default,
                    new string[0]));

                Assert.IsNull(root.transform.Find("DamageFeedback_99"));
                Assert.AreEqual(0, CountActiveDirectChildren(root.transform));
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void PlayProjectileHit_CreatesFeedbackAtHitPosition()
        {
            var root = new GameObject("CombatViewRoot");
            SpriteAnimationClipAsset projectileHitClip = null;
            SpriteAnimationSetAsset feedbackAnimationSet = null;
            try
            {
                projectileHitClip = CreateClip(4f, loop: false, SpriteAnimationKey.None, CreateSprite());
                feedbackAnimationSet = CreateSet(new SpriteAnimationEntry(SpriteAnimationKey.ProjectileHit, projectileHitClip));
                var viewPort = new UnityCombatViewPort(
                    root.transform,
                    UnityUnitFacingMode.Rotate2D,
                    null,
                    null,
                    feedbackAnimationSet);

                viewPort.PlayProjectileHit(new ProjectileHitViewSnapshot(new ProjectileId(3), new UnitId(1), new UnitId(2), new BattleVector2(6f, 7f)));

                Transform feedback = root.transform.Find("ProjectileHitFeedback_3");
                Assert.IsNotNull(feedback);
                Assert.AreEqual(root.transform, feedback.parent);
                Assert.AreEqual(new Vector3(6f, 7f, 0f), feedback.position);
                Assert.AreEqual(new Color(1f, 0.9f, 0.2f, 1f), feedback.GetComponent<SpriteRenderer>().color);
                Assert.AreEqual(1, CountActiveDirectChildren(root.transform));
            }
            finally
            {
                Object.DestroyImmediate(root);
                Object.DestroyImmediate(projectileHitClip);
                Object.DestroyImmediate(feedbackAnimationSet);
            }
        }

        [Test]
        public void PlayStatusAppliedAndExpired_CreateFeedbackForExistingUnit()
        {
            var root = new GameObject("CombatViewRoot");
            SpriteAnimationClipAsset appliedClip = null;
            SpriteAnimationClipAsset expiredClip = null;
            SpriteAnimationSetAsset feedbackAnimationSet = null;
            try
            {
                appliedClip = CreateClip(4f, loop: false, SpriteAnimationKey.None, CreateSprite());
                expiredClip = CreateClip(4f, loop: false, SpriteAnimationKey.None, CreateSprite());
                feedbackAnimationSet = CreateSet(
                    new SpriteAnimationEntry(SpriteAnimationKey.StatusApplied, appliedClip),
                    new SpriteAnimationEntry(SpriteAnimationKey.StatusExpired, expiredClip));
                var viewPort = new UnityCombatViewPort(
                    root.transform,
                    UnityUnitFacingMode.Rotate2D,
                    null,
                    null,
                    feedbackAnimationSet);
                viewPort.CreateUnit(new UnitSpawnViewSnapshot(new UnitId(2), new TeamId(1), "target", new BattleVector2(3f, 4f)));

                viewPort.PlayStatusApplied(new StatusViewSnapshot(new UnitId(2), new UnitId(1), "burn", StatusPolarity.Debuff));
                viewPort.PlayStatusExpired(new StatusViewSnapshot(new UnitId(2), default, "burn", StatusPolarity.Debuff));

                Transform applied = root.transform.Find("StatusAppliedFeedback_2_burn");
                Transform expired = root.transform.Find("StatusExpiredFeedback_2_burn");
                Assert.IsNotNull(applied);
                Assert.IsNotNull(expired);
                Assert.AreEqual(new Vector3(2.65f, 4.85f, -0.000002f), applied.position);
                Assert.AreEqual(new Vector3(3.35f, 4.85f, -0.000002f), expired.position);
                Assert.AreEqual(new Color(0.85f, 0.25f, 1f, 1f), applied.GetComponent<SpriteRenderer>().color);
                Assert.AreEqual(new Color(0.45f, 0.2f, 0.55f, 1f), expired.GetComponent<SpriteRenderer>().color);
                Assert.AreEqual(3, CountActiveDirectChildren(root.transform));
            }
            finally
            {
                Object.DestroyImmediate(root);
                Object.DestroyImmediate(appliedClip);
                Object.DestroyImmediate(expiredClip);
                Object.DestroyImmediate(feedbackAnimationSet);
            }
        }

        [Test]
        public void PlayStatusAppliedAndExpired_WithMissingUnitDoNotCreateFeedback()
        {
            var root = new GameObject("CombatViewRoot");
            try
            {
                var viewPort = new UnityCombatViewPort(root.transform);

                viewPort.PlayStatusApplied(new StatusViewSnapshot(new UnitId(99), new UnitId(1), "burn", StatusPolarity.Debuff));
                viewPort.PlayStatusExpired(new StatusViewSnapshot(new UnitId(99), default, "burn", StatusPolarity.Debuff));

                Assert.IsNull(root.transform.Find("StatusAppliedFeedback_99_burn"));
                Assert.IsNull(root.transform.Find("StatusExpiredFeedback_99_burn"));
                Assert.AreEqual(0, CountActiveDirectChildren(root.transform));
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void Release_WhenInactiveRootWasDestroyed_HidesDetachesAndResetsBeforeFallbackDestroy()
        {
            var activeRoot = new GameObject("CombatViewRoot");
            var inactiveRoot = new GameObject("PooledViews");
            try
            {
                var resetCalled = false;
                bool activeAtReset = true;
                Transform parentAtReset = activeRoot.transform;
                var pool = new GameObjectPool(
                    "TestViews",
                    inactiveRoot.transform,
                    () => new GameObject("PooledView"),
                    instance =>
                    {
                        resetCalled = true;
                        activeAtReset = instance.activeSelf;
                        parentAtReset = instance.transform.parent;
                        instance.name = "ResetView";
                    });

                GameObject instance = pool.Get(activeRoot.transform, gameObject => gameObject.name = "ActiveView");
                Object.DestroyImmediate(inactiveRoot);

                pool.Release(instance);

                Assert.IsTrue(resetCalled);
                Assert.IsFalse(activeAtReset);
                Assert.IsNull(parentAtReset);
            }
            finally
            {
                Object.DestroyImmediate(activeRoot);
                Object.DestroyImmediate(inactiveRoot);
            }
        }

        [Test]
        public void ViewHandles_CacheHotPathComponents()
        {
            Type viewPortType = typeof(UnityCombatViewPort);
            Type unitHandleType = viewPortType.GetNestedType("UnitViewHandle", BindingFlags.NonPublic);
            Type projectileHandleType = viewPortType.GetNestedType("ProjectileViewHandle", BindingFlags.NonPublic);

            Assert.IsNotNull(unitHandleType);
            Assert.IsNotNull(projectileHandleType);
            AssertUnitHandleCachesHotPathComponents(unitHandleType);
            AssertProjectileHandleCachesHotPathComponents(projectileHandleType);
        }

        private static int CountActiveDirectChildren(Transform parent)
        {
            var count = 0;
            for (var i = 0; i < parent.childCount; i++)
            {
                if (parent.GetChild(i).gameObject.activeSelf)
                {
                    count++;
                }
            }

            return count;
        }

        private static float NormalizedAngle(float angle)
        {
            angle %= 360f;
            return angle < 0f ? angle + 360f : angle;
        }

        private static void TickSmootherForTests(Component smoother, float deltaSeconds)
        {
            System.Reflection.MethodInfo tickMethod = smoother.GetType().GetMethod(
                "TickForTests",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
            Assert.IsNotNull(tickMethod);
            tickMethod.Invoke(smoother, new object[] { deltaSeconds });
        }

        private static void CompleteSmoothing(Transform transform)
        {
            Component smoother = transform.GetComponent("CombatViewTransformSmoother");
            Assert.IsNotNull(smoother);
            TickSmootherForTests(smoother, 1f);
        }

        private static void AssertUnitHandleCachesHotPathComponents(Type handleType)
        {
            AssertReadableProperty(handleType, "GameObject", typeof(GameObject));
            AssertReadableProperty(handleType, "Transform", typeof(Transform));
            AssertReadableProperty(handleType, "View", typeof(CombatUnitView));
            AssertReadableProperty(handleType, "SpriteRenderer", typeof(SpriteRenderer));
        }

        private static void AssertProjectileHandleCachesHotPathComponents(Type handleType)
        {
            AssertReadableProperty(handleType, "GameObject", typeof(GameObject));
            AssertReadableProperty(handleType, "Transform", typeof(Transform));
            AssertReadableProperty(handleType, "Smoother", typeof(CombatViewTransformSmoother));
            AssertReadableProperty(handleType, "Animator", typeof(SpriteFrameAnimator));
        }

        private static void AssertReadableProperty(Type declaringType, string propertyName, Type propertyType)
        {
            PropertyInfo property = declaringType.GetProperty(
                propertyName,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

            Assert.IsNotNull(property, $"{declaringType.Name} should expose cached {propertyName}.");
            Assert.AreEqual(propertyType, property.PropertyType);
            Assert.IsTrue(property.CanRead);
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

        private static SpriteAnimationSetAsset CreateSet(SpriteAnimationEntry[] entries, SpriteAbilityAnimationEntry[] abilityEntries)
        {
            var animationSet = ScriptableObject.CreateInstance<SpriteAnimationSetAsset>();
            animationSet.ConfigureForTests(entries, abilityEntries);
            return animationSet;
        }

        private static CombatantConfigAsset CreateCombatantDefinition(string definitionId)
        {
            var combatant = ScriptableObject.CreateInstance<CombatantConfigAsset>();
            combatant.name = definitionId;
            return combatant;
        }

        private static GameObject CreatePresentationPrefab(string childName)
        {
            return CreatePresentationPrefab(childName, animationSet: null);
        }

        private static GameObject CreatePresentationPrefab(string childName, SpriteAnimationSetAsset animationSet)
        {
            var prefab = new GameObject(childName + "Prefab");
            prefab.SetActive(false);
            prefab.AddComponent<SpriteRenderer>();
            CombatUnitView view = prefab.AddComponent<CombatUnitView>();
            view.ConfigureForTests(animationSet);
            var visual = new GameObject(childName);
            visual.transform.SetParent(prefab.transform, worldPositionStays: false);
            return prefab;
        }

        private static CombatantPresentationCatalogAsset CreateCatalog(params CombatantPresentationCatalogEntry[] entries)
        {
            var catalog = ScriptableObject.CreateInstance<CombatantPresentationCatalogAsset>();
            catalog.ConfigureForTests(entries);
            return catalog;
        }

        private static Sprite CreateSprite()
        {
            return Sprite.Create(Texture2D.whiteTexture, new Rect(0f, 0f, 1f, 1f), new Vector2(0.5f, 0.5f), 1f);
        }
    }
}
