using Moongate.Network.Client;

namespace Moongate.Network.Data.Events;

/// <summary>
///     Event payload containing a network client instance.
/// </summary>
public sealed class TcpClientEventArgs : EventArgs
{
    /// <summary>
    ///     Connected or disconnected client.
    /// </summary>
    public MoongateTcpClient Client { get; }

    public TcpClientEventArgs(MoongateTcpClient client)
    {
        Client = client;
    }
}
