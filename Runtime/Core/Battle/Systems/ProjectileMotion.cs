using System;

namespace Combat.Core.Battle
{
    internal static class ProjectileMotion
    {
        public static BattleVector2 Advance(ProjectileComponent projectile)
        {
            switch (projectile.Behavior)
            {
                case ProjectileBehavior.Linear:
                    return projectile.Position + projectile.Velocity;
                default:
                    throw new InvalidOperationException(
                        $"Unsupported projectile behavior: {projectile.Behavior}");
            }
        }
    }
}
