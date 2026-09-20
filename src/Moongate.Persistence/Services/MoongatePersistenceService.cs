using System.Data.Common;
using System.Diagnostics;
using Moongate.Core.Interfaces.Entities;
using Moongate.Persistence.Data.Config;
using Moongate.Persistence.Data.Schema;
using Moongate.Persistence.DataAccess;
using Moongate.Persistence.Interfaces;
using Moongate.Persistence.Interfaces.Internal;
using Moongate.Persistence.Internal;
using Moongate.Persistence.Types.Persistence;
using Npgsql;
using Serilog;

namespace Moongate.Persistence.Services;

/// <summary>Owns registered PostgreSQL databases, schema readiness, transactions and world snapshots.</summary>
public sealed class MoongatePersistenceService : IAsyncDisposable
{
    private readonly ILogger _logger = Log.ForContext<MoongatePersistenceService>();
    private readonly HashSet<PersistenceDatabaseTarget> _registeredTargets = [];
    private readonly Lock _registrationSync = new();
    private readonly PersistenceModuleRegistry _registry = new();
    private readonly PersistenceSchemaCoordinator _schema;
    private readonly PersistenceLifetime _lifetime = new();
    private readonly Dictionary<PersistenceDatabaseTarget, PersistenceMutationGate> _gates = new()
    {
        [PersistenceDatabaseTarget.Accounts] = new(),
        [PersistenceDatabaseTarget.Realm] = new()
    };
    private readonly List<IPersistenceEntityRegistration> _sources = [];
    private readonly AsyncLocal<PersistenceTransaction?> _transaction = new();
    private readonly AsyncLocal<PersistenceCaptureState?> _capture = new();
    private bool _frozen;
    private int _moduleCount;
    private int _entityCount;

    /// <summary>Constructs an I/O-free persistence owner. Register all entities and modules before initialization.</summary>
    public MoongatePersistenceService(PostgreSqlPersistenceOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _schema = new PersistenceSchemaCoordinator(options, _registry);
    }

    /// <summary>Validates the complete registration batch and prepares schemas according to the configured policy.</summary>
    public Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        return RunSchemaAsync(async () =>
        {
            await _schema.InitializeAsync(cancellationToken).ConfigureAwait(false);
            _logger.Information("PostgreSQL persistence ready: {TargetCount} targets, {ModuleCount} modules, {EntityTypeCount} entity types",
                _registeredTargets.Count, _moduleCount, _entityCount);
        });
    }

    /// <summary>Returns pending schema DDL without requiring normal initialization to succeed.</summary>
    public Task<IReadOnlyList<PersistenceSchemaChange>> PreviewSchemaAsync(CancellationToken cancellationToken = default)
    {
        return RunOwnedAsync(() =>
        {
            Freeze();
            return _schema.PreviewAsync(cancellationToken);
        });
    }

    /// <summary>Explicitly applies schema changes for all registered modules.</summary>
    public Task SynchronizeSchemaAsync(CancellationToken cancellationToken = default)
    {
        return RunSchemaAsync(() => _schema.SynchronizeAsync(cancellationToken));
    }

    /// <summary>Executes a sequential callback in one target's asynchronous transaction.</summary>
    /// <remarks>Do not reenter this owner through standalone facades. Failures poison the transaction even if caught.
    /// No callback is retried; a failed commit acknowledgement may have an unknown durable outcome.</remarks>
    public Task ExecuteInTransactionAsync(PersistenceDatabaseTarget target, Func<IPersistenceTransaction, Task> operation, CancellationToken cancellationToken = default)
    {
        return RunOwnedAsync(async () =>
        {
            ArgumentNullException.ThrowIfNull(operation);
            EnsureReady();
            var database = _schema.GetDatabase(target);
            await _gates[target].RunAsync(token => ExecuteCoreAsync(database, operation, token), cancellationToken).ConfigureAwait(false);
            return true;
        });
    }

    /// <summary>Saves detached snapshots, assuming the caller provides safe ownership of live sources.</summary>
    public Task SaveAllAsync(CancellationToken cancellationToken = default)
    {
        return SaveAllAsync((capture, token) => { token.ThrowIfCancellationRequested(); capture(); return Task.CompletedTask; }, cancellationToken);
    }

    /// <summary>Captures sources on their owner loop and commits one independent transaction per target.</summary>
    /// <remarks>Invoke the capture action exactly once during each callback. Snapshot functions must deep-copy
    /// nested mutable values. Absence is not deletion. All captures for a target validate before its first write.
    /// The mutation gate spans capture through commit, including draining any already-started capture when
    /// its dispatcher returns early or fails. Targets do not share a distributed transaction.</remarks>
    public Task SaveAllAsync(Func<Action, CancellationToken, Task> captureAsync, CancellationToken cancellationToken = default)
    {
        return RunOwnedAsync(async () =>
        {
            ArgumentNullException.ThrowIfNull(captureAsync);
            EnsureReady();
            var started = Stopwatch.GetTimestamp();
            var savedCount = 0;
            foreach (var group in _sources.GroupBy(source => GetTarget(source.EntityType)).OrderBy(group => group.Key))
            {
                await _gates[group.Key].RunAsync(async token =>
                {
                    var targetStarted = Stopwatch.GetTimestamp();
                    var targetCount = 0;
                    var state = new PersistenceCaptureState();
                    var writes = new List<Func<PersistenceTransaction, CancellationToken, Task>>();
                    _capture.Value = state;
                    try
                    {
                        await captureAsync(() =>
                        {
                            state.BeginCapture();
                            var previousCapture = _capture.Value;
                            _capture.Value = state;
                            try
                            {
                                token.ThrowIfCancellationRequested();
                                foreach (var source in group)
                                {
                                    writes.Add(source.Capture(out var count));
                                    targetCount += count;
                                }
                                state.CompleteCapture();
                            }
                            catch
                            {
                                state.FailCapture();
                                throw;
                            }
                            finally
                            {
                                _capture.Value = previousCapture;
                                state.ExitCapture();
                            }
                        }, token).ConfigureAwait(false);
                        if (!await state.CloseAsync().ConfigureAwait(false))
                        {
                            throw new InvalidOperationException("Persistence capture must complete exactly once without reentry.");
                        }
                    }
                    finally
                    {
                        await state.CloseAsync().ConfigureAwait(false);
                        _capture.Value = null;
                    }

                    token.ThrowIfCancellationRequested();
                    await ExecuteCoreAsync(_schema.GetDatabase(group.Key), async transaction =>
                    {
                        foreach (var write in writes)
                        {
                            await write(transaction, token).ConfigureAwait(false);
                        }
                    }, token).ConfigureAwait(false);
                    savedCount += targetCount;
                    _logger.Information("PostgreSQL snapshot committed for {Target}: {EntityCount} entities in {ElapsedMilliseconds} ms",
                        group.Key, targetCount, Stopwatch.GetElapsedTime(targetStarted).TotalMilliseconds);
                }, cancellationToken).ConfigureAwait(false);
            }

            cancellationToken.ThrowIfCancellationRequested();
            _logger.Information("PostgreSQL world save completed: {EntityCount} entities in {ElapsedMilliseconds} ms",
                savedCount, Stopwatch.GetElapsedTime(started).TotalMilliseconds);
            return true;
        });
    }

    internal void RegisterModule(IPersistenceModule module)
    {
        lock (_registrationSync)
        {
            ThrowIfFrozen();
            _registry.RegisterModule(module);
            _moduleCount++;
            _registeredTargets.Add(module.DatabaseTarget);
        }
    }

    internal DataAccess<T> RegisterEntity<T>(Func<IEnumerable<T>>? source = null, Func<T, T>? snapshot = null) where T : class, IMoongateEntity
    {
        lock (_registrationSync)
        {
            ThrowIfFrozen();
            if ((source is null) != (snapshot is null))
            {
                throw new ArgumentException("A live source requires an explicit snapshot function.");
            }

            _registry.RegisterEntity(typeof(T));
            _entityCount++;
            if (source is not null)
            {
                _sources.Add(new PersistenceEntityRegistration<T>(source, snapshot!));
            }

            return new DataAccess<T>(this);
        }
    }

    internal PersistenceDatabaseTarget GetTarget(Type type)
    {
        return _schema.GetOwner(type).DatabaseTarget;
    }
    internal bool IsCurrentTransaction(PersistenceTransaction transaction)
    {
        return ReferenceEquals(_transaction.Value, transaction);
    }

    internal Task<TResult> RunOperationAsync<T, TResult>(bool mutation, Func<IFreeSql, DbTransaction?, CancellationToken, Task<TResult>> operation, CancellationToken cancellationToken) where T : class, IMoongateEntity
    {
        return RunOwnedAsync(async () =>
        {
            EnsureReady();
            cancellationToken.ThrowIfCancellationRequested();
            var target = GetTarget(typeof(T));
            var database = _schema.GetDatabase(target);
            return mutation
                ? await _gates[target].RunAsync(token => operation(database.Orm, null, token), cancellationToken).ConfigureAwait(false)
                : await operation(database.Orm, null, cancellationToken).ConfigureAwait(false);
        });
    }

    private Task RunSchemaAsync(Func<Task> operation)
    {
        return RunOwnedAsync(async () =>
        {
            Freeze();
            await operation().ConfigureAwait(false);
            return true;
        });
    }

    private async Task ExecuteCoreAsync(PostgreSqlDatabase database, Func<PersistenceTransaction, Task> operation, CancellationToken cancellationToken)
    {
        var connection = new NpgsqlConnection(database.RuntimeConnectionString);
        NpgsqlTransaction? native = null;
        PersistenceTransaction? scope = null;
        Exception? failure = null;
        try
        {
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
            native = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
            scope = new PersistenceTransaction(this, database, native, cancellationToken);
            _transaction.Value = scope;
            await operation(scope).ConfigureAwait(false);
            await scope.CompleteCallbackAsync().ConfigureAwait(false);

            cancellationToken.ThrowIfCancellationRequested();
            await native.CommitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            failure = exception;
            if (scope is not null)
            {
                scope.Fail(exception);
                try { await scope.CompleteCallbackAsync().ConfigureAwait(false); } catch { /* Preserve the original error. */ }
            }
            if (native is not null)
            {
                try { await native.RollbackAsync(CancellationToken.None).ConfigureAwait(false); } catch { /* Preserve the original error. */ }
            }
            throw;
        }
        finally
        {
            _transaction.Value = null;
            try
            {
                if (native is not null)
                {
                    await native.DisposeAsync().ConfigureAwait(false);
                }
            }
            catch when (failure is not null)
            {
                /* Preserve the original error. */
            }
            finally
            {
                try
                {
                    await connection.DisposeAsync().ConfigureAwait(false);
                }
                catch when (failure is not null)
                {
                    /* Preserve the original error. */
                }
            }
        }
    }

    private async Task<TResult> RunOwnedAsync<TResult>(Func<Task<TResult>> operation)
    {
        RejectReentry();
        return await _lifetime.RunAsync(operation).ConfigureAwait(false);
    }

    private void RejectReentry()
    {
        if (_transaction.Value is { } transaction)
        {
            var error = new InvalidOperationException("A transaction callback cannot reenter its persistence owner through standalone APIs.");
            transaction.Fail(error);
            throw error;
        }
        if (_capture.Value is { } capture)
        {
            capture.FailCapture();
            throw new InvalidOperationException("A capture callback cannot reenter persistence.");
        }
    }

    private void EnsureReady()
    {
        if (!_schema.IsReady)
        {
            throw new InvalidOperationException("Initialize persistence successfully before using its facades.");
        }
    }

    private void Freeze() { lock (_registrationSync) { _frozen = true; } }
    private void ThrowIfFrozen()
    {
        if (_frozen)
        {
            throw new InvalidOperationException("Persistence registration is frozen.");
        }
    }

    /// <summary>Rejects new work, drains admitted operations and then disposes shared database resources.</summary>
    public ValueTask DisposeAsync()
    {
        RejectReentry();
        Freeze();
        return new ValueTask(_lifetime.CloseAsync(async () =>
        {
            foreach (var gate in _gates.Values)
            {
                await gate.CloseAsync(() => Task.CompletedTask).ConfigureAwait(false);
            }

            await _schema.DisposeAsync().ConfigureAwait(false);
        }));
    }
}
