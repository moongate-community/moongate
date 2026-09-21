using Moongate.Persistence.Migrations.Services;
using Moongate.Persistence.Types.Persistence;

namespace Moongate.Persistence.Data.Config;

/// <summary>Configures PostgreSQL targets and explicit schema synchronization policy.</summary>
public sealed class PostgreSqlPersistenceOptions
{
    private readonly IReadOnlyDictionary<PersistenceDatabaseTarget, PersistenceDatabaseOptions> _databases;

    /// <summary>Gets the optional filter for targets activated by SQL alone; registered entities always activate their target.</summary>
    public Func<PersistenceDatabaseTarget, bool>? ActivateMigrationTarget { get; }

    /// <summary>Gets the optional versioned migration catalog factory, resolved at preparation time.</summary>
    public Func<PersistenceDatabaseTarget, MigrationCatalog?>? MigrationCatalogFactory { get; }

    /// <summary>Gets the optional explicit development migration policy.</summary>
    public DevelopmentMigrationOptions? DevelopmentMigrations { get; }

    /// <summary>Gets whether normal initialization may apply schema changes.</summary>
    public bool AutoSynchronizeSchema { get; }

    /// <summary>Gets the configured database targets without resolving their connection strings.</summary>
    public IReadOnlyCollection<PersistenceDatabaseTarget> ConfiguredTargets => _databases.Keys.ToArray();

    /// <summary>Creates empty options with schema synchronization disabled.</summary>
    public PostgreSqlPersistenceOptions() : this([])
    {
    }

    /// <summary>Creates persistence options.</summary>
    /// <param name="databases">Independently configured database targets.</param>
    /// <param name="autoSynchronizeSchema">Whether normal initialization may apply generated schema DDL.</param>
    /// <param name="migrationCatalogFactory">Optional immutable SQL catalog provider used for startup readiness checks.</param>
    /// <param name="activateMigrationTarget">
    /// Optional role filter for SQL-only targets. Registered entity targets bypass this
    /// filter.
    /// </param>
    public PostgreSqlPersistenceOptions(
        IEnumerable<PersistenceDatabaseOptions> databases,
        bool autoSynchronizeSchema = false,
        Func<PersistenceDatabaseTarget, MigrationCatalog?>? migrationCatalogFactory = null,
        Func<PersistenceDatabaseTarget, bool>? activateMigrationTarget = null,
        DevelopmentMigrationOptions? developmentMigrations = null
    )
    {
        ArgumentNullException.ThrowIfNull(databases);
        if (autoSynchronizeSchema && developmentMigrations is not null)
        {
            throw new ArgumentException("Automatic schema synchronization and migration generation are mutually exclusive.");
        }

        DevelopmentMigrations = developmentMigrations;
        var configured = new Dictionary<PersistenceDatabaseTarget, PersistenceDatabaseOptions>();

        foreach (var database in databases)
        {
            ArgumentNullException.ThrowIfNull(database);

            if (!configured.TryAdd(database.Target, database))
            {
                throw new ArgumentException(
                    $"Persistence target '{database.Target}' is configured more than once.",
                    nameof(databases)
                );
            }
        }

        _databases = configured;
        AutoSynchronizeSchema = autoSynchronizeSchema;
        MigrationCatalogFactory = developmentMigrations is null ? migrationCatalogFactory : developmentMigrations.Load;
        ActivateMigrationTarget = activateMigrationTarget;
    }

    /// <inheritdoc />
    public override string ToString()
        =>
            $"PostgreSqlPersistenceOptions {{ AutoSynchronizeSchema = {AutoSynchronizeSchema}, ConfiguredTargets = {_databases.Count} }}";

    internal PersistenceDatabaseOptions GetRequiredDatabase(PersistenceDatabaseTarget target)
    {
        if (!_databases.TryGetValue(target, out var database))
        {
            throw new InvalidOperationException(
                $"Persistence target '{target}' is required by a module but is not configured."
            );
        }

        return database;
    }
}
