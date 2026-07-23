using System;

namespace Combat.Core.Battle
{
    public enum AiBrainKind
    {
        StateMachine
    }

    public sealed class AiDefinition
    {
        public AiDefinition(string id)
            : this(id, AiBrainKind.StateMachine)
        {
        }

        public AiDefinition(string id, AiBrainKind kind)
        {
            Id = string.IsNullOrWhiteSpace(id) ? throw new ArgumentException("AI definition id is required.", nameof(id)) : id;
            Kind = ValidateKind(kind);
        }

        public string Id { get; }
        public AiBrainKind Kind { get; }

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
    }
}
