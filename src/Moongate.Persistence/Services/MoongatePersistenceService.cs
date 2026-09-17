using Moongate.Core.Interfaces.Entities;
using Moongate.Persistence.Data;
using Moongate.Persistence.Data.Internal;
using Moongate.Persistence.DataAccess;
using Moongate.Persistence.Interfaces.Internal;
using Moongate.Persistence.Internal;

namespace Moongate.Persistence.Services;

public sealed class MoongatePersistenceService : IAsyncDisposable
{
    private readonly Lock _lifecycleSync = new();
    private readonly PersistenceMutationGate _mutationGate = new();
    private readonly string _directory;
    private readonly PersistenceOptions _options;
    private readonly List<IPersistenceCollection> _collections = [];
    private readonly HashSet<string> _collectionNames = new(StringComparer.Ordinal);
    private readonly HashSet<Type> _entityTypes = [];
    private bool _initializationStarted;
    private bool _initialized;
    private bool _disposed;
    private Exception? _fault;
    private Task? _initializeTask;
    private Task? _disposeTask;

    public MoongatePersistenceService(string directory, PersistenceOptions? options = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);
        _directory = directory;
        _options = options ?? new PersistenceOptions();
    }

    public DataAccess<T> Register<T>(string collectionName) where T : class, IMoongateEntity
    {
        return RegisterCore<T>(collectionName, null);
    }

    /// <summary>Registers a collection and a live entity source evaluated by each SaveAllAsync call.</summary>
    /// <remarks>
    /// The caller must synchronize source enumeration and entity mutation. Entities absent from the
    /// source are retained; deletions remain explicit. Register before initialization begins.
    /// </remarks>
    public DataAccess<T> Register<T>(string collectionName, Func<IEnumerable<T>> entitySource)
        where T : class, IMoongateEntity
    {
        ArgumentNullException.ThrowIfNull(entitySource);
        return RegisterCore(collectionName, entitySource);
    }

    public Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        lock (_lifecycleSync)
        {
            ThrowIfDisposed();
            ThrowIfFaulted();
            if (_initialized)
            {
                return Task.CompletedTask;
            }

            if (_initializeTask is not null)
            {
                return _initializeTask;
            }
            _initializationStarted = true;
            _initializeTask = InitializeCoreAsync(cancellationToken);

            return _initializeTask;
        }
    }

    public Task CheckpointAsync(CancellationToken cancellationToken = default)
    {
        lock (_lifecycleSync)
        {
            ThrowIfDisposed();
            ThrowIfFaulted();
            if (!_initialized)
            {
                throw new InvalidOperationException("Persistence has not been initialized.");
            }
            IPersistenceCollection[] collections = [.. _collections];

            return _mutationGate.RunAsync(
                token => CheckpointCollectionsAsync(collections, token),
                cancellationToken
            );
        }
    }

    /// <summary>Persists registered live sources and checkpoints every collection.</summary>
    /// <remarks>
    /// Collections without a live source checkpoint their explicit upserts. Saves run sequentially;
    /// this is not a transaction across entities or collections. Cancellation or failure can leave
    /// earlier writes committed. Sources must not reenter persistence mutations, checkpoints,
    /// SaveAllAsync, or disposal.
    /// </remarks>
    public Task SaveAllAsync(CancellationToken cancellationToken = default)
    {
        return SaveAllAsync(
            static (capture, _) =>
            {
                capture();
                return Task.CompletedTask;
            },
            cancellationToken
        );
    }

    /// <summary>Captures registered live sources in caller-controlled context, then persists them.</summary>
    /// <remarks>
    /// The callback must invoke its supplied action exactly once and await that invocation before
    /// returning. All sources are serialized before any captured payload is committed. Mutations,
    /// checkpoints, saves, and disposal are ordered around the complete capture and commit sequence.
    /// </remarks>
    public Task SaveAllAsync(
        Func<Action, CancellationToken, Task> captureAsync,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentNullException.ThrowIfNull(captureAsync);
        lock (_lifecycleSync)
        {
            ThrowIfDisposed();
            ThrowIfFaulted();
            if (!_initialized)
            {
                throw new InvalidOperationException("Persistence has not been initialized.");
            }
            IPersistenceCollection[] collections = [.. _collections];

            return _mutationGate.RunAsync(
                token => SaveAllCoreAsync(collections, captureAsync, token),
                cancellationToken
            );
        }
    }

    private async Task SaveAllCoreAsync(
        IPersistenceCollection[] collections,
        Func<Action, CancellationToken, Task> captureAsync,
        CancellationToken cancellationToken
    )
    {
        const int awaitingCapture = 0;
        const int capturing = 1;
        const int captured = 2;
        const int closed = 3;
        const int failed = 4;
        CapturedPersistenceCollection[]? capturedCollections = null;
        var captureState = awaitingCapture;
        var invalidInvocation = 0;
        Action capture = () =>
        {
            if (Interlocked.CompareExchange(ref captureState, capturing, awaitingCapture) != awaitingCapture)
            {
                Volatile.Write(ref invalidInvocation, 1);
                throw new InvalidOperationException(
                    "The persistence capture action must be invoked exactly once."
                );
            }

            try
            {
                _mutationGate.RunCapture(
                    () => capturedCollections = CaptureAllCollections(collections, cancellationToken)
                );
                if (Interlocked.CompareExchange(ref captureState, captured, capturing) != capturing)
                {
                    Volatile.Write(ref invalidInvocation, 1);
                    throw new InvalidOperationException(
                        "The persistence capture action completed after its callback returned."
                    );
                }
            }
            catch
            {
                Interlocked.CompareExchange(ref captureState, failed, capturing);
                throw;
            }
        };

        int completedState;
        try
        {
            await captureAsync(capture, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            completedState = Interlocked.Exchange(ref captureState, closed);
        }

        if (completedState != captured ||
            Volatile.Read(ref invalidInvocation) != 0 ||
            capturedCollections is null)
        {
            throw new InvalidOperationException(
                "The persistence capture action must be invoked exactly once before its callback returns."
            );
        }

        cancellationToken.ThrowIfCancellationRequested();
        foreach (var snapshot in capturedCollections)
        {
            await snapshot.Collection.CommitAsync(snapshot.Payloads, cancellationToken).ConfigureAwait(false);
        }

        cancellationToken.ThrowIfCancellationRequested();
        await CheckpointCollectionsAsync(collections, cancellationToken).ConfigureAwait(false);
    }

    private DataAccess<T> RegisterCore<T>(string collectionName, Func<IEnumerable<T>>? entitySource)
        where T : class, IMoongateEntity
    {
        lock (_lifecycleSync)
        {
            ThrowIfDisposed();
            if (_initializationStarted)
            {
                throw new InvalidOperationException("Collections cannot be registered after initialization begins.");
            }
            PersistencePaths.ValidateCollectionName(collectionName);
            if (!_collectionNames.Add(collectionName))
            {
                throw new InvalidOperationException($"Collection name '{collectionName}' is already registered.");
            }

            if (!_entityTypes.Add(typeof(T)))
            {
                _collectionNames.Remove(collectionName);
                throw new InvalidOperationException($"Entity type '{typeof(T).FullName}' is already registered.");
            }

            var store = new BinaryCollectionStore(_directory, collectionName, _options);
            var dataAccess = new DataAccess<T>(store, _mutationGate, entitySource);
            _collections.Add(dataAccess);

            return dataAccess;
        }
    }

    private static async Task CheckpointCollectionsAsync(
        IPersistenceCollection[] collections, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        List<Exception>? failures = null;
        foreach (var collection in collections)
        {
            try { await collection.CheckpointAsync(cancellationToken).ConfigureAwait(false); }
            catch (OperationCanceledException exception) when (cancellationToken.IsCancellationRequested)
            {
                if (failures is null)
                {
                    throw;
                }
                failures.Add(exception);
                break;
            }
            catch (Exception exception) { (failures ??= []).Add(exception); }
        }
        ThrowFailures(failures);
    }

    private static CapturedPersistenceCollection[] CaptureAllCollections(
        IPersistenceCollection[] collections,
        CancellationToken cancellationToken
    )
    {
        var captured = new CapturedPersistenceCollection[collections.Length];
        for (var index = 0; index < collections.Length; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var collection = collections[index];
            captured[index] = new CapturedPersistenceCollection(
                collection,
                collection.Capture(cancellationToken)
            );
        }

        cancellationToken.ThrowIfCancellationRequested();

        return captured;
    }

    private async Task InitializeCoreAsync(CancellationToken cancellationToken)
    {
        try
        {
            foreach (var collection in _collections)
            {
                await collection.InitializeAsync(cancellationToken).ConfigureAwait(false);
            }
            lock (_lifecycleSync)
            {
                _initialized = true;
            }
        }
        catch (Exception exception)
        {
            var failures = new List<Exception> { exception };
            failures.AddRange(await CloseEveryCollectionAsync(abort: true).ConfigureAwait(false));
            var failure = failures.Count == 1 ? exception : new AggregateException(failures);
            lock (_lifecycleSync)
            {
                _fault = failure;
            }

            throw failure;
        }
    }

    private async Task<List<Exception>> CloseEveryCollectionAsync(bool abort)
    {
        List<Exception> failures = [];
        foreach (var collection in _collections)
        {
            try
            {
                if (abort)
                {
                    await collection.AbortAsync().ConfigureAwait(false);
                }
                else
                {
                    await collection.DisposeAsync().ConfigureAwait(false);
                }
            }
            catch (Exception exception) { failures.Add(exception); }
        }

        return failures;
    }

    private static void ThrowFailures(List<Exception>? failures)
    {
        if (failures is null || failures.Count == 0)
        {
            return;
        }

        if (failures.Count == 1)
        {
            throw failures[0];
        }

        throw new AggregateException(failures);
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
    }

    private void ThrowIfFaulted()
    {
        if (_fault is not null)
        {
            throw new InvalidOperationException("Persistence initialization failed.", _fault);
        }
    }

    private async Task DisposeCoreAsync(Task? initialization)
    {
        if (initialization is not null)
        {
            await initialization.ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);
        }

        var failures = await CloseEveryCollectionAsync(abort: false).ConfigureAwait(false);
        ThrowFailures(failures);
    }

    public ValueTask DisposeAsync()
    {
        if (_mutationGate.IsInsideCapture)
        {
            throw new InvalidOperationException("A live entity source cannot dispose its persistence service.");
        }

        lock (_lifecycleSync)
        {
            _disposed = true;
            _disposeTask ??= _mutationGate.CloseAsync(() => DisposeCoreAsync(_initializeTask));

            return new ValueTask(_disposeTask);
        }
    }
}
