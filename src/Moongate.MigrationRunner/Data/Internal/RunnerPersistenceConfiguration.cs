namespace Moongate.MigrationRunner.Data.Internal;

internal sealed class RunnerPersistenceConfiguration
{
    public RunnerDatabaseConfiguration Accounts { get; set; } = new();
    public RunnerDatabaseConfiguration Realm { get; set; } = new();
}
