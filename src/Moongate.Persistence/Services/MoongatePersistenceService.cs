using Moongate.Core.Interfaces.Entities;
using Moongate.Persistence.Data;
using Moongate.Persistence.DataAccess;
using Moongate.Persistence.Interfaces.Internal;
using Moongate.Persistence.Internal;

namespace Moongate.Persistence.Services;

public sealed class MoongatePersistenceService : IAsyncDisposable
{
    private readonly Lock _lifecycleSync = new();
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
            var dataAccess = new DataAccess<T>(store);
            _collections.Add(dataAccess);

            return dataAccess;
        }
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

        List<Exception>? failures = null;
        foreach (var collection in collections)
        {
            try { await collection.CheckpointAsync(cancellationToken).ConfigureAwait(false); }
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

        var failures = await CloseEveryCollectionAsync(abort: false).ConfigureAwait(false);
        ThrowFailures(failures);
    }

    public ValueTask DisposeAsync()
    {
        lock (_lifecycleSync)
        {
            _disposed = true;
            _disposeTask ??= DisposeCoreAsync(_initializeTask);

            return new ValueTask(_disposeTask);
        }
    }
}
