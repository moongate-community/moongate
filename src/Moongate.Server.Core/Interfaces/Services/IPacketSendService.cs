using Moongate.Network.Packets.Interfaces;
using Moongate.Network.Interfaces.Client;

namespace Moongate.Server.Core.Interfaces.Services;

/// <summary>Admits packet snapshots into bounded per-connection outbound queues without requiring a game session.</summary>
/// <remarks>Shutdown joins owned sends and disconnects and reports any cleanup failures.</remarks>
public interface IPacketSendService : IMoongateStartupService
{
    /// <summary>Closes send admission immediately and joins the connection and outbound drain.</summary>
    /// <remarks>The service observes and owns cleanup even when callers cannot await it in a synchronous callback.</remarks>
    Task DisconnectAsync(long sessionId);

    /// <summary>Closes only the expected connection when a session ID has been reused.</summary>
    Task DisconnectAsync(long sessionId, INetworkConnection expectedConnection)
        => expectedConnection.CloseAsync();

    /// <summary>Encodes and queues a packet without waiting for socket I/O; returns false when unavailable or full.</summary>
    bool TrySend(long sessionId, IOutgoingPacket packet);

    /// <summary>Admits a packet only when the session ID still belongs to the expected connection.</summary>
    bool TrySend(long sessionId, INetworkConnection expectedConnection, IOutgoingPacket packet);

    /// <summary>Sends a final packet after queued frames, then closes the expected connection.</summary>
    /// <returns>True only when the final packet reached the transport before it closed.</returns>
    Task<bool> SendAndDisconnectAsync(
        long sessionId,
        INetworkConnection expectedConnection,
        IOutgoingPacket packet,
        CancellationToken cancellationToken = default
    );
}
