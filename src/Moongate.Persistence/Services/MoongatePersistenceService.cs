using System.Data.Common;
using System.Diagnostics;
using Moongate.Core.Interfaces.Entities;
using Moongate.Core.Primitives;
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
    private int _entityCount;

    /// <summary>Constructs an I/O-free persistence owner. Register all entities and modules before initialization.</summary>
    public MoongatePersistenceService(PostgreSqlPersistenceOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _schema = new(options, _registry, _logger);
    }

    /// <summary>Reserves a nonzero Serial from a migration-managed sequence in the entity owner's schema.</summary>
    /// <remarks>Reservations are durable and are not reclaimed after failed saves. This does not allocate UO mobile/item ranges.</remarks>
    public Task<Serial> ReserveSerialAsync<T>(string sequenceName, CancellationToken cancellationToken = default)
        where T : class, IMoongateEntity
        => RunOperationAsync<T, Serial>(
            true,
            async (orm, _, token) =>
            {
                ArgumentException.ThrowIfNullOrWhiteSpace(sequenceName);
                var parts = sequenceName.Split('.');

                if (parts.Length != 2 ||
                    parts[0] != _schema.GetOwner(typeof(T)).Schema ||
                    parts[1].Length == 0 ||
                    !parts[1]
                        .All(
                            character => char.IsAsciiLetterLower(character) ||
                                         char.IsAsciiDigit(character) ||
                                         character == '_'
                        ))
                {
                    throw new ArgumentException(
                        "Use a lowercase schema-qualified sequence in the entity owner's schema.",
                        nameof(sequenceName)
                    );
                }

                var value = Convert.ToInt64(
                    await orm.Ado
                             .ExecuteScalarAsync(
                                 "SELECT nextval(CAST(@sequence AS regclass))",
                                 new { sequence = sequenceName },
                                 token
                             )
                             .ConfigureAwait(false)
                );

                if (value <= 0 || value > uint.MaxValue)
                {
                    throw new InvalidOperationException("The sequence returned a value outside the nonzero Serial range.");
                }

                return new Serial((uint)value);
            },
            cancellationToken
        );

    /// <summary>Executes a sequential callback in one target's asynchronous transaction.</summary>
    /// <remarks>
    /// Do not reenter this owner through standalone facades. Failures poison the transaction even if caught.
    /// No callback is retried; a failed commit acknowledgement may have an unknown durable outcome.
    /// </remarks>
    public Task ExecuteInTransactionAsync(
        PersistenceDatabaseTarget target,
        Func<IPersistenceTransaction, Task> operation,
        CancellationToken cancellationToken = default
    )
        => RunOwnedAsync(
            async () =>
            {
                ArgumentNullException.ThrowIfNull(operation);
                EnsureReady();
                var database = _schema.GetDatabase(target);
                await _gates[target]
                      .RunAsync(token => ExecuteCoreAsync(database, operation, token), cancellationToken)
                      .ConfigureAwait(false);

                return true;
            }
        );

    /// <summary>Checks every configured runtime database, validates registrations and prepares schemas according to the configured policy.</summary>
    public Task InitializeAsync(CancellationToken cancellationToken = default)
        => RunSchemaAsync(
            async () =>
            {
                await _schema.InitializeAsync(cancellationToken).ConfigureAwait(false);
                _logger.Information(
                    "PostgreSQL persistence ready: {TargetCount} targets, {ModuleCount} modules, {EntityTypeCount} entity types",
                    _registeredTargets.Count,
                    _registry.ModuleCount,
                    _entityCount
                );
            }
        );

    /// <summary>Returns pending schema DDL without requiring normal initialization to succeed.</summary>
    public Task<IReadOnlyList<PersistenceSchemaChange>> PreviewSchemaAsync(CancellationToken cancellationToken = default)
        => RunOwnedAsync(
            () =>
            {
                Freeze();

                return _schema.PreviewAsync(cancellationToken);
            }
        );

    /// <summary>Saves detached snapshots, assuming the caller provides safe ownership of live sources.</summary>
    public Task SaveAllAsync(CancellationToken cancellationToken = default)
        => SaveAllAsync(
            (capture, token) =>
            {
                token.ThrowIfCancellationRequested();
                capture();

                return Task.CompletedTask;
            },
            cancellationToken
        );

    /// <summary>Captures sources on their owner loop and commits one independent transaction per target.</summary>
    /// <remarks>
    /// Invoke the capture action exactly once during each callback. Snapshot functions must deep-copy
    /// nested mutable values. Absence is not deletion. All captures for a target validate before its first write.
    /// The mutation gate spans capture through commit, including draining any already-started capture when
    /// its dispatcher returns early or fails. Targets do not share a distributed transaction.
    /// </remarks>
    public Task SaveAllAsync(
        Func<Action, CancellationToken, Task> captureAsync,
        CancellationToken cancellationToken = default
    )
        => RunOwnedAsync(
            async () =>
            {
                ArgumentNullException.ThrowIfNull(captureAsync);
                EnsureReady();
                var started = Stopwatch.GetTimestamp();
                var savedCount = 0;

                foreach (var group in _sources.GroupBy(source => GetTarget(source.EntityType)).OrderBy(group => group.Key))
                {
                    await _gates[group.Key]
                          .RunAsync(
                              async token =>
                              {
                                  var targetStarted = Stopwatch.GetTimestamp();
                                  var targetCount = 0;
                                  var state = new PersistenceCaptureState();
                                  var writes = new List<Func<PersistenceTransaction, CancellationToken, Task>>();
                                  _capture.Value = state;

                                  try
                                  {
                                      await captureAsync(
                                              () =>
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
                                              },
                                              token
                                          )
                                          .ConfigureAwait(false);

                                      if (!await state.CloseAsync().ConfigureAwait(false))
                                      {
                                          throw new InvalidOperationException(
                                              "Persistence capture must complete exactly once without reentry."
                                          );
                                      }
                                  }
                                  finally
                                  {
                                      await state.CloseAsync().ConfigureAwait(false);
                                      _capture.Value = null;
                                  }

                                  token.ThrowIfCancellationRequested();
                                  await ExecuteCoreAsync(
                                          _schema.GetDatabase(group.Key),
                                          async transaction =>
                                          {
                                              foreach (var write in writes)
                                              {
                                                  await write(transaction, token).ConfigureAwait(false);
                                              }
                                          },
                                          token
                                      )
                                      .ConfigureAwait(false);
                                  savedCount += targetCount;
                                  _logger.Information(
                                      "PostgreSQL snapshot committed for {Target}: {EntityCount} entities in {ElapsedMilliseconds} ms",
                                      group.Key,
                                      targetCount,
                                      Stopwatch.GetElapsedTime(targetStarted).TotalMilliseconds
                                  );
                              },
                              cancellationToken
                          )
                          .ConfigureAwait(false);
                }

                cancellationToken.ThrowIfCancellationRequested();
                _logger.Information(
                    "PostgreSQL world save completed: {EntityCount} entities in {ElapsedMilliseconds} ms",
                    savedCount,
                    Stopwatch.GetElapsedTime(started).TotalMilliseconds
                );

                return true;
            }
        );

    /// <summary>Explicitly applies schema changes for all registered modules.</summary>
    public Task SynchronizeSchemaAsync(CancellationToken cancellationToken = default)
        => RunSchemaAsync(() => _schema.SynchronizeAsync(cancellationToken));

    internal PersistenceDatabaseTarget GetTarget(Type type)
        => _schema.GetOwner(type).DatabaseTarget;

    internal bool IsCurrentTransaction(PersistenceTransaction transaction)
        => ReferenceEquals(_transaction.Value, transaction);

    internal DataAccess<T> RegisterEntity<T>(
        Func<IEnumerable<T>>? source = null,
        Func<T, T>? snapshot = null,
        PersistenceDatabaseTarget? target = null
    ) where T : class, IMoongateEntity
    {
        lock (_registrationSync)
        {
            ThrowIfFrozen();

            if (source is null != snapshot is null)
            {
                throw new ArgumentException("A live source requires an explicit snapshot function.");
            }

            _registry.RegisterEntity(typeof(T), target);

            if (target.HasValue)
            {
                _registeredTargets.Add(target.Value);
            }

            _entityCount++;

            if (source is not null)
            {
                _sources.Add(new PersistenceEntityRegistration<T>(source, snapshot!));
            }

            return new(this);
        }
    }

    internal void RegisterModule(IPersistenceModule module)
    {
        lock (_registrationSync)
        {
            ThrowIfFrozen();
            _registry.RegisterModule(module);
            _registeredTargets.Add(module.DatabaseTarget);
        }
    }

    internal Task<TResult> RunOperationAsync<T, TResult>(
        bool mutation,
        Func<IFreeSql, DbTransaction?, CancellationToken, Task<TResult>> operation,
        CancellationToken cancellationToken
    ) where T : class, IMoongateEntity
        => RunOwnedAsync(
            async () =>
            {
                EnsureReady();
                cancellationToken.ThrowIfCancellationRequested();
                var target = GetTarget(typeof(T));
                var database = _schema.GetDatabase(target);

                return mutation
                           ? await _gates[target]
                                   .RunAsync(token => operation(database.Orm, null, token), cancellationToken)
                                   .ConfigureAwait(false)
                           : await operation(database.Orm, null, cancellationToken).ConfigureAwait(false);
            }
        );

    private void EnsureReady()
    {
        if (!_schema.IsReady)
        {
            throw new InvalidOperationException("Initialize persistence successfully before using its facades.");
        }
    }

    private async Task ExecuteCoreAsync(
        PostgreSqlDatabase database,
        Func<PersistenceTransaction, Task> operation,
        CancellationToken cancellationToken
    )
    {
        var connection = new NpgsqlConnection(database.RuntimeConnectionString);
        NpgsqlTransaction? native = null;
        PersistenceTransaction? scope = null;
        Exception? failure = null;

        try
        {
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
            native = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
            scope = new(this, database, native, cancellationToken);
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

                try
                {
                    await scope.CompleteCallbackAsync().ConfigureAwait(false);
                }
                catch
                {
                    /* Preserve the original error. */
                }
            }

            if (native is not null)
            {
                try
                {
                    await native.RollbackAsync(CancellationToken.None).ConfigureAwait(false);
                }
                catch
                {
                    /* Preserve the original error. */
                }
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

    private void Freeze()
    {
        lock (_registrationSync)
        {
            _frozen = true;
        }
    }

    private void RejectReentry()
    {
        if (_transaction.Value is { } transaction)
        {
            var error = new InvalidOperationException(
                "A transaction callback cannot reenter its persistence owner through standalone APIs."
            );
            transaction.Fail(error);

            throw error;
        }

        if (_capture.Value is { } capture)
        {
            capture.FailCapture();

            throw new InvalidOperationException("A capture callback cannot reenter persistence.");
        }
    }

    private async Task<TResult> RunOwnedAsync<TResult>(Func<Task<TResult>> operation)
    {
        RejectReentry();

        return await _lifetime.RunAsync(operation).ConfigureAwait(false);
    }

    private Task RunSchemaAsync(Func<Task> operation)
        => RunOwnedAsync(
            async () =>
            {
                Freeze();
                await operation().ConfigureAwait(false);

                return true;
            }
        );

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

        return new(
            _lifetime.CloseAsync(
                async () =>
                {
                    foreach (var gate in _gates.Values)
                    {
                        await gate.CloseAsync(() => Task.CompletedTask).ConfigureAwait(false);
                    }

                    await _schema.DisposeAsync().ConfigureAwait(false);
                }
            )
        );
    }
}
