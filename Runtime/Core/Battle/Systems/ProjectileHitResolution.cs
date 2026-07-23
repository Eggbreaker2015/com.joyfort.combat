using System.Collections.Generic;
using Combat.Foundation.Events;

namespace Combat.Core.Battle
{
    internal static class ProjectileHitResolution
    {
        public static void SelectValidHits(
            BattleWorld world,
            List<ProjectileHit> candidates,
            HashSet<ProjectileId> exhaustedProjectiles,
            List<ProjectileHitContext> resolvedHits)
        {
            candidates.Sort(CompareCandidates);
            for (var i = 0; i < candidates.Count; i++)
            {
                ProjectileHit hit = candidates[i];
                if (exhaustedProjectiles.Contains(hit.ProjectileId)
                    || !world.TryFindProjectile(hit.ProjectileId, out EntityId projectileEntity)
                    || ProjectileLifecycle.HasPendingDestroy(world, projectileEntity)
                    || !world.ProjectileComponents.TryGet(
                        projectileEntity,
                        out ProjectileComponent projectile)
                    || !projectile.CanHitMoreTargets
                    || projectile.HasHitTarget(hit.TargetUnitId)
                    || !TryCreateContext(
                        world,
                        projectile,
                        projectileEntity,
                        hit,
                        out ProjectileHitContext context))
                {
                    continue;
                }

                ProjectileComponent updated =
                    projectile.WithRegisteredHit(hit.TargetUnitId);
                bool destroysProjectile = !updated.CanHitMoreTargets;
                world.ProjectileComponents.Set(projectileEntity, updated);
                context = context.WithDestroyDecision(destroysProjectile);
                if (destroysProjectile)
                {
                    exhaustedProjectiles.Add(hit.ProjectileId);
                }

                resolvedHits.Add(context);
            }
        }

        public static void Apply(
            BattleWorld world,
            List<ProjectileHitContext> resolvedHits,
            EventBuffer<BattleEvent> events,
            EventSequence eventSequence,
            BattleTick tick,
            HashSet<EntityId> destroyedProjectiles)
        {
            resolvedHits.Sort(CompareResolved);
            for (var i = 0; i < resolvedHits.Count; i++)
            {
                ProjectileHitContext context = resolvedHits[i];
                if (ProjectileLifecycle.HasPendingDestroy(world, context.ProjectileEntity)
                    || !world.ProjectileComponents.TryGet(
                        context.ProjectileEntity,
                        out ProjectileComponent projectile))
                {
                    continue;
                }

                events.Write(BattleEvent.ProjectileHit(
                    eventSequence.Next(),
                    tick,
                    context.ProjectileId,
                    context.SourceUnitId,
                    context.TargetUnitId,
                    context.Position));
                QueueImpactEffects(world, projectile, context);
                if (context.DestroysProjectile)
                {
                    ProjectileLifecycle.QueueDestroy(
                        world,
                        events,
                        eventSequence,
                        tick,
                        context.ProjectileId,
                        context.ProjectileEntity,
                        destroyedProjectiles);
                }
            }
        }

        private static void QueueImpactEffects(
            BattleWorld world,
            ProjectileComponent projectile,
            ProjectileHitContext context)
        {
            IReadOnlyList<BattleEffectData> effects = projectile.ImpactEffects;
            for (var i = 0; i < effects.Count; i++)
            {
                BattleEffectData effect = effects[i];
                world.CommandBuffer.QueueEffect(BattleEffectCommandFactory.CreateAt(
                    projectile.Source,
                    context.TargetEntity,
                    effect,
                    BattleEffectContext.Projectile(projectile.ProjectileId, effect.Type),
                    context.Position));
            }
        }

        private static bool TryCreateContext(
            BattleWorld world,
            ProjectileComponent projectile,
            EntityId projectileEntity,
            ProjectileHit hit,
            out ProjectileHitContext context)
        {
            context = default;
            if (!hit.ProjectileEntity.Equals(projectileEntity)
                || !world.TryGetUnitId(projectile.Source, out UnitId sourceUnitId)
                || !world.TryFindEntity(hit.TargetUnitId, out EntityId resolvedTarget)
                || !resolvedTarget.Equals(hit.TargetEntity)
                || !world.IsAliveUnit(resolvedTarget)
                || !world.TryGetTeamId(resolvedTarget, out TeamId targetTeam)
                || targetTeam.Equals(projectile.TeamId))
            {
                return false;
            }

            context = new ProjectileHitContext(
                hit.ProjectileId,
                projectileEntity,
                projectile.Source,
                sourceUnitId,
                resolvedTarget,
                hit.TargetUnitId,
                hit.Position,
                hit.Fraction,
                destroysProjectile: false);
            return true;
        }

        private static int CompareCandidates(ProjectileHit left, ProjectileHit right)
        {
            int projectileComparison = left.ProjectileId.Value.CompareTo(
                right.ProjectileId.Value);
            if (projectileComparison != 0)
            {
                return projectileComparison;
            }

            int fractionComparison = left.Fraction.CompareTo(right.Fraction);
            return fractionComparison != 0
                ? fractionComparison
                : left.TargetUnitId.Value.CompareTo(right.TargetUnitId.Value);
        }

        private static int CompareResolved(
            ProjectileHitContext left,
            ProjectileHitContext right)
        {
            int projectileComparison = left.ProjectileId.Value.CompareTo(
                right.ProjectileId.Value);
            if (projectileComparison != 0)
            {
                return projectileComparison;
            }

            int fractionComparison = left.Fraction.CompareTo(right.Fraction);
            return fractionComparison != 0
                ? fractionComparison
                : left.TargetUnitId.Value.CompareTo(right.TargetUnitId.Value);
        }
    }
}
