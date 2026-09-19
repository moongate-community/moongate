using Moongate.Network.Packets.Interfaces;

namespace Moongate.Server.Core.Interfaces.Services;

/// <summary>Admits packet snapshots into bounded per-connection outbound queues without requiring a game session.</summary>
/// <remarks>Shutdown joins owned sends and disconnects and reports any cleanup failures.</remarks>
public interface IPacketSendService : IMoongateStartupService
{
    /// <summary>Encodes and queues a packet without waiting for socket I/O; returns false when unavailable or full.</summary>
    bool TrySend(long sessionId, IOutgoingPacket packet);

    /// <summary>Closes send admission immediately and joins the connection and outbound drain.</summary>
    /// <remarks>The service observes and owns cleanup even when callers cannot await it in a synchronous callback.</remarks>
    Task DisconnectAsync(long sessionId);
}
