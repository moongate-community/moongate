using System.Runtime.ExceptionServices;
using Moongate.Persistence.Data.Config;
using Moongate.Persistence.Data.Internal;
using Moongate.Persistence.Data.Schema;
using Moongate.Persistence.Interfaces;
using Moongate.Persistence.Types.Persistence;

namespace Moongate.Persistence.Internal;

internal sealed class PersistenceSchemaCoordinator : IAsyncDisposable
{
    private readonly PostgreSqlPersistenceOptions _options;
    private readonly PersistenceModuleRegistry _registry;
    private readonly SemaphoreSlim _operationGate = new(1, 1);
    private readonly Dictionary<PersistenceDatabaseTarget, PostgreSqlDatabase> _databases = [];
    private PersistenceModuleRegistrySnapshot? _snapshot;
    private ExceptionDispatchInfo? _preparationFailure;
    private bool _preparationAttempted;
    private bool _disposed;

    public bool IsReady { get; private set; }

    public PersistenceSchemaCoordinator(
        PostgreSqlPersistenceOptions options,
        PersistenceModuleRegistry registry
    )
    {
        _options = options;
        _registry = registry;
    }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        await _operationGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            ThrowIfDisposed();
            IsReady = false;
            Prepare();
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
                        "Run the schema preview/apply command or explicitly enable automatic schema synchronization."
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

    public async Task<IReadOnlyList<PersistenceSchemaChange>> PreviewAsync(
        CancellationToken cancellationToken = default
    )
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
            foreach (var target in _registry.GetDatabaseTargets())
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

    private async Task<IReadOnlyList<PersistenceSchemaChange>> PreviewCoreAsync(
        CancellationToken cancellationToken
    )
    {
        var changes = new List<PersistenceSchemaChange>();
        foreach (var module in _snapshot!.Modules)
        {
            var database = _databases[module.Module.DatabaseTarget];
            var ddl = await CompareAsync(database, module, cancellationToken).ConfigureAwait(false);
            if (!string.IsNullOrWhiteSpace(ddl))
            {
                changes.Add(
                    new PersistenceSchemaChange(
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
            return await comparison.WaitAsync(cancellationToken).ConfigureAwait(false);
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

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
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
