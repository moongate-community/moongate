using Moongate.Network.Client;

namespace Moongate.Network.Data.Events;

/// <summary>
///     Event payload containing an exception raised by server or client network loops.
/// </summary>
public sealed class TcpExceptionEventArgs : EventArgs
{
    /// <summary>
    ///     Exception raised by the networking component.
    /// </summary>
    public Exception Exception { get; }

    /// <summary>
    ///     Client related to the exception, when available.
    /// </summary>
    public MoongateTcpClient? Client { get; }

    public TcpExceptionEventArgs(Exception exception, MoongateTcpClient? client = null)
    {
        Exception = exception;
        Client = client;
    }
}
