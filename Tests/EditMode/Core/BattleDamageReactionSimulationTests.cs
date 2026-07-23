using Combat.Core.Battle;
using Combat.Foundation.Events;
using NUnit.Framework;

namespace Combat.Tests.Core
{
    public sealed class BattleDamageReactionSimulationTests
    {
        [Test]
        public void AfterDamageTaken_DamageSourceDealsThornsDamage()
        {
            var world = new BattleWorld();
            SpawnCombatant(world, new UnitId(1), new TeamId(1), maxHealth: 10);
            SpawnCombatant(world, new UnitId(2), new TeamId(2), maxHealth: 10);
            world.TryFindEntity(new UnitId(1), out EntityId attacker);
            world.TryFindEntity(new UnitId(2), out EntityId defender);
            world.StatusComponents.Set(defender, new StatusComponent(new[]
            {
                Status("thorns", defender, BattleTriggerTiming.AfterDamageTaken, BattleReactionEffectInstance.Create(BattleReactionTarget.Source, BattleEffectData.Damage(3)))
            }));
            var events = new EventBuffer<BattleEvent>();

            world.CommandBuffer.QueueEffect(BattleEffectCommand.Damage(attacker, defender, 4));
            world.FlushEffectCommands(events, new EventSequence(), new BattleTick(1));

            Assert.AreEqual(6, world.HealthComponents.Get(defender).Current);
            Assert.AreEqual(7, world.HealthComponents.Get(attacker).Current);
            EventStream<BattleEvent> stream = events.AsStream();
            Assert.AreEqual(2, stream.Count);
            AssertDamage(stream[0], new UnitId(1), new UnitId(2), 4);
            AssertDamage(stream[1], new UnitId(2), new UnitId(1), 3);
        }

        [Test]
        public void AfterDamageDealt_ApplyStatusTargetAppliesMark()
        {
            var world = new BattleWorld();
            SpawnCombatant(world, new UnitId(1), new TeamId(1), maxHealth: 10);
            SpawnCombatant(world, new UnitId(2), new TeamId(2), maxHealth: 10);
            world.TryFindEntity(new UnitId(1), out EntityId attacker);
            world.TryFindEntity(new UnitId(2), out EntityId defender);
            StatusApplicationData poison = StatusApplication("poison", StatusPolarity.Debuff);
            world.StatusComponents.Set(attacker, new StatusComponent(new[]
            {
                Status("venom-edge", attacker, BattleTriggerTiming.AfterDamageDealt, BattleReactionEffectInstance.Create(BattleReactionTarget.Target, BattleEffectData.ApplyStatus(poison)))
            }));
            var events = new EventBuffer<BattleEvent>();

            world.CommandBuffer.QueueEffect(BattleEffectCommand.Damage(attacker, defender, 2));
            world.FlushEffectCommands(events, new EventSequence(), new BattleTick(1));

            Assert.IsTrue(world.StatusComponents.Has(defender));
            Assert.AreEqual("poison", world.StatusComponents.Get(defender).Statuses[0].Id);
            EventStream<BattleEvent> stream = events.AsStream();
            Assert.AreEqual(2, stream.Count);
            AssertDamage(stream[0], new UnitId(1), new UnitId(2), 2);
            Assert.AreEqual(BattleEventType.StatusApplied, stream[1].Type);
            Assert.AreEqual(new UnitId(1), stream[1].SourceUnitId);
            Assert.AreEqual(new UnitId(2), stream[1].TargetUnitId);
            Assert.AreEqual("poison", stream[1].StatusId);
        }

        [Test]
        public void AfterDamageDealt_AllTriggerConditionsMustPass()
        {
            var world = new BattleWorld();
            SpawnCombatant(world, new UnitId(1), new TeamId(1), maxHealth: 10);
            SpawnCombatant(world, new UnitId(2), new TeamId(2), maxHealth: 10);
            world.TryFindEntity(new UnitId(1), out EntityId attacker);
            world.TryFindEntity(new UnitId(2), out EntityId defender);
            world.HealthComponents.Set(attacker, new HealthComponent(current: 2));
            world.StatusComponents.Set(attacker, new StatusComponent(new[]
            {
                Status(
                    "execute",
                    attacker,
                    BattleTriggerTiming.AfterDamageDealt,
                    new BattleConditionGroup(
                        BattleConditionMatchMode.All,
                        new[]
                        {
                            BattleConditionDefinition.Compare(
                                BattleConditionOperandDefinition.HealthPercent(BattleConditionSubject.Source),
                                BattleConditionComparison.LessOrEqual,
                                BattleConditionOperandDefinition.LiteralPercentBasisPoints(2000)),
                            BattleConditionDefinition.Compare(
                                BattleConditionOperandDefinition.StatusCount(BattleConditionSubject.Target, BattleStatusConditionFilterDefinition.Polarity(StatusPolarity.Debuff)),
                                BattleConditionComparison.GreaterOrEqual,
                                BattleConditionOperandDefinition.LiteralInt(1))
                        }),
                    BattleReactionEffectInstance.Create(BattleReactionTarget.Target, BattleEffectData.Damage(2)))
            }));
            world.StatusComponents.Set(defender, new StatusComponent(new[]
            {
                Status("poison", StatusPolarity.Debuff, defender)
            }));
            var events = new EventBuffer<BattleEvent>();

            world.CommandBuffer.QueueEffect(BattleEffectCommand.Damage(attacker, defender, 1));
            world.FlushEffectCommands(events, new EventSequence(), new BattleTick(1));

            Assert.AreEqual(7, world.HealthComponents.Get(defender).Current);
            EventStream<BattleEvent> stream = events.AsStream();
            Assert.AreEqual(2, stream.Count);
            AssertDamage(stream[0], new UnitId(1), new UnitId(2), 1);
            AssertDamage(stream[1], new UnitId(1), new UnitId(2), 2);
        }

        [Test]
        public void AfterDamageDealt_CompiledConditionProgramStillGatesReaction()
        {
            var world = new BattleWorld();
            SpawnCombatant(world, new UnitId(1), new TeamId(1), maxHealth: 10);
            SpawnCombatant(world, new UnitId(2), new TeamId(2), maxHealth: 10);
            world.TryFindEntity(new UnitId(1), out EntityId attacker);
            world.TryFindEntity(new UnitId(2), out EntityId defender);
            world.HealthComponents.Set(attacker, new HealthComponent(current: 9));
            world.StatusComponents.Set(attacker, new StatusComponent(new[]
            {
                Status(
                    "execute",
                    attacker,
                    BattleTriggerTiming.AfterDamageDealt,
                    new BattleConditionGroup(
                        BattleConditionMatchMode.All,
                        new[]
                        {
                            BattleConditionDefinition.Compare(
                                BattleConditionOperandDefinition.HealthPercent(BattleConditionSubject.Source),
                                BattleConditionComparison.LessOrEqual,
                                BattleConditionOperandDefinition.LiteralPercentBasisPoints(2000))
                        }),
                    BattleReactionEffectInstance.Create(BattleReactionTarget.Target, BattleEffectData.Damage(2)))
            }));
            var events = new EventBuffer<BattleEvent>();

            world.CommandBuffer.QueueEffect(BattleEffectCommand.Damage(attacker, defender, 1));
            world.FlushEffectCommands(events, new EventSequence(), new BattleTick(1));

            Assert.AreEqual(9, world.HealthComponents.Get(defender).Current);
            Assert.AreEqual(1, events.AsStream().Count);
        }

        [Test]
        public void AfterDamageTaken_AnyTriggerConditionCanPass()
        {
            var world = new BattleWorld();
            SpawnCombatant(world, new UnitId(1), new TeamId(1), maxHealth: 10);
            SpawnCombatant(world, new UnitId(2), new TeamId(2), maxHealth: 10);
            world.TryFindEntity(new UnitId(1), out EntityId attacker);
            world.TryFindEntity(new UnitId(2), out EntityId defender);
            world.StatusComponents.Set(attacker, new StatusComponent(new[]
            {
                Status("burning", StatusPolarity.Debuff, attacker)
            }));
            world.StatusComponents.Set(defender, new StatusComponent(new[]
            {
                Status(
                    "punish",
                    defender,
                    BattleTriggerTiming.AfterDamageTaken,
                    new BattleConditionGroup(
                        BattleConditionMatchMode.Any,
                        new[]
                        {
                            BattleConditionDefinition.Compare(
                                BattleConditionOperandDefinition.HealthPercent(BattleConditionSubject.Owner),
                                BattleConditionComparison.LessOrEqual,
                                BattleConditionOperandDefinition.LiteralPercentBasisPoints(2000)),
                            BattleConditionDefinition.Compare(
                                BattleConditionOperandDefinition.StatusCount(BattleConditionSubject.Source, BattleStatusConditionFilterDefinition.StatusId("burning")),
                                BattleConditionComparison.GreaterOrEqual,
                                BattleConditionOperandDefinition.LiteralInt(1))
                        }),
                    BattleReactionEffectInstance.Create(BattleReactionTarget.Source, BattleEffectData.Damage(2)))
            }));
            var events = new EventBuffer<BattleEvent>();

            world.CommandBuffer.QueueEffect(BattleEffectCommand.Damage(attacker, defender, 1));
            world.FlushEffectCommands(events, new EventSequence(), new BattleTick(1));

            Assert.AreEqual(9, world.HealthComponents.Get(defender).Current);
            Assert.AreEqual(8, world.HealthComponents.Get(attacker).Current);
            EventStream<BattleEvent> stream = events.AsStream();
            Assert.AreEqual(2, stream.Count);
            AssertDamage(stream[0], new UnitId(1), new UnitId(2), 1);
            AssertDamage(stream[1], new UnitId(2), new UnitId(1), 2);
        }

        [Test]
        public void AfterEnemyKilled_AppliesStackingBuffToKillerInSameFlush()
        {
            var world = new BattleWorld();
            SpawnCombatant(world, new UnitId(1), new TeamId(1), maxHealth: 10);
            SpawnCombatant(world, new UnitId(2), new TeamId(2), maxHealth: 4);
            SpawnCombatant(world, new UnitId(3), new TeamId(2), maxHealth: 10);
            world.TryFindEntity(new UnitId(1), out EntityId killer);
            world.TryFindEntity(new UnitId(2), out EntityId firstVictim);
            world.TryFindEntity(new UnitId(3), out EntityId secondVictim);
            StatusApplicationData attackStack = new StatusApplicationData(
                "kill-attack-stack",
                StatusPolarity.Buff,
                durationTicks: 5,
                tickIntervalTicks: 5,
                periodicDamage: 0,
                modifiers: new[]
                {
                    BattleModifierData.Damage(BattleDamageModifierStat.DamageDealt, BattleModifierOperation.Flat, BattleScalar.FromInt(1))
                },
                triggers: new BattleTriggerData[0],
                maxStacks: 5);
            world.StatusComponents.Set(killer, new StatusComponent(new[]
            {
                Status(
                    "kill-trigger",
                    killer,
                    BattleTriggerTiming.AfterEnemyKilled,
                    BattleReactionEffectInstance.Create(BattleReactionTarget.Self, BattleEffectData.ApplyStatus(attackStack)))
            }));
            var events = new EventBuffer<BattleEvent>();
            var sequence = new EventSequence();

            world.CommandBuffer.QueueEffect(BattleEffectCommand.Damage(killer, firstVictim, 4));
            world.FlushEffectCommands(events, sequence, new BattleTick(1));
            world.CommandBuffer.QueueEffect(BattleEffectCommand.Damage(killer, secondVictim, 1));
            world.FlushEffectCommands(events, sequence, new BattleTick(2));

            StatusInstance stack = FindStatus(world.StatusComponents.Get(killer), "kill-attack-stack");
            Assert.AreEqual(1, stack.StackCount);
            Assert.AreEqual(5, stack.DurationRemainingTicks);
            Assert.AreEqual(8, world.HealthComponents.Get(secondVictim).Current);
        }

        [Test]
        public void AfterEnemyKilled_StackingAttackBuffCapsAtFiveAndRefreshesDuration()
        {
            var world = new BattleWorld();
            SpawnCombatant(world, new UnitId(1), new TeamId(1), maxHealth: 10);
            for (var unitId = 2; unitId <= 7; unitId++)
            {
                SpawnCombatant(world, new UnitId(unitId), new TeamId(2), maxHealth: 1);
            }
            SpawnCombatant(world, new UnitId(8), new TeamId(2), maxHealth: 6);

            world.TryFindEntity(new UnitId(1), out EntityId killer);
            StatusApplicationData attackStack = new StatusApplicationData(
                "kill-attack-stack",
                StatusPolarity.Buff,
                durationTicks: 5,
                tickIntervalTicks: 5,
                periodicDamage: 0,
                modifiers: new[]
                {
                    BattleModifierData.Damage(BattleDamageModifierStat.DamageDealt, BattleModifierOperation.Flat, BattleScalar.FromInt(1))
                },
                triggers: new BattleTriggerData[0],
                maxStacks: 5);
            world.StatusComponents.Set(killer, new StatusComponent(new[]
            {
                Status(
                    "kill-trigger",
                    killer,
                    BattleTriggerTiming.AfterEnemyKilled,
                    BattleReactionEffectInstance.Create(BattleReactionTarget.Self, BattleEffectData.ApplyStatus(attackStack)))
            }));
            var events = new EventBuffer<BattleEvent>();
            var sequence = new EventSequence();

            for (var victimUnitId = 2; victimUnitId <= 7; victimUnitId++)
            {
                world.TryFindEntity(new UnitId(victimUnitId), out EntityId victim);
                world.CommandBuffer.QueueEffect(BattleEffectCommand.Damage(killer, victim, 1));
                world.FlushEffectCommands(events, sequence, new BattleTick(victimUnitId - 1));

                StatusInstance stack = FindStatus(world.StatusComponents.Get(killer), "kill-attack-stack");
                Assert.AreEqual(victimUnitId <= 6 ? victimUnitId - 1 : 5, stack.StackCount);
                Assert.AreEqual(5, stack.DurationRemainingTicks);

                StatusInstance aged = stack.WithTiming(durationRemainingTicks: 2, ticksUntilNextPeriodicEffect: 2);
                StatusComponent component = world.StatusComponents.Get(killer);
                world.StatusComponents.Set(killer, ReplaceStatus(component, aged));
            }

            StatusInstance cappedStack = FindStatus(world.StatusComponents.Get(killer), "kill-attack-stack");
            Assert.AreEqual(5, cappedStack.StackCount);
            Assert.AreEqual(2, cappedStack.DurationRemainingTicks);

            world.TryFindEntity(new UnitId(8), out EntityId finalTarget);
            world.CommandBuffer.QueueEffect(BattleEffectCommand.Damage(killer, finalTarget, 1));
            world.FlushEffectCommands(events, sequence, new BattleTick(7));

            Assert.AreEqual(0, world.HealthComponents.Get(finalTarget).Current);
            EventStream<BattleEvent> stream = events.AsStream();
            AssertDamage(stream[stream.Count - 3], new UnitId(1), new UnitId(8), 6);
            StatusInstance refreshed = FindStatus(world.StatusComponents.Get(killer), "kill-attack-stack");
            Assert.AreEqual(5, refreshed.StackCount);
            Assert.AreEqual(5, refreshed.DurationRemainingTicks);
        }

        [Test]
        public void ReactionDamage_DoesNotTriggerSecondLayerReactions()
        {
            var world = new BattleWorld();
            SpawnCombatant(world, new UnitId(1), new TeamId(1), maxHealth: 10);
            SpawnCombatant(world, new UnitId(2), new TeamId(2), maxHealth: 10);
            world.TryFindEntity(new UnitId(1), out EntityId attacker);
            world.TryFindEntity(new UnitId(2), out EntityId defender);
            world.StatusComponents.Set(attacker, new StatusComponent(new[]
            {
                Status("attacker-thorns", attacker, BattleTriggerTiming.AfterDamageTaken, BattleReactionEffectInstance.Create(BattleReactionTarget.Source, BattleEffectData.Damage(3)))
            }));
            world.StatusComponents.Set(defender, new StatusComponent(new[]
            {
                Status("defender-thorns", defender, BattleTriggerTiming.AfterDamageTaken, BattleReactionEffectInstance.Create(BattleReactionTarget.Source, BattleEffectData.Damage(3)))
            }));
            var events = new EventBuffer<BattleEvent>();

            world.CommandBuffer.QueueEffect(BattleEffectCommand.Damage(attacker, defender, 4));
            world.FlushEffectCommands(events, new EventSequence(), new BattleTick(1));

            Assert.AreEqual(6, world.HealthComponents.Get(defender).Current);
            Assert.AreEqual(7, world.HealthComponents.Get(attacker).Current);
            EventStream<BattleEvent> stream = events.AsStream();
            Assert.AreEqual(2, stream.Count);
            AssertDamage(stream[0], new UnitId(1), new UnitId(2), 4);
            AssertDamage(stream[1], new UnitId(2), new UnitId(1), 3);
            Assert.AreEqual(BattleEffectSourceKind.Unknown, stream[0].EffectSourceKind);
            Assert.AreEqual(BattleEffectSourceKind.Reaction, stream[1].EffectSourceKind);
            Assert.AreEqual(BattleEffectType.Damage, stream[1].EffectType);
        }

        [Test]
        public void AfterEnemyKilled_SuppressedReactionDamageDoesNotTriggerKillBuff()
        {
            var world = new BattleWorld();
            SpawnCombatant(world, new UnitId(1), new TeamId(1), maxHealth: 10);
            SpawnCombatant(world, new UnitId(2), new TeamId(2), maxHealth: 10);
            world.TryFindEntity(new UnitId(1), out EntityId attacker);
            world.TryFindEntity(new UnitId(2), out EntityId defender);
            StatusApplicationData attackStack = new StatusApplicationData(
                "kill-attack-stack",
                StatusPolarity.Buff,
                durationTicks: 5,
                tickIntervalTicks: 5,
                periodicDamage: 0,
                modifiers: new[]
                {
                    BattleModifierData.Damage(BattleDamageModifierStat.DamageDealt, BattleModifierOperation.Flat, BattleScalar.FromInt(1))
                },
                triggers: new BattleTriggerData[0],
                maxStacks: 5);
            world.StatusComponents.Set(defender, new StatusComponent(new[]
            {
                Status(
                    "thorns",
                    defender,
                    BattleTriggerTiming.AfterDamageTaken,
                    BattleReactionEffectInstance.Create(BattleReactionTarget.Source, BattleEffectData.Damage(10))),
                Status(
                    "kill-trigger",
                    defender,
                    BattleTriggerTiming.AfterEnemyKilled,
                    BattleReactionEffectInstance.Create(BattleReactionTarget.Self, BattleEffectData.ApplyStatus(attackStack)))
            }));
            var events = new EventBuffer<BattleEvent>();

            world.CommandBuffer.QueueEffect(BattleEffectCommand.Damage(attacker, defender, 1));
            world.FlushEffectCommands(events, new EventSequence(), new BattleTick(1));

            Assert.AreEqual(0, world.HealthComponents.Get(attacker).Current);
            Assert.AreEqual(LifeState.Dead, world.LifeStateComponents.Get(attacker).State);
            Assert.IsFalse(HasStatus(world.StatusComponents.Get(defender), "kill-attack-stack"));
            EventStream<BattleEvent> stream = events.AsStream();
            Assert.AreEqual(3, stream.Count);
            AssertDamage(stream[0], new UnitId(1), new UnitId(2), 1);
            AssertDamage(stream[1], new UnitId(2), new UnitId(1), 10);
            Assert.AreEqual(BattleEventType.UnitDied, stream[2].Type);
            Assert.AreEqual(new UnitId(1), stream[2].UnitId);
        }

        [Test]
        public void FlushEffectCommands_ReactionAreaDamageSuppressesNestedReactions()
        {
            var world = new BattleWorld();
            SpawnCombatant(world, new UnitId(1), new TeamId(1), new BattleVector2(0f, 0f), maxHealth: 10);
            SpawnCombatant(world, new UnitId(2), new TeamId(2), new BattleVector2(0.5f, 0f), maxHealth: 10);
            SpawnCombatant(world, new UnitId(3), new TeamId(1), new BattleVector2(0.75f, 0f), maxHealth: 10);
            world.TryFindEntity(new UnitId(1), out EntityId attacker);
            world.TryFindEntity(new UnitId(2), out EntityId defender);
            world.TryFindEntity(new UnitId(3), out EntityId ally);
            world.StatusComponents.Set(attacker, new StatusComponent(new[]
            {
                Status(
                    "counter",
                    attacker,
                    BattleTriggerTiming.AfterDamageTaken,
                    BattleReactionEffectInstance.Create(BattleReactionTarget.Source, BattleEffectData.Damage(1)))
            }));
            world.StatusComponents.Set(defender, new StatusComponent(new[]
            {
                Status(
                    "retaliation",
                    defender,
                    BattleTriggerTiming.AfterDamageTaken,
                    BattleReactionEffectInstance.Create(
                        BattleReactionTarget.Source,
                        BattleEffectData.CreateAreaEffect(new AreaEffectData(
                            BattleScalar.FromFloat(1.5f),
                            AreaEffectTargetFilter.Enemies,
                            new[] { BattleEffectData.Damage(2) }))))
            }));
            var events = new EventBuffer<BattleEvent>();
            var sequence = new EventSequence();

            world.CommandBuffer.QueueEffect(BattleEffectCommand.Damage(attacker, defender, 3));
            world.FlushEffectCommands(events, sequence, new BattleTick(1));
            world.FlushEffectCommands(events, sequence, new BattleTick(2));

            EventStream<BattleEvent> stream = events.AsStream();
            Assert.AreEqual(3, CountEvents(stream, BattleEventType.DamageApplied));
            AssertDamage(stream[0], new UnitId(1), new UnitId(2), 3);
            AssertDamage(stream[1], new UnitId(2), new UnitId(1), 2);
            AssertDamage(stream[2], new UnitId(2), new UnitId(3), 2);
            Assert.AreEqual(7, world.HealthComponents.Get(defender).Current);
            Assert.AreEqual(8, world.HealthComponents.Get(attacker).Current);
            Assert.AreEqual(8, world.HealthComponents.Get(ally).Current);
            Assert.AreEqual(0, world.CommandBuffer.ReactionEffectCommands.Count);
            Assert.AreEqual(0, world.CommandBuffer.EffectCommands.Count);
        }

        [Test]
        public void PendingDeathTarget_CanRunAfterDamageTakenBeforeDeathCheck()
        {
            var world = new BattleWorld();
            SpawnCombatant(world, new UnitId(1), new TeamId(1), maxHealth: 10);
            SpawnCombatant(world, new UnitId(2), new TeamId(2), maxHealth: 10);
            world.TryFindEntity(new UnitId(1), out EntityId attacker);
            world.TryFindEntity(new UnitId(2), out EntityId defender);
            world.StatusComponents.Set(defender, new StatusComponent(new[]
            {
                Status("thorns", defender, BattleTriggerTiming.AfterDamageTaken, BattleReactionEffectInstance.Create(BattleReactionTarget.Source, BattleEffectData.Damage(3)))
            }));
            var events = new EventBuffer<BattleEvent>();

            world.CommandBuffer.QueueEffect(BattleEffectCommand.Damage(attacker, defender, 10));
            world.FlushEffectCommands(events, new EventSequence(), new BattleTick(1));

            Assert.AreEqual(0, world.HealthComponents.Get(defender).Current);
            Assert.AreEqual(LifeState.Dead, world.LifeStateComponents.Get(defender).State);
            Assert.AreEqual(7, world.HealthComponents.Get(attacker).Current);
            EventStream<BattleEvent> stream = events.AsStream();
            Assert.AreEqual(3, stream.Count);
            AssertDamage(stream[0], new UnitId(1), new UnitId(2), 10);
            AssertDamage(stream[1], new UnitId(2), new UnitId(1), 3);
            Assert.AreEqual(BattleEventType.UnitDied, stream[2].Type);
            Assert.AreEqual(new UnitId(2), stream[2].UnitId);
        }

        [Test]
        public void SuppressedDamage_StillAppliesModifiersAndDeathCheck()
        {
            var world = new BattleWorld();
            SpawnCombatant(world, new UnitId(1), new TeamId(1), maxHealth: 10);
            SpawnCombatant(world, new UnitId(2), new TeamId(2), maxHealth: 10);
            world.TryFindEntity(new UnitId(1), out EntityId attacker);
            world.TryFindEntity(new UnitId(2), out EntityId defender);
            world.StatusComponents.Set(defender, new StatusComponent(new[]
            {
                Status("vulnerable", defender, BattleDamageModifierStat.DamageTaken, BattleModifierOperation.Flat, BattleScalar.FromInt(5))
            }));
            var events = new EventBuffer<BattleEvent>();

            world.CommandBuffer.QueueEffect(BattleEffectCommand.Damage(attacker, defender, 5, BattleEffectTriggerPolicy.SuppressReactions));
            world.FlushEffectCommands(events, new EventSequence(), new BattleTick(1));

            Assert.AreEqual(0, world.HealthComponents.Get(defender).Current);
            Assert.AreEqual(LifeState.Dead, world.LifeStateComponents.Get(defender).State);
            EventStream<BattleEvent> stream = events.AsStream();
            Assert.AreEqual(2, stream.Count);
            AssertDamage(stream[0], new UnitId(1), new UnitId(2), 10);
            Assert.AreEqual(BattleEventType.UnitDied, stream[1].Type);
            Assert.AreEqual(new UnitId(2), stream[1].UnitId);
        }

        private static void SpawnCombatant(BattleWorld world, UnitId unitId, TeamId teamId, int maxHealth)
        {
            SpawnCombatant(world, unitId, teamId, new BattleVector2(0f, 0f), maxHealth);
        }

        private static void SpawnCombatant(BattleWorld world, UnitId unitId, TeamId teamId, BattleVector2 position, int maxHealth)
        {
            world.CommandBuffer.SpawnCombatant(new SpawnCombatantCommand(
                unitId,
                new CombatantSpawnData(
                    teamId,
                    "unit",
                    position,
                    maxHealth,
                    radius: BattleScalar.FromFloat(0.25f),
                    moveSpeed: BattleScalar.Zero,
                    basicAbility: BasicAbility(),
                    abilities: new AbilitySpawnData[0])));
            world.FlushSpawnCombatantCommands(new EventBuffer<BattleEvent>(), new EventSequence(), new BattleTick(0));
        }

        private static AbilitySpawnData BasicAbility()
        {
            return TestCombatants.AbilitySpawn("basic-attack", 1f, 1, 1);
        }

        private static int CountEvents(EventStream<BattleEvent> stream, BattleEventType type)
        {
            var count = 0;
            for (var i = 0; i < stream.Count; i++)
            {
                if (stream[i].Type == type)
                {
                    count++;
                }
            }

            return count;
        }

        private static StatusInstance Status(string id, EntityId source, BattleTriggerTiming timing, BattleReactionEffectInstance effect)
        {
            return Status(id, source, timing, BattleConditionGroup.Empty, effect);
        }

        private static StatusInstance Status(string id, EntityId source, BattleTriggerTiming timing, BattleConditionGroup conditions, BattleReactionEffectInstance effect)
        {
            return new StatusInstance(
                id,
                StatusPolarity.Buff,
                source,
                durationRemainingTicks: 3,
                tickIntervalTicks: 1,
                ticksUntilNextPeriodicEffect: 1,
                periodicDamage: 0,
                modifiers: new BattleModifierInstance[0],
                triggers: new[] { new BattleTriggerInstance(timing, conditions, new[] { effect }) });
        }

        private static StatusInstance Status(string id, StatusPolarity polarity, EntityId source)
        {
            return new StatusInstance(
                id,
                polarity,
                source,
                durationRemainingTicks: 3,
                tickIntervalTicks: 1,
                ticksUntilNextPeriodicEffect: 1,
                periodicDamage: 0,
                modifiers: new BattleModifierInstance[0],
                triggers: new BattleTriggerInstance[0]);
        }

        private static StatusInstance Status(string id, EntityId source, BattleDamageModifierStat stat, BattleModifierOperation operation, BattleScalar value)
        {
            return new StatusInstance(
                id,
                StatusPolarity.Debuff,
                source,
                durationRemainingTicks: 3,
                tickIntervalTicks: 1,
                ticksUntilNextPeriodicEffect: 1,
                periodicDamage: 0,
                modifiers: new[] { BattleModifierInstance.Damage(stat, operation, value) },
                triggers: new BattleTriggerInstance[0]);
        }

        private static StatusApplicationData StatusApplication(string id, StatusPolarity polarity)
        {
            return new StatusApplicationData(id, polarity, 3, 1, 0, new BattleModifierData[0], new BattleTriggerData[0]);
        }

        private static StatusInstance FindStatus(StatusComponent component, string id)
        {
            for (var i = 0; i < component.Statuses.Count; i++)
            {
                if (component.Statuses[i].Id == id)
                {
                    return component.Statuses[i];
                }
            }

            Assert.Fail("Status not found: " + id);
            return default;
        }

        private static bool HasStatus(StatusComponent component, string id)
        {
            for (var i = 0; i < component.Statuses.Count; i++)
            {
                if (component.Statuses[i].Id == id)
                {
                    return true;
                }
            }

            return false;
        }

        private static StatusComponent ReplaceStatus(StatusComponent component, StatusInstance replacement)
        {
            var statuses = new StatusInstance[component.Statuses.Count];
            for (var i = 0; i < component.Statuses.Count; i++)
            {
                statuses[i] = component.Statuses[i].Id == replacement.Id
                    ? replacement
                    : component.Statuses[i];
            }

            return new StatusComponent(statuses);
        }

        private static void AssertDamage(BattleEvent battleEvent, UnitId expectedSource, UnitId expectedTarget, int expectedAmount)
        {
            Assert.AreEqual(BattleEventType.DamageApplied, battleEvent.Type);
            Assert.AreEqual(expectedSource, battleEvent.SourceUnitId);
            Assert.AreEqual(expectedTarget, battleEvent.TargetUnitId);
            Assert.AreEqual(expectedAmount, battleEvent.Amount);
        }
    }
}
