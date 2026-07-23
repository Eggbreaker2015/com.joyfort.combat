using System;

namespace Combat.Core.Battle
{
    internal enum BrainState
    {
        Idle,
        Chase,
        Attack,
        Dead
    }

    internal readonly struct BrainComponent
    {
        public BrainComponent(string definitionId, AiBrainKind kind, BrainState state, BattleTick stateEnteredTick)
        {
            DefinitionId = string.IsNullOrWhiteSpace(definitionId) ? throw new ArgumentException("AI definition id is required.", nameof(definitionId)) : definitionId;
            Kind = ValidateKind(kind);
            State = ValidateState(state);
            StateEnteredTick = stateEnteredTick;
        }

        public string DefinitionId { get; }
        public AiBrainKind Kind { get; }
        public BrainState State { get; }
        public BattleTick StateEnteredTick { get; }

        public BrainComponent WithState(BrainState state, BattleTick stateEnteredTick)
        {
            state = ValidateState(state);
            return state == State
                ? this
                : new BrainComponent(DefinitionId, Kind, state, stateEnteredTick);
        }

        private static AiBrainKind ValidateKind(AiBrainKind kind)
        {
            switch (kind)
            {
                case AiBrainKind.StateMachine:
                    return kind;
                default:
                    throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unsupported AI brain kind.");
            }
        }

        private static BrainState ValidateState(BrainState state)
        {
            switch (state)
            {
                case BrainState.Idle:
                case BrainState.Chase:
                case BrainState.Attack:
                case BrainState.Dead:
                    return state;
                default:
                    throw new ArgumentOutOfRangeException(nameof(state), state, "Unsupported AI brain state.");
            }
        }
    }
}
