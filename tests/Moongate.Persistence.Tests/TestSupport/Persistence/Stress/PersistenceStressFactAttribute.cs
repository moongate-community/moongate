namespace Moongate.Persistence.Tests.TestSupport.Persistence.Stress;

public sealed class PersistenceStressFactAttribute : FactAttribute
{
    public PersistenceStressFactAttribute()
    {
        if (Environment.GetEnvironmentVariable("MOONGATE_RUN_PERSISTENCE_STRESS") != "1")
        {
            Skip = "Run explicitly with scripts/stress-persistence.py; stress load is excluded from ordinary test runs.";
        }
    }
}
