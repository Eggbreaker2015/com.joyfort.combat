namespace Combat.Foundation.Diagnostics
{
    public interface ICombatLogSink
    {
        void Write(CombatLogEntry entry);
    }
}
