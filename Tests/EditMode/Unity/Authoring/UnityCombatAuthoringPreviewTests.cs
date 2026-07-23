using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Combat.Foundation.Diagnostics;
using Combat.Runtime.Display;
using Combat.Runtime.Runner;
using Combat.Core.Battle;
using Combat.Unity.Authoring;
using Combat.Unity.Demo;
using Combat.Unity.Display;
using Combat.Unity.Editor;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Combat.Tests.Unity.Authoring
{
    public sealed class UnityCombatAuthoringPreviewTests
    {
        private readonly List<ScriptableObject> _assets = new List<ScriptableObject>();

        [TearDown]
        public void TearDown()
        {
            foreach (ScriptableObject asset in _assets)
            {
                Object.DestroyImmediate(asset);
            }
            _assets.Clear();
        }

        [Test]
        public void Preview_ExposesInternalTransactionalCompositionSeam()
        {
            MethodInfo method = typeof(UnityCombatBootstrap)
                .GetMethods(BindingFlags.Instance | BindingFlags.NonPublic)
                .SingleOrDefault(candidate =>
                    candidate.Name == "BuildAuthoringPreview" &&
                    candidate.GetParameters().Length == 4);

            Assert.That(method, Is.Not.Null);
        }

        [Test]
        public void Start_RequiresScenarioAsset()
        {
            var gameObject = new GameObject("Bootstrap");
            try
            {
                UnityCombatBootstrap bootstrap = gameObject.AddComponent<UnityCombatBootstrap>();
                MethodInfo start = typeof(UnityCombatBootstrap).GetMethod(
                    "Start", BindingFlags.Instance | BindingFlags.NonPublic);

                TargetInvocationException exception = Assert.Throws<TargetInvocationException>(
                    () => start.Invoke(bootstrap, null));

                Assert.That(exception.InnerException, Is.TypeOf<InvalidOperationException>());
                Assert.That(exception.InnerException.Message, Does.Contain("BattleScenarioAsset"));
            }
            finally
            {
                Object.DestroyImmediate(gameObject);
            }
        }

        [Test]
        public void SerializedPreviewSettings_HaveStableDefaults()
        {
            var gameObject = new GameObject("Bootstrap");
            try
            {
                UnityCombatBootstrap bootstrap = gameObject.AddComponent<UnityCombatBootstrap>();

                AssertField(bootstrap, "_unitFacingMode", UnityUnitFacingMode.SideScrollerFlip);
                AssertField<object>(bootstrap, "_combatantPresentationCatalog", null);
                AssertField<object>(bootstrap, "_projectileAnimationSet", null);
                AssertField<object>(bootstrap, "_feedbackAnimationSet", null);
                AssertField(bootstrap, "_enableRuntimeObserver", false);
            }
            finally
            {
                Object.DestroyImmediate(gameObject);
            }
        }

        [Test]
        public void DemoInstaller_DefaultScenarioConvertsActionTimingsAtThirtyTicksPerSecond()
        {
            AbilityConfigAsset basicSlash = CreateAsset<AbilityConfigAsset>("DefaultBasicSlash");
            AbilityConfigAsset firebolt = CreateAsset<AbilityConfigAsset>("DefaultFirebolt");
            AbilityConfigAsset counterStance = CreateAsset<AbilityConfigAsset>("DefaultCounterStance");
            AbilityConfigAsset killFuryStance = CreateAsset<AbilityConfigAsset>("DefaultKillFuryStance");
            ProjectileEmitterConfigAsset fireboltEmitter =
                CreateAsset<ProjectileEmitterConfigAsset>("DefaultFireboltEmitter");
            ProjectileEmitterConfigAsset fireboltBurst =
                CreateAsset<ProjectileEmitterConfigAsset>("DefaultFireboltBurst");
            ProjectileConfigAsset fireboltProjectile =
                CreateAsset<ProjectileConfigAsset>("DefaultFireboltProjectile");
            ProjectileConfigAsset fireboltBurstProjectile =
                CreateAsset<ProjectileConfigAsset>("DefaultFireboltBurstProjectile");
            StatusConfigAsset burn = CreateAsset<StatusConfigAsset>("DefaultBurn");
            StatusConfigAsset killAttackStack =
                CreateAsset<StatusConfigAsset>("DefaultKillAttackStack");
            StatusConfigAsset killFury = CreateAsset<StatusConfigAsset>("DefaultKillFury");
            StatusConfigAsset thorns = CreateAsset<StatusConfigAsset>("DefaultThorns");
            CombatantConfigAsset melee = CreateAsset<CombatantConfigAsset>("DefaultMelee");
            BattleScenarioAsset scenario = CreateAsset<BattleScenarioAsset>("DefaultBattleScenario");

            InvokeInstallerConfigure("ConfigureBasicSlash", basicSlash);
            InvokeInstallerConfigure("ConfigureBurn", burn);
            InvokeInstallerConfigure("ConfigureFireboltBurstProjectile", fireboltBurstProjectile);
            InvokeInstallerConfigure("ConfigureFireboltBurst", fireboltBurst, fireboltBurstProjectile);
            InvokeInstallerConfigure(
                "ConfigureFireboltProjectile", fireboltProjectile, burn, fireboltBurst);
            InvokeInstallerConfigure("ConfigureFireboltEmitter", fireboltEmitter, fireboltProjectile);
            InvokeInstallerConfigure("ConfigureFirebolt", firebolt, fireboltEmitter);
            InvokeInstallerConfigure("ConfigureCounterStance", counterStance, thorns);
            InvokeInstallerConfigure("ConfigureKillAttackStack", killAttackStack);
            InvokeInstallerConfigure("ConfigureKillFury", killFury, killAttackStack);
            InvokeInstallerConfigure("ConfigureKillFuryStance", killFuryStance, killFury);
            InvokeInstallerConfigure(
                "ConfigureMelee", melee, basicSlash, firebolt, counterStance, killFuryStance);
            InvokeInstallerConfigure("ConfigureScenario", scenario, melee);

            BattleConfig config = BattleAuthoringConverter.BuildBattleConfig(scenario);
            CombatantDefinition definition = config.InitialSpawns[0].Definition;

            Assert.That(config.TicksPerSecond, Is.EqualTo(30));
            AssertAbilityTiming(definition.BasicAbility, "basic-slash", 2, 3);
            AssertAbilityTiming(FindAbility(definition, "firebolt"), "firebolt", 3, 4);
            AssertAbilityTiming(FindAbility(definition, "counter-stance"), "counter-stance", 1, 4);
        }

        [Test]
        public void Preview_PresentsInitialFactsBeforeDiagnosticsAndPublishesVisibleRoot()
        {
            var order = new List<string>();
            var viewport = new TestViewPort(() => order.Add("present"));
            var sink = new RecordingLogSink(() => order.Add("diagnostics"));
            UnityCombatBootstrap bootstrap = CreateConfiguredBootstrap();
            try
            {
                bootstrap.BuildAuthoringPreview(
                    () => new CombatLogger(CombatLogSettings.ShowInfoAndAbove, sink),
                    (_, __) => viewport,
                    (_, __) => { },
                    Object.DestroyImmediate);

                int firstDiagnostic = order.IndexOf("diagnostics");
                Assert.That(firstDiagnostic, Is.GreaterThan(0));
                Assert.That(order.Take(firstDiagnostic), Has.All.EqualTo("present"));
                Assert.That(GetField<GameObject>(bootstrap, "_rootObject").activeSelf, Is.True);
                Assert.That(GetField<BattleInstance>(bootstrap, "_instance"), Is.Not.Null);
            }
            finally
            {
                Object.DestroyImmediate(bootstrap.gameObject);
            }
        }

        [Test]
        public void Preview_LoggerFailureLeavesNoPublishedOrVisibleRoot()
        {
            UnityCombatBootstrap bootstrap = CreateConfiguredBootstrap();
            GameObject destroyed = null;
            try
            {
                Assert.Throws<InvalidOperationException>(() => bootstrap.BuildAuthoringPreview(
                    () => new CombatLogger(
                        CombatLogSettings.ShowInfoAndAbove,
                        new ThrowingLogSink()),
                    (_, __) => new TestViewPort(null),
                    (_, __) => { },
                    root => destroyed = root));

                AssertFailedTransaction(bootstrap, destroyed);
            }
            finally
            {
                if (destroyed != null)
                {
                    Object.DestroyImmediate(destroyed);
                }
                Object.DestroyImmediate(bootstrap.gameObject);
            }
        }

        [Test]
        public void Preview_PresentationFailureSkipsDiagnosticsAndCleansRoot()
        {
            UnityCombatBootstrap bootstrap = CreateConfiguredBootstrap();
            var sink = new RecordingLogSink(null);
            GameObject destroyed = null;
            try
            {
                Assert.Throws<InvalidOperationException>(() => bootstrap.BuildAuthoringPreview(
                    () => new CombatLogger(CombatLogSettings.ShowInfoAndAbove, sink),
                    (_, __) => new TestViewPort(
                        () => throw new InvalidOperationException("Presentation failed.")),
                    (_, __) => { },
                    root => destroyed = root));

                Assert.That(sink.Count, Is.EqualTo(0));
                AssertFailedTransaction(bootstrap, destroyed);
            }
            finally
            {
                if (destroyed != null)
                {
                    Object.DestroyImmediate(destroyed);
                }
                Object.DestroyImmediate(bootstrap.gameObject);
            }
        }

        [Test]
        public void Preview_ViewportConstructionFailureCleansInactiveDetachedRoot()
        {
            UnityCombatBootstrap bootstrap = CreateConfiguredBootstrap();
            GameObject destroyed = null;
            try
            {
                Assert.Throws<InvalidOperationException>(() => bootstrap.BuildAuthoringPreview(
                    () => CombatLogger.Disabled,
                    (_, __) => throw new InvalidOperationException("Viewport failed."),
                    (_, __) => { },
                    root => destroyed = root));

                AssertFailedTransaction(bootstrap, destroyed);
            }
            finally
            {
                if (destroyed != null)
                {
                    Object.DestroyImmediate(destroyed);
                }
                Object.DestroyImmediate(bootstrap.gameObject);
            }
        }

        [Test]
        public void Preview_CleanupIsIdempotent()
        {
            UnityCombatBootstrap bootstrap = CreateConfiguredBootstrap();
            try
            {
                bootstrap.BuildAuthoringPreview(
                    () => CombatLogger.Disabled,
                    (_, __) => new TestViewPort(null),
                    (_, __) => { },
                    Object.DestroyImmediate);
                MethodInfo cleanup = typeof(UnityCombatBootstrap).GetMethod(
                    "DestroyViewRoot", BindingFlags.Instance | BindingFlags.NonPublic);

                Assert.DoesNotThrow(() => cleanup.Invoke(bootstrap, null));
                Assert.DoesNotThrow(() => cleanup.Invoke(bootstrap, null));
                Assert.That(GetField<GameObject>(bootstrap, "_rootObject"), Is.Null);
                Assert.That(GetField<BattleInstance>(bootstrap, "_instance"), Is.Null);
            }
            finally
            {
                Object.DestroyImmediate(bootstrap.gameObject);
            }
        }

        private UnityCombatBootstrap CreateConfiguredBootstrap()
        {
            AbilityConfigAsset ability = CreateAsset<AbilityConfigAsset>("PreviewBasicAbility");
            CombatantConfigAsset combatant = CreateAsset<CombatantConfigAsset>("PreviewCombatant");
            BattleScenarioAsset scenario = CreateAsset<BattleScenarioAsset>("PreviewScenario");
            typeof(CombatantConfigAsset).GetField(
                    "_basicAbility", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(combatant, ability);
            typeof(CombatantConfigAsset).GetField(
                    "_stats", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(combatant, new[]
                {
                    new BattleStatConfig(BattleStatId.MaxHealth, 10f),
                    new BattleStatConfig(BattleStatId.MoveSpeed, 1f)
                });
            typeof(BattleScenarioAsset).GetField(
                    "_initialSpawns", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(scenario, new[]
                {
                    new SpawnEntry(1, combatant, Vector2.zero)
                });
            var gameObject = new GameObject("Bootstrap");
            UnityCombatBootstrap bootstrap = gameObject.AddComponent<UnityCombatBootstrap>();
            typeof(UnityCombatBootstrap).GetField(
                    "_scenario", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(bootstrap, scenario);
            return bootstrap;
        }

        private T CreateAsset<T>(string name) where T : ScriptableObject
        {
            T asset = ScriptableObject.CreateInstance<T>();
            asset.name = name;
            _assets.Add(asset);
            return asset;
        }

        private static void InvokeInstallerConfigure(string name, params object[] arguments)
        {
            MethodInfo method = typeof(DemoScenarioInstaller).GetMethod(
                name, BindingFlags.Static | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null, "Missing DemoScenarioInstaller." + name);
            method.Invoke(null, arguments);
        }

        private static AbilityDefinition FindAbility(
            CombatantDefinition definition,
            string id)
        {
            AbilityDefinition ability = definition.Abilities.FirstOrDefault(
                candidate => candidate.Id == id);
            Assert.That(ability, Is.Not.Null, "Missing default ability " + id);
            return ability;
        }

        private static void AssertAbilityTiming(
            AbilityDefinition ability,
            string id,
            int windupTicks,
            int recoveryTicks)
        {
            Assert.That(ability.Id, Is.EqualTo(id));
            Assert.That(ability.WindupTicks, Is.EqualTo(windupTicks));
            Assert.That(ability.RecoveryTicks, Is.EqualTo(recoveryTicks));
        }

        private static void AssertFailedTransaction(
            UnityCombatBootstrap bootstrap,
            GameObject destroyed)
        {
            Assert.That(destroyed, Is.Not.Null);
            Assert.That(destroyed.activeSelf, Is.False);
            Assert.That(destroyed.transform.parent, Is.Null);
            Assert.That(GetField<GameObject>(bootstrap, "_rootObject"), Is.Null);
            Assert.That(GetField<BattleInstance>(bootstrap, "_instance"), Is.Null);
        }

        private static T GetField<T>(UnityCombatBootstrap bootstrap, string name)
        {
            return (T)typeof(UnityCombatBootstrap).GetField(
                    name, BindingFlags.Instance | BindingFlags.NonPublic)
                .GetValue(bootstrap);
        }

        private static void AssertField<T>(UnityCombatBootstrap bootstrap, string name, T expected)
        {
            FieldInfo field = typeof(UnityCombatBootstrap).GetField(
                name, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null);
            Assert.That(field.GetValue(bootstrap), Is.EqualTo(expected));
        }

        private sealed class RecordingLogSink : ICombatLogSink
        {
            private readonly Action _onWrite;

            public RecordingLogSink(Action onWrite)
            {
                _onWrite = onWrite;
            }

            public int Count { get; private set; }

            public void Write(CombatLogEntry entry)
            {
                Count++;
                _onWrite?.Invoke();
            }
        }

        private sealed class ThrowingLogSink : ICombatLogSink
        {
            public void Write(CombatLogEntry entry)
            {
                throw new InvalidOperationException("Diagnostics failed.");
            }
        }

        private sealed class TestViewPort : ICombatViewPort
        {
            private readonly Action _onCreateUnit;

            public TestViewPort(Action onCreateUnit)
            {
                _onCreateUnit = onCreateUnit;
            }

            public void CreateUnit(UnitSpawnViewSnapshot snapshot) => _onCreateUnit?.Invoke();
            public void MoveUnit(UnitId unitId, BattleVector2 position) { }
            public void StopUnitMovement(UnitId unitId) { }
            public void FaceUnit(UnitId unitId, BattleVector2 facing) { }
            public void SetUnitVisibility(UnitId unitId, bool isVisible) { }
            public void PlayAction(ActionViewSnapshot snapshot) { }
            public void PlayHit(DamageViewSnapshot snapshot) { }
            public void PlayHeal(HealingViewSnapshot snapshot) { }
            public void DestroyUnit(UnitId unitId) { }
            public void CreateProjectile(ProjectileViewSnapshot snapshot) { }
            public void MoveProjectile(ProjectileId projectileId, BattleVector2 position) { }
            public void PlayProjectileHit(ProjectileHitViewSnapshot snapshot) { }
            public void DestroyProjectile(ProjectileId projectileId) { }
            public void PlayStatusApplied(StatusViewSnapshot snapshot) { }
            public void PlayStatusExpired(StatusViewSnapshot snapshot) { }
            public void ShowBattleResult(BattleResult result) { }
        }
    }
}
