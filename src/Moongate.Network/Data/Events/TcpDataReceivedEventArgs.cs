using Moongate.Network.Client;

namespace Moongate.Network.Data.Events;

/// <summary>
///     Event payload containing data received from a network client.
/// </summary>
public sealed class TcpDataReceivedEventArgs : EventArgs
{
    /// <summary>
    ///     Source client for the data payload.
    /// </summary>
    public MoongateTcpClient Client { get; }

    /// <summary>
    ///     Stable payload copy that may be retained after the synchronous callback returns.
    /// </summary>
    public ReadOnlyMemory<byte> Data { get; }

    public TcpDataReceivedEventArgs(MoongateTcpClient client, ReadOnlyMemory<byte> data)
    {
        Client = client;
        Data = data;
    }
}
