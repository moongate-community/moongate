using System.Runtime.ExceptionServices;
using Moongate.Persistence.Data.Config;
using Moongate.Persistence.Data.Internal;
using Moongate.Persistence.Data.Schema;
using Moongate.Persistence.Interfaces;
using Moongate.Persistence.Migrations.Services;
using Moongate.Persistence.Migrations.Types.Migrations;
using Moongate.Persistence.Types.Persistence;
using Npgsql;
using Serilog;

namespace Moongate.Persistence.Internal;

internal sealed class PersistenceSchemaCoordinator : IAsyncDisposable
{
    private readonly ILogger _logger;
    private readonly PostgreSqlPersistenceOptions _options;
    private readonly PersistenceModuleRegistry _registry;
    private readonly SemaphoreSlim _operationGate = new(1, 1);
    private readonly Dictionary<PersistenceDatabaseTarget, PostgreSqlDatabase> _databases = [];
    private readonly Dictionary<PersistenceDatabaseTarget, MigrationCatalog> _catalogs = [];
    private PersistenceModuleRegistrySnapshot? _snapshot;
    private ExceptionDispatchInfo? _preparationFailure;
    private bool _preparationAttempted;
    private bool _disposed;

    public bool IsReady { get; private set; }

    public PersistenceSchemaCoordinator(
        PostgreSqlPersistenceOptions options,
        PersistenceModuleRegistry registry,
        ILogger? logger = null
    )
    {
        _logger = logger ?? Log.ForContext<PersistenceSchemaCoordinator>();
        _options = options;
        _registry = registry;
    }

    public PostgreSqlDatabase GetDatabase(PersistenceDatabaseTarget target)
    {
        ThrowIfDisposed();
        Prepare();

        return _databases.TryGetValue(target, out var database)
            ? database
            : throw new InvalidOperationException($"Persistence target '{target}' is not active.");
    }

    public IPersistenceModule GetOwner(Type entityType)
    {
        ThrowIfDisposed();
        Prepare();

        return _snapshot!.GetOwner(entityType);
    }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        await _operationGate.WaitAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            ThrowIfDisposed();
            IsReady = false;
            Prepare();
            await CheckConnectionsAsync(cancellationToken).ConfigureAwait(false);
            if (_options.DevelopmentMigrations is not null)
            {
                await new DevelopmentMigrationCoordinator(_options.DevelopmentMigrations, _logger)
                    .RunAsync(_databases, _snapshot!, cancellationToken)
                    .ConfigureAwait(false);
                IsReady = true;
                return;
            }

            await ValidateMigrationsAsync(cancellationToken).ConfigureAwait(false);

            if (_options.AutoSynchronizeSchema)
            {
                await SynchronizeCoreAsync(cancellationToken).ConfigureAwait(false);
            }
            else
            {
                var changes = await PreviewCoreAsync(cancellationToken).ConfigureAwait(false);

                if (changes.Count > 0)
                {
                    var modules = string.Join(", ", changes.Select(change => change.ModuleId));

                    throw new InvalidOperationException(
                        $"PostgreSQL schema changes are required for persistence modules: {modules}. " +
                        "Generate and review a SQL migration, then apply it with Moongate.MigrationRunner."
                    );
                }
            }

            IsReady = true;
        }
        finally
        {
            _operationGate.Release();
        }
    }

    public async Task<IReadOnlyList<PersistenceSchemaChange>> PreviewAsync(CancellationToken cancellationToken = default)
    {
        await _operationGate.WaitAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            ThrowIfDisposed();
            Prepare();

            return await PreviewCoreAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _operationGate.Release();
        }
    }

    public async Task SynchronizeAsync(CancellationToken cancellationToken = default)
    {
        await _operationGate.WaitAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            ThrowIfDisposed();
            IsReady = false;
            Prepare();
            await SynchronizeCoreAsync(cancellationToken).ConfigureAwait(false);
            IsReady = true;
        }
        finally
        {
            _operationGate.Release();
        }
    }

    private async Task CheckConnectionsAsync(CancellationToken cancellationToken)
    {
        foreach (var target in _options.ConfiguredTargets)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var connectionString = _databases.TryGetValue(target, out var database)
                ? database.RuntimeConnectionString
                : _options.GetRequiredDatabase(target).ResolveRuntimeConnectionString();
            await using var connection = new NpgsqlConnection(connectionString);

            try
            {
                await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
                await using var command = new NpgsqlCommand("SELECT 1", connection);
                await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (NpgsqlException exception) when (!cancellationToken.IsCancellationRequested)
            {
                throw new InvalidOperationException(
                    $"Postgres connection failed for persistence target '{target}'. Check the database, credentials and server availability.",
                    exception
                );
            }

            _logger.Information(
                "Postgres connection successful: {Target} database {Database} at {Host}:{Port}",
                target,
                connection.Database,
                connection.Host,
                connection.Port
            );
        }
    }

    private static async Task<string> CompareAsync(
        PostgreSqlDatabase database,
        PersistenceModuleRegistration module,
        CancellationToken cancellationToken
    )
    {
        cancellationToken.ThrowIfCancellationRequested();
        var comparison = Task.Run(
            () => database.Orm.CodeFirst.GetComparisonDDLStatements(module.EntityTypes.ToArray()),
            CancellationToken.None
        );

        try
        {
            var ddl = await comparison.WaitAsync(cancellationToken).ConfigureAwait(false);
            var sequences = await PersistenceSerialSequence.CompareAsync(database, module.EntityTypes, cancellationToken)
                .ConfigureAwait(false);
            return ddl + sequences;
        }
        catch (OperationCanceledException)
        {
            try
            {
                await comparison.ConfigureAwait(false);
            }
            catch (Exception comparisonFailure)
            {
                throw new InvalidOperationException(
                    $"Persistence module '{module.Module.Id}' schema comparison failed after cancellation was requested.",
                    comparisonFailure
                );
            }

            cancellationToken.ThrowIfCancellationRequested();

            throw;
        }
    }

    private void Prepare()
    {
        if (_snapshot is not null)
        {
            return;
        }

        if (_preparationFailure is not null)
        {
            _preparationFailure.Throw();
        }

        if (_preparationAttempted)
        {
            throw new InvalidOperationException("Persistence schema preparation did not complete.");
        }

        _preparationAttempted = true;

        try
        {
            var targets = _registry.GetDatabaseTargets().ToHashSet();

            if (_options.MigrationCatalogFactory is not null)
            {
                foreach (var target in _options.ConfiguredTargets)
                {
                    var catalog = _options.MigrationCatalogFactory(target);

                    if (catalog is null)
                    {
                        continue;
                    }

                    var expected = target == PersistenceDatabaseTarget.Accounts
                        ? MigrationTarget.Auth
                        : MigrationTarget.World;

                    if (catalog.Target != expected)
                    {
                        throw new InvalidOperationException(
                            "The migration catalog belongs to a different persistence target."
                        );
                    }

                    _catalogs.Add(target, catalog);

                    if (catalog.Scripts.Count > 0 && (_options.ActivateMigrationTarget?.Invoke(target) ?? true))
                    {
                        targets.Add(target);
                    }
                }
            }

            foreach (var target in targets)
            {
                var database = PostgreSqlDatabase.Create(_options.GetRequiredDatabase(target));
                _databases.Add(target, database);
            }

            _snapshot = _registry.ValidateAndFreeze(_databases.Values.ToArray());
        }
        catch (Exception exception)
        {
            foreach (var database in _databases.Values)
            {
                database.Dispose();
            }

            _databases.Clear();
            _preparationFailure = ExceptionDispatchInfo.Capture(exception);

            throw;
        }
    }

    private async Task<IReadOnlyList<PersistenceSchemaChange>> PreviewCoreAsync(CancellationToken cancellationToken)
    {
        var changes = new List<PersistenceSchemaChange>();

        foreach (var module in _snapshot!.Modules)
        {
            var database = _databases[module.Module.DatabaseTarget];
            var ddl = await CompareAsync(database, module, cancellationToken).ConfigureAwait(false);

            if (!string.IsNullOrWhiteSpace(ddl))
            {
                changes.Add(
                    new(
                        module.Module.DatabaseTarget,
                        module.Module.Id,
                        ddl
                    )
                );
            }
        }

        return changes;
    }

    private async Task SynchronizeCoreAsync(CancellationToken cancellationToken)
    {
        foreach (var targetGroup in _snapshot!.Modules.GroupBy(module => module.Module.DatabaseTarget))
        {
            var database = _databases[targetGroup.Key];
            await using var schemaLock = await PostgreSqlSchemaLock.AcquireAsync(
                    database.SchemaConnectionString,
                    targetGroup.Key,
                    cancellationToken
                )
                .ConfigureAwait(false);

            foreach (var module in targetGroup)
            {
                var ddl = await CompareAsync(database, module, cancellationToken).ConfigureAwait(false);

                if (!string.IsNullOrWhiteSpace(ddl))
                {
                    await schemaLock.ExecuteDdlAsync(ddl, cancellationToken).ConfigureAwait(false);
                }
            }

            foreach (var module in targetGroup)
            {
                var remaining = await CompareAsync(database, module, cancellationToken).ConfigureAwait(false);

                if (!string.IsNullOrWhiteSpace(remaining))
                {
                    throw new InvalidOperationException(
                        $"Persistence module '{module.Module.Id}' still requires schema changes after synchronization."
                    );
                }
            }
        }
    }

    private void ThrowIfDisposed()
        => ObjectDisposedException.ThrowIf(_disposed, this);

    private async Task ValidateMigrationsAsync(CancellationToken cancellationToken)
    {
        foreach (var (target, catalog) in _catalogs)
        {
            if (!_databases.TryGetValue(target, out var database))
            {
                continue;
            }

            await using var connection = new NpgsqlConnection(database.RuntimeConnectionString);
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
            var applied = await MigrationHistory
                .ReadAsync(() => connection.CreateCommand(), catalog.Target, cancellationToken)
                .ConfigureAwait(false);
            var pending = MigrationHistory.Validate(catalog, applied);

            if (pending.Count > 0)
            {
                throw new InvalidOperationException(
                    $"Pending PostgreSQL migrations for {target}: {string.Join(", ", pending.Select(script => script.Name))}. Run Moongate.MigrationRunner apply before starting the server."
                );
            }
        }
    }

    public async ValueTask DisposeAsync()
    {
        await _operationGate.WaitAsync().ConfigureAwait(false);

        try
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            IsReady = false;

            foreach (var database in _databases.Values)
            {
                database.Dispose();
            }

            _databases.Clear();
        }
        finally
        {
            _operationGate.Release();
        }
    }
}
