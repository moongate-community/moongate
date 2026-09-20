using Moongate.Persistence.Types.Persistence;

namespace Moongate.Persistence.Data.Config;

/// <summary>Configures PostgreSQL targets and explicit schema synchronization policy.</summary>
public sealed class PostgreSqlPersistenceOptions
{
    private readonly IReadOnlyDictionary<PersistenceDatabaseTarget, PersistenceDatabaseOptions> _databases;

    /// <summary>Gets whether normal initialization may apply schema changes.</summary>
    public bool AutoSynchronizeSchema { get; }

    /// <summary>Gets the configured database targets without resolving their connection strings.</summary>
    public IReadOnlyCollection<PersistenceDatabaseTarget> ConfiguredTargets => _databases.Keys.ToArray();

    /// <summary>Creates empty options with schema synchronization disabled.</summary>
    public PostgreSqlPersistenceOptions() : this([], false)
    {
    }

    /// <summary>Creates persistence options.</summary>
    /// <param name="databases">Independently configured database targets.</param>
    /// <param name="autoSynchronizeSchema">Whether normal initialization may apply generated schema DDL.</param>
    public PostgreSqlPersistenceOptions(
        IEnumerable<PersistenceDatabaseOptions> databases,
        bool autoSynchronizeSchema = false
    )
    {
        ArgumentNullException.ThrowIfNull(databases);
        var configured = new Dictionary<PersistenceDatabaseTarget, PersistenceDatabaseOptions>();
        foreach (var database in databases)
        {
            ArgumentNullException.ThrowIfNull(database);
            if (!configured.TryAdd(database.Target, database))
            {
                throw new ArgumentException($"Persistence target '{database.Target}' is configured more than once.", nameof(databases));
            }
        }

        _databases = configured;
        AutoSynchronizeSchema = autoSynchronizeSchema;
    }

    internal PersistenceDatabaseOptions GetRequiredDatabase(PersistenceDatabaseTarget target)
    {
        if (!_databases.TryGetValue(target, out var database))
        {
            throw new InvalidOperationException($"Persistence target '{target}' is required by a module but is not configured.");
        }

        return database;
    }

    /// <inheritdoc />
    public override string ToString()
    {
        return $"PostgreSqlPersistenceOptions {{ AutoSynchronizeSchema = {AutoSynchronizeSchema}, ConfiguredTargets = {_databases.Count} }}";
    }
}
