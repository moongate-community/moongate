using Moongate.Core.Primitives;
using Moongate.Persistence.Data;
using Moongate.Persistence.Interfaces.Internal;
using Moongate.Persistence.Types.Persistence;

namespace Moongate.Persistence.Internal;

internal sealed class BinaryCollectionStore : IAsyncDisposable
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly Lock _closeSync = new();
    private readonly Dictionary<Serial, byte[]> _entries = new();
    private readonly BinaryCollectionFiles _files;
    private readonly long _checkpointThreshold;
    private readonly int _maxPayloadBytes;
    private bool _initialized;
    private Exception? _fault;
    private ulong _sequence;
    private int _closing;
    private Task? _closeTask;

    public BinaryCollectionStore(string directory, string collectionName, PersistenceOptions options)
        : this(directory, collectionName, options, new PhysicalPersistenceFileSystem())
    {
    }

    internal BinaryCollectionStore(string directory, string collectionName, PersistenceOptions options, IPersistenceFileSystem fileSystem)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (options.MaxPayloadBytes <= 0 || options.MaxPayloadBytes > Array.MaxLength)
        {
            throw new ArgumentOutOfRangeException(nameof(options), "Payload limit must be positive and fit a byte array.");
        }

        if (options.JournalCheckpointThresholdBytes <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(options), "Checkpoint threshold must be positive.");
        }
        _checkpointThreshold = options.JournalCheckpointThresholdBytes;
        _maxPayloadBytes = options.MaxPayloadBytes;
        _files = new BinaryCollectionFiles(new PersistencePaths(directory, collectionName), fileSystem);
    }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfClosing();
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            ThrowIfClosing();
            if (_fault is not null)
            {
                throw new InvalidOperationException("Collection is faulted; reopen it.", _fault);
            }

            if (_initialized)
            {
                return;
            }
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                _sequence = _files.Initialize(_entries, _maxPayloadBytes, cancellationToken);
                _initialized = true;
            }
            catch (Exception exception)
            {
                _fault = exception;
                _entries.Clear();
                _files.Dispose();
                throw;
            }
        }
        finally { _gate.Release(); }
    }

    public byte[]? Get(Serial id)
    {
        ThrowIfClosing();
        _gate.Wait();
        try
        {
            EnsureReady();
            return _entries.GetValueOrDefault(id);
        }
        finally { _gate.Release(); }
    }

    public byte[][] Capture()
    {
        ThrowIfClosing();
        _gate.Wait();
        try
        {
            EnsureReady();
            return [.. _entries.Values];
        }
        finally { _gate.Release(); }
    }

    public KeyValuePair<Serial, byte[]>[] CaptureEntries()
    {
        ThrowIfClosing();
        _gate.Wait();
        try
        {
            EnsureReady();
            return [.. _entries];
        }
        finally { _gate.Release(); }
    }

    public async Task<byte[][]> CaptureAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfClosing();
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            EnsureReady();
            cancellationToken.ThrowIfCancellationRequested();
            return [.. _entries.Values];
        }
        finally { _gate.Release(); }
    }

    public async Task UpsertAsync(Serial id, byte[] payload, CancellationToken cancellationToken = default)
    {
        ThrowIfClosing();
        ValidateId(id);
        ArgumentNullException.ThrowIfNull(payload);
        if (payload.Length == 0 || payload.Length > _maxPayloadBytes)
        {
            throw new ArgumentOutOfRangeException(nameof(payload), "Payload must be nonempty and within the configured limit.");
        }
        var captured = payload.ToArray();
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            EnsureReady();
            cancellationToken.ThrowIfCancellationRequested();
            var sequence = checked(_sequence + 1);
            try
            {
                CheckpointIfNeeded();
                cancellationToken.ThrowIfCancellationRequested();
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception exception) { _fault = exception; throw; }
            try
            {
                _files.Append(PersistenceOperation.Upsert, sequence, id, captured);
                _entries[id] = captured;
                _sequence = sequence;
            }
            catch (Exception exception) { _fault = exception; throw; }
        }
        finally { _gate.Release(); }
    }

    public async Task<bool> DeleteAsync(Serial id, CancellationToken cancellationToken = default)
    {
        ThrowIfClosing();
        ValidateId(id);
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            EnsureReady();
            cancellationToken.ThrowIfCancellationRequested();
            if (!_entries.ContainsKey(id))
            {
                return false;
            }
            var sequence = checked(_sequence + 1);
            try
            {
                CheckpointIfNeeded();
                cancellationToken.ThrowIfCancellationRequested();
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception exception) { _fault = exception; throw; }
            try
            {
                _files.Append(PersistenceOperation.Delete, sequence, id, []);
                _entries.Remove(id);
                _sequence = sequence;
                return true;
            }
            catch (Exception exception) { _fault = exception; throw; }
        }
        finally { _gate.Release(); }
    }

    public async Task CheckpointAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfClosing();
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            EnsureReady();
            cancellationToken.ThrowIfCancellationRequested();
            try { _files.Checkpoint(_sequence, _entries); }
            catch (Exception exception) { _fault = exception; throw; }
        }
        finally { _gate.Release(); }
    }

    public Task AbortAsync()
    {
        return BeginClose(checkpoint: false);
    }

    private static void ValidateId(Serial id)
    {
        if (!id.IsValid)
        {
            throw new ArgumentOutOfRangeException(nameof(id), "Serial must be nonzero.");
        }
    }

    private void ThrowIfClosing()
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _closing) != 0, this);
    }

    private void EnsureReady()
    {
        ThrowIfClosing();
        if (_fault is not null)
        {
            throw new InvalidOperationException("Collection is faulted; reopen it.", _fault);
        }

        if (!_initialized)
        {
            throw new InvalidOperationException("Collection has not been initialized.");
        }
    }

    private void CheckpointIfNeeded()
    {
        if (_files.JournalLength >= _checkpointThreshold)
        {
            _files.Checkpoint(_sequence, _entries);
        }
    }

    private Task BeginClose(bool checkpoint)
    {
        lock (_closeSync)
        {
            Interlocked.Exchange(ref _closing, 1);
            return _closeTask ??= CloseAsync(checkpoint);
        }
    }

    private async Task CloseAsync(bool checkpoint)
    {
        await _gate.WaitAsync().ConfigureAwait(false);
        try
        {
            if (checkpoint && _initialized && _fault is null)
            {
                try { _files.Checkpoint(_sequence, _entries); }
                catch (Exception exception) { _fault = exception; throw; }
            }
        }
        finally
        {
            try { _files.Dispose(); }
            finally { _gate.Release(); }
        }
    }

    public ValueTask DisposeAsync()
    {
        return new ValueTask(BeginClose(checkpoint: true));
    }
}
