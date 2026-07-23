using System;
using System.Collections.Generic;
using Combat.Core.Battle;

namespace Combat.Runtime.Display
{
    public sealed class VisualTimelineRunner : IVisualCommandSink
    {
        private readonly ICombatViewPort _viewPort;
        private readonly VisualTimeline _timeline;
        private readonly VisualTimelinePolicy _policy;
        private long _nextOrder;

        public VisualTimelineRunner(ICombatViewPort viewPort)
            : this(viewPort, new VisualTimeline(), VisualTimelinePolicy.Default)
        {
        }

        public VisualTimelineRunner(ICombatViewPort viewPort, VisualTimeline timeline, VisualTimelinePolicy policy)
        {
            _viewPort = viewPort ?? throw new ArgumentNullException(nameof(viewPort));
            _timeline = timeline ?? throw new ArgumentNullException(nameof(timeline));
            _policy = policy ?? throw new ArgumentNullException(nameof(policy));
        }

        public float CurrentTimeSeconds { get; private set; }
        public int PendingCount => _timeline.Count;

        public void Dispatch(VisualCommand command)
        {
            Enqueue(command);
        }

        public void Enqueue(VisualCommand command)
        {
            if (!command.IsValid)
            {
                throw new InvalidOperationException("Visual command is invalid and cannot be scheduled.");
            }

            float scheduledTimeSeconds = CurrentTimeSeconds + _policy.GetDelaySeconds(command);
            _timeline.Enqueue(new VisualTimelineEntry(command, scheduledTimeSeconds, _nextOrder));
            _nextOrder++;
        }

        public void Enqueue(IReadOnlyList<VisualCommand> commands)
        {
            if (commands == null)
            {
                throw new ArgumentNullException(nameof(commands));
            }

            for (var i = 0; i < commands.Count; i++)
            {
                Enqueue(commands[i]);
            }
        }

        public void Advance(float deltaSeconds)
        {
            if (float.IsNaN(deltaSeconds) || float.IsInfinity(deltaSeconds))
            {
                throw new ArgumentOutOfRangeException(nameof(deltaSeconds), deltaSeconds, "Delta time must be finite.");
            }

            if (deltaSeconds < 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(deltaSeconds), deltaSeconds, "Delta time cannot be negative.");
            }

            if (deltaSeconds > 0f)
            {
                var nextTimeSeconds = CurrentTimeSeconds + deltaSeconds;
                if (float.IsNaN(nextTimeSeconds) || float.IsInfinity(nextTimeSeconds))
                {
                    throw new ArgumentOutOfRangeException(nameof(deltaSeconds), deltaSeconds, "Delta time would overflow visual timeline time.");
                }

                CurrentTimeSeconds = nextTimeSeconds;
            }

            _timeline.AdvanceTo(CurrentTimeSeconds, _viewPort);
        }

        public void Flush()
        {
            _timeline.Flush(_viewPort);
        }

        public void Clear()
        {
            _timeline.Clear();
            CurrentTimeSeconds = 0f;
            _nextOrder = 0;
        }
    }

    public sealed class VisualPresentationScheduler : IVisualCommandSink
    {
        private readonly IVisualCommandSink _inner;
        private readonly Dictionary<int, UnitVisualChannels> _unitChannels = new Dictionary<int, UnitVisualChannels>();

        public VisualPresentationScheduler(IVisualCommandSink inner)
        {
            _inner = inner ?? throw new ArgumentNullException(nameof(inner));
        }

        public void Dispatch(VisualCommand command)
        {
            if (!command.IsValid)
            {
                throw new InvalidOperationException("Visual command is invalid and cannot be dispatched.");
            }

            switch (command.Type)
            {
                case VisualCommandType.CreateUnit:
                    _unitChannels[command.UnitId.Value] = UnitVisualChannels.None;
                    _inner.Dispatch(command);
                    break;
                case VisualCommandType.DestroyUnit:
                    _unitChannels.Remove(command.UnitId.Value);
                    _inner.Dispatch(command);
                    break;
                case VisualCommandType.MoveUnit:
                    if (!IsLocomotionBlocked(command.UnitId))
                    {
                        _inner.Dispatch(command);
                    }

                    break;
                case VisualCommandType.PlayAction:
                    StartAction(command.SourceUnitId, command.ActionLocks);
                    if (HasActionLock(command.ActionLocks, BattleActionLocks.Movement))
                    {
                        _inner.Dispatch(VisualCommand.StopUnitMovement(command.SourceUnitId));
                    }

                    _inner.Dispatch(command);
                    break;
                case VisualCommandType.EndAction:
                    EndAction(command.UnitId);
                    break;
                default:
                    _inner.Dispatch(command);
                    break;
            }
        }

        private bool IsLocomotionBlocked(UnitId unitId)
        {
            return _unitChannels.TryGetValue(unitId.Value, out UnitVisualChannels channels)
                && channels.IsActionBlockingLocomotion;
        }

        private void StartAction(UnitId unitId, BattleActionLocks actionLocks)
        {
            _unitChannels[unitId.Value] = GetChannels(unitId).WithActionLocks(actionLocks);
        }

        private void EndAction(UnitId unitId)
        {
            if (_unitChannels.ContainsKey(unitId.Value))
            {
                _unitChannels[unitId.Value] = GetChannels(unitId).WithActionLocks(BattleActionLocks.None);
            }
        }

        private UnitVisualChannels GetChannels(UnitId unitId)
        {
            return _unitChannels.TryGetValue(unitId.Value, out UnitVisualChannels channels)
                ? channels
                : UnitVisualChannels.None;
        }

        private readonly struct UnitVisualChannels
        {
            private UnitVisualChannels(BattleActionLocks actionLocks)
            {
                ActionLocks = actionLocks;
            }

            public BattleActionLocks ActionLocks { get; }
            public bool IsActionBlockingLocomotion => HasActionLock(ActionLocks, BattleActionLocks.Movement);
            public static UnitVisualChannels None => default;

            public UnitVisualChannels WithActionLocks(BattleActionLocks value)
            {
                return new UnitVisualChannels(value);
            }
        }

        private static bool HasActionLock(BattleActionLocks locks, BattleActionLocks flag)
        {
            return (locks & flag) == flag;
        }
    }
}
