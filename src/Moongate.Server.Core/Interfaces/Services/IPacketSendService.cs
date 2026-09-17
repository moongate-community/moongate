using Moongate.Network.Packets.Interfaces;

namespace Moongate.Server.Core.Interfaces.Services;

/// <summary>Admits packet snapshots into bounded per-connection outbound queues.</summary>
public interface IPacketSendService : IMoongateStartupService
{
    /// <summary>Encodes and queues a packet without waiting for socket I/O; returns false when unavailable or full.</summary>
    bool TrySend(long sessionId, IOutgoingPacket packet);

    /// <summary>Closes the connection and awaits its owned outbound drain. Never wait inside transport callbacks.</summary>
    Task DisconnectAsync(long sessionId);
}
