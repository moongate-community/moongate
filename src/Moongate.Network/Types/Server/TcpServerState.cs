namespace Moongate.Network.Types.Server;

internal enum TcpServerState
{
    Stopped,
    Starting,
    Running,
    Stopping,
    Disposed
}
