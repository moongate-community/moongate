using System.Diagnostics.CodeAnalysis;
using Moongate.Network.Interfaces.Client;
using Moongate.Server.Core.Interfaces.Services;
using Moongate.Server.Data.Internal.Network;
using Serilog;

namespace Moongate.Server.Services.Network;

/// <summary>Tracks connections independently of game sessions and owns their complete cleanup lifetime.</summary>
public sealed class ConnectionService : IConnectionService
{
    private readonly Lock _gate = new();
    private readonly Dictionary<long, ConnectionEntry> _entries = new();
    private readonly List<Exception> _failures = [];
    private readonly ILogger _logger = Log.ForContext<ConnectionService>();
    private bool _running;
    private bool _stopped;
    private Task? _stopTask;

    /// <inheritdoc />
    public int Count
    {
        get { lock (_gate) { return _entries.Count; } }
    }

    /// <inheritdoc />
    public Task StartAsync()
    {
        lock (_gate)
        {
            if (_stopped) { throw new InvalidOperationException("The connection registry cannot restart after shutdown."); }
            _running = true;
        }
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task StopAsync()
    {
        lock (_gate)
        {
            _running = false;
            _stopped = true;
            return _stopTask ??= StopCoreAsync(_entries.Values.ToArray());
        }
    }

    /// <inheritdoc />
    public IReadOnlyCollection<INetworkConnection> GetAll()
    {
        lock (_gate) { return _entries.Values.Select(entry => entry.Connection).ToArray(); }
    }

    /// <inheritdoc />
    public bool TryRegister(INetworkConnection connection)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ConnectionEntry entry;
        lock (_gate)
        {
            if (!_running || !connection.IsConnected) { return false; }
            if (_entries.TryGetValue(connection.SessionId, out var existing))
            {
                return !existing.IsClosing && ReferenceEquals(existing.Connection, connection);
            }
            entry = new ConnectionEntry(connection);
            _entries.Add(connection.SessionId, entry);
        }
        _ = ObserveAsync(entry);
        return true;
    }

    /// <inheritdoc />
    public bool TryGet(long sessionId, [NotNullWhen(true)] out INetworkConnection? connection)
    {
        lock (_gate)
        {
            if (_running && _entries.TryGetValue(sessionId, out var entry) &&
                !entry.IsClosing && entry.Connection.IsConnected)
            {
                connection = entry.Connection;
                return true;
            }
            connection = null;
            return false;
        }
    }

    /// <inheritdoc />
    public Task DisconnectAsync(long sessionId)
    {
        lock (_gate)
        {
            if (!_entries.TryGetValue(sessionId, out var entry)) { return Task.CompletedTask; }
            RequestClose(entry);
            return entry.Cleanup.Task;
        }
    }

    private void RequestClose(ConnectionEntry entry)
    {
        lock (_gate)
        {
            if (entry.IsClosing) { return; }
            entry.IsClosing = true;
        }
        _ = CloseCoreAsync(entry);
    }

    private async Task CloseCoreAsync(ConnectionEntry entry)
    {
        // Publish ownership and close admission before invoking any transport code, including from callbacks.
        await Task.CompletedTask.ConfigureAwait(ConfigureAwaitOptions.ForceYielding);
        try
        {
            await entry.Connection.CloseAsync().ConfigureAwait(false);
            entry.CloseRequest.TrySetResult();
        }
        catch (Exception exception)
        {
            entry.CloseRequest.TrySetException(exception);
        }
    }

    private async Task ObserveAsync(ConnectionEntry entry)
    {
        await Task.CompletedTask.ConfigureAwait(ConfigureAwaitOptions.ForceYielding);
        List<Exception> failures = [];
        try { await entry.Connection.Completion.ConfigureAwait(false); }
        catch (Exception exception) { failures.Add(exception); }

        RequestClose(entry);
        try { await entry.CloseRequest.Task.ConfigureAwait(false); }
        catch (Exception exception) { failures.Add(exception); }

        lock (_gate)
        {
            if (_entries.TryGetValue(entry.Connection.SessionId, out var current) && ReferenceEquals(current, entry))
            {
                _entries.Remove(entry.Connection.SessionId);
            }
            _failures.AddRange(failures);
            if (failures.Count == 0) { entry.Cleanup.TrySetResult(); }
            else
            {
                entry.Cleanup.TrySetException(new AggregateException(failures));
                // Keep failures available to explicit callers and StopAsync while observing remote-only cleanup.
                _ = entry.Cleanup.Task.Exception;
            }
        }
        foreach (var failure in failures)
        {
            _logger.Error(failure, "Connection cleanup failed for session {SessionId}", entry.Connection.SessionId);
        }
    }

    private async Task StopCoreAsync(ConnectionEntry[] entries)
    {
        await Task.CompletedTask.ConfigureAwait(ConfigureAwaitOptions.ForceYielding);
        foreach (var entry in entries) { RequestClose(entry); }
        await Task.WhenAll(entries.Select(entry => entry.Cleanup.Task))
                  .ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);
        Exception[] failures;
        lock (_gate) { failures = _failures.ToArray(); }
        if (failures.Length > 0) { throw new AggregateException(failures); }
    }
}
