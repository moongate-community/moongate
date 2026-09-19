using System.Net.Sockets;

namespace Moongate.Network.Server.Internal;

/// <summary>Owns an admitted socket until preparation is transferred to a client.</summary>
internal sealed class AcceptedConnectionSetup
{
    public Socket Socket { get; }
    public TaskCompletionSource Completion { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public AcceptedConnectionSetup(Socket socket)
    {
        Socket = socket;
    }
}
