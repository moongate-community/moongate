using Moongate.Network.Packets.Spans;

namespace Moongate.Network.Packets.Interfaces;

/// <summary>
///     Writes one complete outgoing packet into a caller-owned buffer.
/// </summary>
public interface IOutgoingPacket : IPacket
{
    void Write(ref PacketWriter writer);
}
