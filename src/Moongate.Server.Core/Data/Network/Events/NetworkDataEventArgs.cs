using Moongate.Network.Interfaces.Client;

namespace Moongate.Server.Core.Data.Network.Events;

/// <summary>Synchronously exposes borrowed transport memory. Decode or copy it before returning.</summary>
public sealed class NetworkDataEventArgs : EventArgs
{
    /// <summary>Gets the admitted connection that received the bytes.</summary>
    public INetworkConnection Connection { get; }

    /// <summary>Gets bytes valid only during the callback. Never queue this memory for later use.</summary>
    public ReadOnlyMemory<byte> Data { get; }

    public NetworkDataEventArgs(INetworkConnection connection, ReadOnlyMemory<byte> data)
    {
        Connection = connection;
        Data = data;
    }
}
