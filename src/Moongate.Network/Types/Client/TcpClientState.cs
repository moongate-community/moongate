namespace Moongate.Network.Types.Client;

internal enum TcpClientState
{
    Created,
    Running,
    Closing,
    Closed,
    Disposed
}
