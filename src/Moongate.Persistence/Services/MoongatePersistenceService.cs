using Moongate.Core.Interfaces.Entities;
using Moongate.Persistence.Data;
using Moongate.Persistence.DataAccess;
using Moongate.Persistence.Interfaces.Internal;
using Moongate.Persistence.Internal;

namespace Moongate.Persistence.Services;

public sealed class MoongatePersistenceService : IAsyncDisposable
{
    private readonly Lock _lifecycleSync = new();
    private readonly SemaphoreSlim _saveGate = new(1, 1);
    private readonly AsyncLocal<bool> _insideSave = new();
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

    public async Task CheckpointAsync(CancellationToken cancellationToken = default)
    {
        IPersistenceCollection[] collections;
        lock (_lifecycleSync)
        {
            ThrowIfDisposed();
            ThrowIfFaulted();
            if (!_initialized)
            {
                throw new InvalidOperationException("Persistence has not been initialized.");
            }
            collections = [.. _collections];
        }

        await CheckpointCollectionsAsync(collections, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Persists registered live sources and checkpoints every collection.</summary>
    /// <remarks>
    /// Collections without a live source checkpoint their explicit upserts. Saves run sequentially;
    /// this is not a transaction across entities or collections. Cancellation or failure can leave
    /// earlier writes committed. Sources must not reenter SaveAllAsync or dispose this service.
    /// </remarks>
    public async Task SaveAllAsync(CancellationToken cancellationToken = default)
    {
        if (_insideSave.Value)
        {
            throw new InvalidOperationException("A live entity source cannot reenter SaveAllAsync.");
        }

        await _saveGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            IPersistenceCollection[] collections;
            lock (_lifecycleSync)
            {
                ThrowIfDisposed();
                ThrowIfFaulted();
                if (!_initialized)
                {
                    throw new InvalidOperationException("Persistence has not been initialized.");
                }
                collections = [.. _collections];
            }

            _insideSave.Value = true;
            foreach (var collection in collections)
            {
                cancellationToken.ThrowIfCancellationRequested();
                await collection.SaveAsync(cancellationToken).ConfigureAwait(false);
            }

            cancellationToken.ThrowIfCancellationRequested();
            await CheckpointCollectionsAsync(collections, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _insideSave.Value = false;
            _saveGate.Release();
        }
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
            var dataAccess = new DataAccess<T>(store, entitySource);
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

        await _saveGate.WaitAsync().ConfigureAwait(false);
        try
        {
            var failures = await CloseEveryCollectionAsync(abort: false).ConfigureAwait(false);
            ThrowFailures(failures);
        }
        finally
        {
            _saveGate.Release();
        }
    }

    public ValueTask DisposeAsync()
    {
        if (_insideSave.Value)
        {
            throw new InvalidOperationException("A live entity source cannot dispose its persistence service.");
        }

        lock (_lifecycleSync)
        {
            _disposed = true;
            _disposeTask ??= DisposeCoreAsync(_initializeTask);

            return new ValueTask(_disposeTask);
        }
    }
}
