using Combat.Core.Battle;
using NUnit.Framework;

namespace Combat.Tests.Core
{
    public sealed class UnitActionComponentTests
    {
        [Test]
        public void None_AllowsMovementTurningAndStartingAction()
        {
            var world = new BattleWorld();
            SpawnUnit(world, new UnitId(1), new TeamId(1));
            world.TryFindEntity(new UnitId(1), out EntityId entity);

            Assert.IsTrue(UnitControlRules.CanMove(world, entity));
            Assert.IsTrue(UnitControlRules.CanTurn(world, entity));
            Assert.IsTrue(UnitControlRules.CanStartAction(world, entity));
        }

        [Test]
        public void ActiveActionLocksMovementAndStartingAnotherAction()
        {
            var world = new BattleWorld();
            SpawnUnit(world, new UnitId(1), new TeamId(1));
            world.TryFindEntity(new UnitId(1), out EntityId entity);
            world.UnitActionComponents.Set(entity, UnitActionComponent.Ability(
                abilityIndex: 0,
                abilityId: "basic-slash",
                target: entity,
                startedTick: new BattleTick(3),
                releaseTick: new BattleTick(4),
                endTick: new BattleTick(5),
                locks: BattleActionLocks.Movement | BattleActionLocks.StartAnotherAction));

            Assert.IsFalse(UnitControlRules.CanMove(world, entity));
            Assert.IsTrue(UnitControlRules.CanTurn(world, entity));
            Assert.IsFalse(UnitControlRules.CanStartAction(world, entity));
        }

        [Test]
        public void SpawnedCombatantGetsNoneActionComponent()
        {
            var world = new BattleWorld();
            SpawnUnit(world, new UnitId(1), new TeamId(1));
            world.TryFindEntity(new UnitId(1), out EntityId entity);

            Assert.IsTrue(world.UnitActionComponents.TryGet(entity, out UnitActionComponent action));
            Assert.AreEqual(UnitActionType.None, action.Type);
        }

        private static void SpawnUnit(BattleWorld world, UnitId unitId, TeamId teamId)
        {
            world.CommandBuffer.SpawnCombatant(new SpawnCombatantCommand(
                unitId,
                new CombatantSpawnData(
                    teamId,
                    "unit",
                    new BattleVector2(0f, 0f),
                    maxHealth: 10,
                    radius: BattleScalar.FromFloat(0.25f),
                    moveSpeed: BattleScalar.FromFloat(1f),
                    basicAbility: TestCombatants.AbilitySpawn("basic-slash", 1f, 1, 1),
                    abilities: new AbilitySpawnData[0],
                    brain: BrainSpawnData.None)));
            world.FlushSpawnCombatantCommands(new Combat.Foundation.Events.EventBuffer<BattleEvent>(), new Combat.Foundation.Events.EventSequence(), new BattleTick(0));
        }
    }
}
