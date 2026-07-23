namespace Combat.Foundation.Diagnostics
{
    public interface ICombatLogFilter
    {
        bool ShouldLog(CombatLogLevel level, string tag);
    }
}
