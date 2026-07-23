using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Combat.Core.Battle
{
    public enum BattleInputCommandType
    {
        Auto,
        Hold,
        MoveToPosition,
        Garrison,
        FocusTarget,
        UseAbility
    }

    public readonly struct BattleInputCommand
    {
        private BattleInputCommand(
            BattleInputCommandType type,
            UnitId unitId,
            UnitId targetUnitId,
            BattleVector2 destination,
            int abilityIndex)
        {
            Type = ValidateType(type);
            UnitId = unitId;
            TargetUnitId = targetUnitId;
            Destination = destination;
            AbilityIndex = abilityIndex;
        }

        public BattleInputCommandType Type { get; }
        public UnitId UnitId { get; }
        public UnitId TargetUnitId { get; }
        public BattleVector2 Destination { get; }
        public int AbilityIndex { get; }

        public static BattleInputCommand Auto(UnitId unitId)
        {
            return new BattleInputCommand(BattleInputCommandType.Auto, unitId, default, default, -1);
        }

        public static BattleInputCommand Hold(UnitId unitId)
        {
            return new BattleInputCommand(BattleInputCommandType.Hold, unitId, default, default, -1);
        }

        public static BattleInputCommand MoveToPosition(UnitId unitId, BattleVector2 destination)
        {
            return new BattleInputCommand(BattleInputCommandType.MoveToPosition, unitId, default, destination, -1);
        }

        public static BattleInputCommand Garrison(UnitId unitId)
        {
            return new BattleInputCommand(BattleInputCommandType.Garrison, unitId, default, default, -1);
        }

        public static BattleInputCommand FocusTarget(UnitId unitId, UnitId targetUnitId)
        {
            return new BattleInputCommand(BattleInputCommandType.FocusTarget, unitId, targetUnitId, default, -1);
        }

        public static BattleInputCommand UseAbility(UnitId unitId, int abilityIndex, UnitId targetUnitId)
        {
            return new BattleInputCommand(BattleInputCommandType.UseAbility, unitId, targetUnitId, default, abilityIndex);
        }

        private static BattleInputCommandType ValidateType(BattleInputCommandType type)
        {
            switch (type)
            {
                case BattleInputCommandType.Auto:
                case BattleInputCommandType.Hold:
                case BattleInputCommandType.MoveToPosition:
                case BattleInputCommandType.Garrison:
                case BattleInputCommandType.FocusTarget:
                case BattleInputCommandType.UseAbility:
                    return type;
                default:
                    throw new ArgumentOutOfRangeException(nameof(type), type, "Unsupported battle input command type.");
            }
        }
    }

    public readonly struct BattleInputFrame
    {
        private static readonly ReadOnlyCollection<BattleInputCommand> EmptyCommands =
            new ReadOnlyCollection<BattleInputCommand>(Array.Empty<BattleInputCommand>());

        private readonly BattleInputCommand[] _commands;
        private readonly ReadOnlyCollection<BattleInputCommand> _readOnlyCommands;

        public BattleInputFrame(IReadOnlyList<BattleInputCommand> commands)
        {
            if (commands == null)
            {
                throw new ArgumentNullException(nameof(commands));
            }

            _commands = new BattleInputCommand[commands.Count];
            for (var i = 0; i < commands.Count; i++)
            {
                _commands[i] = commands[i];
            }

            _readOnlyCommands = new ReadOnlyCollection<BattleInputCommand>(_commands);
        }

        public static BattleInputFrame Empty => default;
        public IReadOnlyList<BattleInputCommand> Commands => _readOnlyCommands ?? EmptyCommands;
    }
}
