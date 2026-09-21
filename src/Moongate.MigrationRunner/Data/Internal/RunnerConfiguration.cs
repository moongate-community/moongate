namespace Moongate.MigrationRunner.Data.Internal;

internal sealed class RunnerConfiguration
{
    public RunnerPersistenceConfiguration Persistence { get; set; } = new();
}
