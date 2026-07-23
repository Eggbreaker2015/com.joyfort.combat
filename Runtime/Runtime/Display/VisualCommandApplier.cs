using System;
using Combat.Core.Battle;
using Combat.Runtime.Runner;

namespace Combat.Runtime.Display
{
    internal static class VisualCommandApplier
    {
        public static void Dispatch(ICombatViewPort viewPort, VisualCommand command)
        {
            if (viewPort == null)
            {
                throw new ArgumentNullException(nameof(viewPort));
            }

            if (!command.IsValid)
            {
                throw new InvalidOperationException("Cannot dispatch an invalid visual command.");
            }

            switch (command.Type)
            {
                case VisualCommandType.CreateUnit:
                    viewPort.CreateUnit(command.GetPayload<UnitSpawnViewSnapshot>());
                    break;
                case VisualCommandType.MoveUnit:
                    UnitMoveViewSnapshot moveUnit = command.GetPayload<UnitMoveViewSnapshot>();
                    viewPort.MoveUnit(moveUnit.UnitId, moveUnit.Position);
                    break;
                case VisualCommandType.StopUnitMovement:
                    viewPort.StopUnitMovement(command.GetPayload<UnitCommandTarget>().UnitId);
                    break;
                case VisualCommandType.FaceUnit:
                    UnitFacingViewSnapshot faceUnit = command.GetPayload<UnitFacingViewSnapshot>();
                    viewPort.FaceUnit(faceUnit.UnitId, faceUnit.Facing);
                    break;
                case VisualCommandType.SetUnitVisibility:
                    UnitVisibilityViewSnapshot visibility = command.GetPayload<UnitVisibilityViewSnapshot>();
                    viewPort.SetUnitVisibility(visibility.UnitId, visibility.IsVisible);
                    break;
                case VisualCommandType.PlayAction:
                    viewPort.PlayAction(command.GetPayload<ActionVisualCommandPayload>().Snapshot);
                    break;
                case VisualCommandType.EndAction:
                    break;
                case VisualCommandType.PlayHit:
                    viewPort.PlayHit(command.GetPayload<DamageViewSnapshot>());
                    break;
                case VisualCommandType.PlayHeal:
                    viewPort.PlayHeal(command.GetPayload<HealingViewSnapshot>());
                    break;
                case VisualCommandType.DestroyUnit:
                    viewPort.DestroyUnit(command.GetPayload<UnitCommandTarget>().UnitId);
                    break;
                case VisualCommandType.CreateProjectile:
                    viewPort.CreateProjectile(command.GetPayload<ProjectileViewSnapshot>());
                    break;
                case VisualCommandType.MoveProjectile:
                    ProjectileMoveViewSnapshot moveProjectile = command.GetPayload<ProjectileMoveViewSnapshot>();
                    viewPort.MoveProjectile(moveProjectile.ProjectileId, moveProjectile.Position);
                    break;
                case VisualCommandType.PlayProjectileHit:
                    viewPort.PlayProjectileHit(command.GetPayload<ProjectileHitViewSnapshot>());
                    break;
                case VisualCommandType.DestroyProjectile:
                    viewPort.DestroyProjectile(command.GetPayload<ProjectileCommandTarget>().ProjectileId);
                    break;
                case VisualCommandType.PlayStatusApplied:
                    viewPort.PlayStatusApplied(command.GetPayload<StatusViewSnapshot>());
                    break;
                case VisualCommandType.PlayStatusExpired:
                    viewPort.PlayStatusExpired(command.GetPayload<StatusViewSnapshot>());
                    break;
                case VisualCommandType.ShowBattleResult:
                    viewPort.ShowBattleResult(new BattleResult(true, command.GetPayload<BattleResultViewSnapshot>().WinningTeamId));
                    break;
                default:
                    throw new InvalidOperationException($"Unsupported visual command type: {command.Type}.");
            }
        }
    }
}
