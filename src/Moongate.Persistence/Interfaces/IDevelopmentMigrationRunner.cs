using Moongate.Persistence.Types.Persistence;

namespace Moongate.Persistence.Interfaces;

/// <summary>Executes versioned migrations outside the FreeSql dependency graph.</summary>
public interface IDevelopmentMigrationRunner
{
    /// <summary>Fails before generation if the runner cannot be started.</summary>
    void ValidateAvailable();

    /// <summary>Applies pending files, awaiting process termination even when canceled.</summary>
    Task ApplyAsync(PersistenceDatabaseTarget target, CancellationToken cancellationToken);
}
