using Moongate.Network.Packets.Interfaces;

namespace Moongate.Server.Core.Interfaces.Services;

/// <summary>
///     Admits typed incoming packets to the shared game loop inbox.
/// </summary>
/// <remarks>
///     Start after the game loop and plugin registration, before network listeners. Stop closes admission.
/// </remarks>
public interface IPacketDispatchService : IMoongateStartupService
{
    /// <summary>
    ///     Completes after detaching the client and retiring session membership, without closing the socket.
    /// </summary>
    /// <remarks>
    ///     Callers own observation and draining of the returned cleanup tasks. Retirement normally runs on the loop;
    ///     before dispatcher startup or after loop termination only transport and membership cleanup runs directly.
    ///     A caller already on the loop thread retires directly and must never block waiting on its own inbox.
    /// </remarks>
    Task DisconnectAsync(long sessionId);

    /// <summary>
    ///     Attempts bounded admission without running a handler inline or waiting for inbox space.
    /// </summary>
    bool TryDispatch(long sessionId, IPacket packet);
}
