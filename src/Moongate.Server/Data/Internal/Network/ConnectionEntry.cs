using Moongate.Network.Interfaces.Client;

namespace Moongate.Server.Data.Internal.Network;

internal sealed class ConnectionEntry
{
    public INetworkConnection Connection { get; }
    public TaskCompletionSource CloseRequest { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public TaskCompletionSource Cleanup { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public bool IsClosing { get; set; }

    public ConnectionEntry(INetworkConnection connection)
    {
        Connection = connection;
    }
}
