using System.Reflection;
using Combat.Core.Battle;
using Combat.Unity.Display;
using Combat.Unity.Editor;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Combat.Tests.Unity
{
    public sealed class CombatUnitRuntimeObserverTests
    {
        [Test]
        public void RefreshForTests_CopiesSnapshotIntoInspectorFields()
        {
            var gameObject = new GameObject("Unit_1");
            try
            {
                var observer = gameObject.AddComponent<CombatUnitRuntimeObserver>();
                var source = new FixedSnapshotSource(UnitSnapshot(new UnitId(1), "attacker"));

                observer.Bind(new UnitId(1), source);
                observer.RefreshForTests();

                Assert.IsTrue(observer.IsBoundForTests);
                Assert.IsTrue(observer.HasSnapshotForTests);
                Assert.AreEqual(1, observer.UnitIdForTests);
                Assert.AreEqual("attacker", observer.DefinitionIdForTests);
                Assert.AreEqual(10, observer.CurrentHealthForTests);
                Assert.AreEqual("Alive", observer.LifeStateForTests);
                Assert.AreEqual(1, observer.AbilityCountForTests);
                Assert.AreEqual(1, observer.StatusCountForTests);
                object status = GetFirstObservedStatus(observer);
                Assert.AreEqual(2, GetPrivateInt(status, "_stackCount"));
                Assert.AreEqual(5, GetPrivateInt(status, "_maxStacks"));
            }
            finally
            {
                Object.DestroyImmediate(gameObject);
            }
        }

        [Test]
        public void ClearBinding_ClearsSnapshotFields()
        {
            var gameObject = new GameObject("Unit_1");
            try
            {
                var observer = gameObject.AddComponent<CombatUnitRuntimeObserver>();
                observer.Bind(new UnitId(1), new FixedSnapshotSource(UnitSnapshot(new UnitId(1), "attacker")));
                observer.RefreshForTests();

                observer.ClearBinding();

                Assert.IsFalse(observer.IsBoundForTests);
                Assert.IsFalse(observer.HasSnapshotForTests);
                Assert.AreEqual(0, observer.UnitIdForTests);
                Assert.AreEqual(string.Empty, observer.DefinitionIdForTests);
                Assert.AreEqual(0, observer.AbilityCountForTests);
                Assert.AreEqual(0, observer.StatusCountForTests);
            }
            finally
            {
                Object.DestroyImmediate(gameObject);
            }
        }

        [Test]
        public void LateUpdate_DoesNotRefreshWhenGameObjectIsNotSelected()
        {
            var gameObject = new GameObject("Unit_1");
            try
            {
                var observer = gameObject.AddComponent<CombatUnitRuntimeObserver>();
                var source = new MutableSnapshotSource(UnitSnapshot(new UnitId(1), "attacker"));
                observer.Bind(new UnitId(1), source);
                source.Snapshot = UnitSnapshot(new UnitId(1), "updated-attacker");
                Selection.activeGameObject = null;

                InvokeLateUpdate(observer);

                Assert.AreEqual("attacker", observer.DefinitionIdForTests);
                Assert.AreEqual(1, source.RequestCount);
            }
            finally
            {
                Selection.activeGameObject = null;
                Object.DestroyImmediate(gameObject);
            }
        }

        [Test]
        public void LateUpdate_RefreshesWhenGameObjectIsSelected()
        {
            var gameObject = new GameObject("Unit_1");
            try
            {
                var observer = gameObject.AddComponent<CombatUnitRuntimeObserver>();
                var source = new MutableSnapshotSource(UnitSnapshot(new UnitId(1), "attacker"));
                observer.Bind(new UnitId(1), source);
                source.Snapshot = UnitSnapshot(new UnitId(1), "updated-attacker");
                Selection.activeGameObject = gameObject;

                InvokeLateUpdate(observer);

                Assert.AreEqual("updated-attacker", observer.DefinitionIdForTests);
                Assert.AreEqual(2, source.RequestCount);
            }
            finally
            {
                Selection.activeGameObject = null;
                Object.DestroyImmediate(gameObject);
            }
        }

        [Test]
        public void InspectorRuntimeValues_UsePackageOwnedReadOnlyCustomEditor()
        {
            var gameObject = new GameObject("Unit_1");
            UnityEditor.Editor editor = null;
            try
            {
                var observer = gameObject.AddComponent<CombatUnitRuntimeObserver>();
                editor = UnityEditor.Editor.CreateEditor(observer);

                Assert.That(editor, Is.TypeOf<CombatUnitRuntimeObserverEditor>());
            }
            finally
            {
                if (editor != null)
                {
                    Object.DestroyImmediate(editor);
                }

                Object.DestroyImmediate(gameObject);
            }
        }

        private static void InvokeLateUpdate(CombatUnitRuntimeObserver observer)
        {
            typeof(CombatUnitRuntimeObserver)
                .GetMethod("LateUpdate", BindingFlags.Instance | BindingFlags.NonPublic)
                ?.Invoke(observer, null);
        }

        private static object GetFirstObservedStatus(CombatUnitRuntimeObserver observer)
        {
            FieldInfo statusesField = typeof(CombatUnitRuntimeObserver).GetField("_statuses", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(statusesField);
            var statuses = (System.Array)statusesField.GetValue(observer);
            Assert.IsNotNull(statuses);
            Assert.Greater(statuses.Length, 0);
            return statuses.GetValue(0);
        }

        private static int GetPrivateInt(object instance, string fieldName)
        {
            FieldInfo field = instance.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(field);
            return (int)field.GetValue(instance);
        }

        private static UnitRuntimeSnapshot UnitSnapshot(UnitId unitId, string definitionId)
        {
            return new UnitRuntimeSnapshot(
                new BattleTick(7),
                unitId,
                definitionId,
                new TeamId(1),
                new BattleVector2(2f, 3f),
                radius: 0.25f,
                currentHealth: 10,
                maxHealth: 12,
                lifeState: "Alive",
                hasBrain: true,
                brainDefinitionId: "state-machine",
                brainKind: "StateMachine",
                brainState: "Attack",
                brainStateEnteredTick: new BattleTick(5),
                hasTarget: true,
                targetUnitId: new UnitId(2),
                moveSpeed: 4f,
                abilities: new[]
                {
                    new AbilityRuntimeSnapshot(0, true, "basic-attack", 2f, 5, 3, 1)
                },
                statuses: new[]
                {
                    new StatusRuntimeSnapshot("burn", StatusPolarity.Debuff, true, new UnitId(2), 4, 1, 1, 2, 0, 0, stackCount: 2, maxStacks: 5)
                });
        }

        private sealed class FixedSnapshotSource : IBattleRuntimeSnapshotSource
        {
            private readonly UnitRuntimeSnapshot _snapshot;

            public FixedSnapshotSource(UnitRuntimeSnapshot snapshot)
            {
                _snapshot = snapshot;
            }

            public bool TryGetUnitRuntimeSnapshot(UnitId unitId, out UnitRuntimeSnapshot snapshot)
            {
                if (unitId.Equals(_snapshot.UnitId))
                {
                    snapshot = _snapshot;
                    return true;
                }

                snapshot = default;
                return false;
            }
        }

        private sealed class MutableSnapshotSource : IBattleRuntimeSnapshotSource
        {
            public MutableSnapshotSource(UnitRuntimeSnapshot snapshot)
            {
                Snapshot = snapshot;
            }

            public UnitRuntimeSnapshot Snapshot { get; set; }
            public int RequestCount { get; private set; }

            public bool TryGetUnitRuntimeSnapshot(UnitId unitId, out UnitRuntimeSnapshot snapshot)
            {
                RequestCount++;
                if (unitId.Equals(Snapshot.UnitId))
                {
                    snapshot = Snapshot;
                    return true;
                }

                snapshot = default;
                return false;
            }
        }
    }
}
