using System;
using System.Collections.Generic;

namespace Combat.Core.Battle
{
    internal static class ProjectileEmitterSystem
    {
        public static void Run(BattleWorld world, int ticksPerSecond, BattleTick tick)
        {
            if (ticksPerSecond <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(ticksPerSecond));
            }

            IReadOnlyList<EntityId> entities = world.ProjectileEmitterComponents.Entities;
            for (var i = 0; i < entities.Count; i++)
            {
                EntityId entity = entities[i];
                if (!world.ProjectileEmitterComponents.TryGet(entity, out ProjectileEmitterComponent emitter))
                {
                    continue;
                }

                if (tick.Value < emitter.ActivateOnTick.Value)
                {
                    continue;
                }

                if (!TryResolveEmitterPosition(world, emitter, out BattleVector2 position))
                {
                    world.CommandBuffer.DestroyEntity(new DestroyEntityCommand(entity));
                    continue;
                }

                ProjectileEmitterComponent updated = emitter.AnchorMode == ProjectileEmitterAnchorMode.FixedPosition
                    ? emitter
                    : emitter.WithLastResolvedPosition(position);
                int ticksUntilNextFire = updated.TicksUntilNextFire;
                if (ticksUntilNextFire == 0)
                {
                    QueuePatternProjectiles(world, updated, position, ticksPerSecond, tick.Next());
                    ticksUntilNextFire = updated.FireIntervalTicks - 1;
                }
                else
                {
                    ticksUntilNextFire -= 1;
                }

                int durationRemaining = updated.DurationRemainingTicks - 1;
                if (durationRemaining == 0)
                {
                    world.CommandBuffer.DestroyEntity(new DestroyEntityCommand(entity));
                }
                else
                {
                    world.ProjectileEmitterComponents.Set(entity, updated.WithTiming(durationRemaining, ticksUntilNextFire));
                }
            }
        }

        private static bool TryResolveEmitterPosition(BattleWorld world, ProjectileEmitterComponent emitter, out BattleVector2 position)
        {
            if (emitter.AnchorMode == ProjectileEmitterAnchorMode.FollowSource)
            {
                if (!world.IsAliveUnit(emitter.Source) || !world.PositionComponents.TryGet(emitter.Source, out PositionComponent sourcePosition))
                {
                    position = default;
                    return false;
                }

                position = sourcePosition.Position + emitter.AnchorOffset;
                return true;
            }

            position = emitter.LastResolvedPosition + emitter.AnchorOffset;
            return true;
        }

        private static void QueuePatternProjectiles(BattleWorld world, ProjectileEmitterComponent emitter, BattleVector2 position, int ticksPerSecond, BattleTick activateOnTick)
        {
            switch (emitter.Pattern.Type)
            {
                case ProjectilePatternType.Single:
                    QueueProjectile(world, emitter, position, ResolveSingleDirection(world, emitter, position), ticksPerSecond, activateOnTick);
                    break;
                case ProjectilePatternType.Circle:
                    for (var i = 0; i < emitter.Pattern.ProjectileCount; i++)
                    {
                        BattleScalar radians = BattleScalar.TwoPi * BattleScalar.FromInt(i) / BattleScalar.FromInt(emitter.Pattern.ProjectileCount);
                        QueueProjectile(
                            world,
                            emitter,
                            position,
                            new BattleVector2(BattleScalar.Cos(radians), BattleScalar.Sin(radians)),
                            ticksPerSecond,
                            activateOnTick);
                    }

                    break;
                default:
                    throw new InvalidOperationException($"Unsupported projectile pattern: {emitter.Pattern.Type}");
            }
        }

        private static void QueueProjectile(BattleWorld world, ProjectileEmitterComponent emitter, BattleVector2 position, BattleVector2 direction, int ticksPerSecond, BattleTick activateOnTick)
        {
            BattleVector2 velocity = direction * (emitter.ProjectilePayload.Speed / BattleScalar.FromInt(ticksPerSecond));
            world.CommandBuffer.SpawnProjectile(new SpawnProjectileCommand(emitter.Source, emitter.TeamId, position, velocity, emitter.ProjectilePayload, activateOnTick));
        }

        private static BattleVector2 ResolveSingleDirection(BattleWorld world, ProjectileEmitterComponent emitter, BattleVector2 position)
        {
            switch (emitter.Pattern.DirectionMode)
            {
                case ProjectileDirectionMode.FixedDirection:
                    return NormalizeOrRight(emitter.Pattern.Direction);
                case ProjectileDirectionMode.TargetDirection:
                    if (world.IsAliveUnit(emitter.Target)
                        && world.PositionComponents.TryGet(emitter.Target, out PositionComponent targetPosition))
                    {
                        BattleVector2 targetDirection = (targetPosition.Position - position).Normalized;
                        if (targetDirection.SqrMagnitudeScalar > BattleScalar.Zero)
                        {
                            return targetDirection;
                        }
                    }

                    return NormalizeOrRight(emitter.Pattern.Direction);
                default:
                    throw new InvalidOperationException($"Unsupported projectile direction mode: {emitter.Pattern.DirectionMode}");
            }
        }

        private static BattleVector2 NormalizeOrRight(BattleVector2 direction)
        {
            BattleVector2 normalized = direction.Normalized;
            return normalized.SqrMagnitudeScalar <= BattleScalar.Zero ? new BattleVector2(1f, 0f) : normalized;
        }
    }
}
