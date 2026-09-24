using Moongate.Network.Interfaces.Client;
using Moongate.Server.Core.Data.Network.Events;
using Moongate.Server.Core.Interfaces.Services;

namespace Moongate.Tests.TestSupport.Network;

internal sealed class NetworkServiceStub : INetworkService
{
    private readonly IConnectionService _connections;
    private readonly Lock _gate = new();
    private readonly HashSet<ControlledNetworkConnection> _clients = [];
    public event EventHandler<NetworkConnectionEventArgs>? ConnectionAccepted;
    public event EventHandler<NetworkConnectionEventArgs>? ConnectionClosed;
    public event EventHandler<NetworkDataEventArgs>? DataReceived;

    public Func<Task> OnStart { get; set; } = () => Task.CompletedTask;
    public Func<Task> OnStop { get; set; } = () => Task.CompletedTask;

    public int SubscriberCount
        => (ConnectionAccepted?.GetInvocationList().Length ?? 0) +
           (ConnectionClosed?.GetInvocationList().Length ?? 0) +
           (DataReceived?.GetInvocationList().Length ?? 0);

    public NetworkServiceStub(IConnectionService connections)
    {
        _connections = connections;
    }

    public void Accept(ControlledNetworkConnection connection)
    {
        if (!_connections.TryRegister(connection))
        {
            throw new InvalidOperationException("Registration failed.");
        }

        lock (_gate)
        {
            _clients.Add(connection);
        }

        ConnectionAccepted?.Invoke(this, new(connection));
    }

    public void Close(ControlledNetworkConnection connection)
    {
        lock (_gate)
        {
            if (!_clients.Remove(connection))
            {
                return;
            }
        }

        ConnectionClosed?.Invoke(this, new(connection));

        // Completion deliberately follows the callback, as it does in the real transport.
        connection.Complete();
    }

    public void Receive(INetworkConnection connection, ReadOnlyMemory<byte> data)
        => DataReceived?.Invoke(this, new(connection, data));

    public Task StartAsync()
        => OnStart();

    public async Task StopAsync()
    {
        await OnStop();
        ControlledNetworkConnection[] clients;

        lock (_gate)
        {
            clients = _clients.ToArray();
        }

        foreach (var client in clients)
        {
            Close(client);
        }

        await Task.WhenAll(clients.Select(client => _connections.DisconnectAsync(client.SessionId)));
    }
}
