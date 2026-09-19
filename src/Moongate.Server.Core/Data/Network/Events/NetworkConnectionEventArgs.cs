using Moongate.Network.Interfaces.Client;

namespace Moongate.Server.Core.Data.Network.Events;

/// <summary>A synchronous notification about an admitted transport connection.</summary>
public sealed class NetworkConnectionEventArgs : EventArgs
{
    /// <summary>Gets the connection whose lifecycle changed.</summary>
    public INetworkConnection Connection { get; }

    public NetworkConnectionEventArgs(INetworkConnection connection)
    {
        Connection = connection;
    }
}
