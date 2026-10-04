using Moongate.Persistence.Interfaces;
using Moongate.Persistence.Types.Persistence;

namespace Moongate.Tests.TestSupport.Persistence;

/// <summary>
///     Records the targets it is asked to apply, and applies nothing.
/// </summary>
public sealed class RecordingMigrationRunner : IDevelopmentMigrationRunner
{
    public List<PersistenceDatabaseTarget> Applied { get; } = [];

    public int Validated { get; private set; }

    public void ValidateAvailable()
    {
        Validated++;
    }

    public Task ApplyAsync(PersistenceDatabaseTarget target, CancellationToken cancellationToken)
    {
        Applied.Add(target);

        return Task.CompletedTask;
    }
}
