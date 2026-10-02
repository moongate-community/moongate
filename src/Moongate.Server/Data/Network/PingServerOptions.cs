using System.Net;

namespace Moongate.Server.Data.Network;

/// <summary>
///     Settings of the UDP ping server.
/// </summary>
public sealed class PingServerOptions
{
    /// <summary>
    ///     Gets whether the server answers pings; when false it binds nothing.
    /// </summary>
    public bool Enabled { get; init; } = true;

    /// <summary>
    ///     Gets the UDP endpoints to bind, one socket each.
    /// </summary>
    public IReadOnlyList<IPEndPoint> Endpoints { get; init; } = [];

    /// <summary>
    ///     Gets the largest datagram that is answered, in bytes; a larger one is dropped.
    /// </summary>
    public int MaxDatagramSize { get; init; } = 64;
}
