using System;
using System.Collections.Generic;
using Combat.Core.Battle;
using Combat.Foundation.Events;
using NUnit.Framework;

namespace Combat.Tests.Core
{
    public sealed class BattleWorldSpawnTests
    {
        [Test]
        public void SpawnCombatantCommand_DoesNotChangeWorldBeforeFlush()
        {
            var world = new BattleWorld();

            world.CommandBuffer.SpawnCombatant(new SpawnCombatantCommand(
                new UnitId(1),
                SpawnData(new TeamId(1), new BattleVector2(2f, 3f))));

            Assert.IsFalse(world.TryFindEntity(new UnitId(1), out _));
        }

        [Test]
        public void FlushSpawnCombatantCommands_CreatesComponentsAndEvents()
        {
            var world = new BattleWorld();
            var events = new EventBuffer<BattleEvent>();
            var sequence = new EventSequence();

            world.CommandBuffer.SpawnCombatant(new SpawnCombatantCommand(
                new UnitId(1),
                SpawnData(new TeamId(1), new BattleVector2(2f, 3f))));

            world.FlushSpawnCombatantCommands(events, sequence, new BattleTick(0));

            Assert.IsTrue(world.TryFindEntity(new UnitId(1), out EntityId entity));
            Assert.AreEqual(new UnitId(1), world.UnitComponents.Get(entity).UnitId);
            Assert.AreEqual(new TeamId(1), world.TeamComponents.Get(entity).TeamId);
            Assert.AreEqual(new BattleVector2(2f, 3f), world.PositionComponents.Get(entity).Position);
            Assert.AreEqual(BattleScalar.FromFloat(0.25f), world.PositionComponents.Get(entity).Radius);
            Assert.AreEqual(10, world.HealthComponents.Get(entity).Current);
            Assert.AreEqual(10, BattleStatResolver.ResolveMaxHealth(world, entity));
            Assert.AreEqual(LifeState.Alive, world.LifeStateComponents.Get(entity).State);
            Assert.AreEqual(default(EntityId), world.TargetComponents.Get(entity).Target);
            Assert.AreEqual(new BattleVector2(1f, 0f), world.FacingComponents.Get(entity).Direction);
            Assert.AreEqual(10, world.BaseStatsComponents.Get(entity).Stats.RequireInt(BattleStatId.MaxHealth, "test"));
            Assert.AreEqual(BattleScalar.FromFloat(1f), BattleStatResolver.ResolveScalar(world, entity, BattleStatId.MoveSpeed));
            AbilityComponent abilities = world.AbilityComponents.Get(entity);
            Assert.AreEqual(2, abilities.Abilities.Count);
            Assert.AreEqual("basic-slash", abilities.Abilities[0].Id);
            Assert.AreEqual(BattleScalar.FromFloat(1.5f), abilities.Abilities[0].Range);
            Assert.AreEqual(1, AbilityEffects(abilities.Abilities[0]).Count);
            Assert.AreEqual(BattleEffectType.Damage, AbilityEffects(abilities.Abilities[0])[0].Type);
            Assert.AreEqual(2, AbilityEffects(abilities.Abilities[0])[0].Amount);
            Assert.AreEqual(3, abilities.Abilities[0].CooldownTicks);
            Assert.AreEqual(0, abilities.Abilities[0].CooldownRemainingTicks);
            Assert.AreEqual("slash", abilities.Abilities[1].Id);
            Assert.AreEqual(BattleScalar.FromFloat(1.25f), abilities.Abilities[1].Range);
            Assert.AreEqual(2, AbilityEffects(abilities.Abilities[1]).Count);
            Assert.AreEqual(BattleEffectType.Damage, AbilityEffects(abilities.Abilities[1])[0].Type);
            Assert.AreEqual(5, AbilityEffects(abilities.Abilities[1])[0].Amount);
            Assert.AreEqual(4, abilities.Abilities[1].CooldownTicks);
            Assert.AreEqual(0, abilities.Abilities[1].CooldownRemainingTicks);
            Assert.AreEqual(BattleEffectType.ApplyStatus, AbilityEffects(abilities.Abilities[1])[1].Type);
            Assert.AreEqual("burn", AbilityEffects(abilities.Abilities[1])[1].Status.Id);
            Assert.AreEqual(StatusPolarity.Debuff, AbilityEffects(abilities.Abilities[1])[1].Status.Polarity);
            Assert.AreEqual(3, AbilityEffects(abilities.Abilities[1])[1].Status.DurationTicks);
            Assert.AreEqual(1, AbilityEffects(abilities.Abilities[1])[1].Status.TickIntervalTicks);
            Assert.AreEqual(2, AbilityEffects(abilities.Abilities[1])[1].Status.PeriodicDamage);
            Assert.IsFalse(abilities.Abilities is AbilityState[]);

            EventStream<BattleEvent> stream = events.AsStream();
            Assert.AreEqual(1, stream.Count);
            Assert.AreEqual(BattleEventType.UnitSpawned, stream[0].Type);
            Assert.AreEqual(new UnitId(1), stream[0].UnitId);
            Assert.AreEqual(new TeamId(1), stream[0].TeamId);
            Assert.AreEqual("melee", stream[0].DefinitionId);
            Assert.AreEqual(new BattleVector2(2f, 3f), stream[0].Position);
        }

        [Test]
        public void FlushSpawnCombatantCommands_DuplicateUnitIdFailsBeforeMutatingWorld()
        {
            var world = new BattleWorld();
            var events = new EventBuffer<BattleEvent>();
            var sequence = new EventSequence();

            world.CommandBuffer.SpawnCombatant(new SpawnCombatantCommand(
                new UnitId(1),
                SpawnData(new TeamId(1), new BattleVector2(2f, 3f))));
            world.CommandBuffer.SpawnCombatant(new SpawnCombatantCommand(
                new UnitId(1),
                SpawnData(new TeamId(2), new BattleVector2(4f, 5f))));

            Exception exception = Assert.Catch(() => world.FlushSpawnCombatantCommands(events, sequence, new BattleTick(0)));

            Assert.IsFalse(world.TryFindEntity(new UnitId(1), out _));
            Assert.AreEqual(0, world.UnitComponents.Entities.Count);
            Assert.AreEqual(0, events.AsStream().Count);
            Assert.IsInstanceOf<InvalidOperationException>(exception);
        }

        [Test]
        public void FlushSpawnCombatantCommands_CopiesAbilityStatusModifiersIntoRuntimeState()
        {
            var world = new BattleWorld();
            var events = new EventBuffer<BattleEvent>();
            var sequence = new EventSequence();
            BattleModifierData modifier = BattleModifierData.Damage(
                BattleDamageModifierStat.DamageTaken,
                BattleModifierOperation.PercentAdd,
                BattleScalar.FromFloat(0.25f));

            world.CommandBuffer.SpawnCombatant(new SpawnCombatantCommand(
                new UnitId(1),
                new CombatantSpawnData(
                    new TeamId(1),
                    "mage",
                    new BattleVector2(0f, 0f),
                    maxHealth: 10,
                    radius: BattleScalar.FromFloat(0.25f),
                    moveSpeed: BattleScalar.Zero,
                    basicAbility: BasicAbility(),
                    abilities: new[]
                    {
                        TestCombatants.AbilitySpawn(
                            "hex",
                            2f,
                            0,
                            3,
                            new[]
                            {
                                new StatusApplicationData(
                                    "vulnerable",
                                    StatusPolarity.Debuff,
                                    durationTicks: 3,
                                    tickIntervalTicks: 1,
                                    periodicDamage: 0,
                                    modifiers: new[] { modifier },
                                    triggers: new BattleTriggerData[0])
                            },
                            new ProjectileEmitterSpawnData[0])
                    })));

            world.FlushSpawnCombatantCommands(events, sequence, new BattleTick(0));
            world.TryFindEntity(new UnitId(1), out EntityId entity);

            StatusApplicationData status = AbilityEffects(world.AbilityComponents.Get(entity).Abilities[1])[0].Status;
            Assert.AreEqual(1, status.Modifiers.Count);
            Assert.AreEqual(BattleModifierTarget.Damage, status.Modifiers[0].Target);
            Assert.AreEqual(BattleDamageModifierStat.DamageTaken, status.Modifiers[0].DamageStat);
            Assert.AreEqual(BattleModifierOperation.PercentAdd, status.Modifiers[0].Operation);
            Assert.AreEqual(BattleScalar.FromFloat(0.25f), status.Modifiers[0].Value);
        }

        [Test]
        public void FlushSpawnCombatantCommands_CopiesAbilityStatusTriggers()
        {
            var world = new BattleWorld();
            var mark = new StatusApplicationData(
                "mark",
                StatusPolarity.Debuff,
                durationTicks: 2,
                tickIntervalTicks: 1,
                periodicDamage: 0,
                modifiers: new BattleModifierData[0],
                triggers: new BattleTriggerData[0]);
            var thornsTrigger = new BattleTriggerData(
                BattleTriggerTiming.AfterDamageTaken,
                new[]
                {
                    BattleReactionEffectData.Create(BattleReactionTarget.Source, BattleEffectData.ApplyStatus(mark))
                });
            var thorns = new StatusApplicationData(
                "thorns",
                StatusPolarity.Buff,
                durationTicks: 4,
                tickIntervalTicks: 1,
                periodicDamage: 0,
                modifiers: new BattleModifierData[0],
                triggers: new[] { thornsTrigger });

            world.CommandBuffer.SpawnCombatant(new SpawnCombatantCommand(
                new UnitId(1),
                new CombatantSpawnData(
                    new TeamId(1),
                    "unit",
                    new BattleVector2(0f, 0f),
                    maxHealth: 10,
                    radius: BattleScalar.FromFloat(0.25f),
                    moveSpeed: BattleScalar.Zero,
                    basicAbility: BasicAbility(),
                    abilities: new[]
                    {
                        TestCombatants.AbilitySpawn("guard", 1f, 0, 1, new[] { thorns }, new ProjectileEmitterSpawnData[0])
                    })));

            world.FlushSpawnCombatantCommands(new EventBuffer<BattleEvent>(), new EventSequence(), new BattleTick(0));
            world.TryFindEntity(new UnitId(1), out EntityId entity);

            StatusApplicationData stored = AbilityEffects(world.AbilityComponents.Get(entity).Abilities[1])[0].Status;
            Assert.AreEqual(1, stored.Triggers.Count);
            Assert.AreEqual(BattleTriggerTiming.AfterDamageTaken, stored.Triggers[0].Timing);
            Assert.AreEqual(BattleEffectType.ApplyStatus, stored.Triggers[0].Effects[0].Effect.Type);
            Assert.AreEqual("mark", stored.Triggers[0].Effects[0].Effect.Status.Id);
        }

        private static CombatantSpawnData SpawnData(TeamId teamId, BattleVector2 position)
        {
            return new CombatantSpawnData(
                teamId,
                "melee",
                position,
                maxHealth: 10,
                radius: BattleScalar.FromFloat(0.25f),
                moveSpeed: BattleScalar.One,
                basicAbility: TestCombatants.AbilitySpawn(
                    "basic-slash",
                    range: 1.5f,
                    damage: 2,
                    cooldownTicks: 3,
                    appliedStatuses: new StatusApplicationData[0],
                    projectileEmitters: new ProjectileEmitterSpawnData[0]),
                abilities: new[]
                {
                    TestCombatants.AbilitySpawn(
                        "slash",
                        range: 1.25f,
                        damage: 5,
                        cooldownTicks: 4,
                        appliedStatuses: new[]
                        {
                            new StatusApplicationData("burn", StatusPolarity.Debuff, durationTicks: 3, tickIntervalTicks: 1, periodicDamage: 2, modifiers: new BattleModifierData[0], triggers: new BattleTriggerData[0])
                        },
                        projectileEmitters: new ProjectileEmitterSpawnData[0])
                });
        }

        private static AbilitySpawnData BasicAbility()
        {
            return TestCombatants.AbilitySpawn(
                "basic-slash",
                range: 1.5f,
                damage: 1,
                cooldownTicks: 2,
                appliedStatuses: new StatusApplicationData[0],
                projectileEmitters: new ProjectileEmitterSpawnData[0]);
        }

        private static IReadOnlyList<BattleEffectData> AbilityEffects(AbilityState ability)
        {
            return ability.EffectFrames[0].Effects;
        }
    }
}
