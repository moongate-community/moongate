using Moongate.Network.Packets.Spans;

namespace Moongate.Network.Packets.Interfaces;

/// <summary>
///     Writes one complete outgoing packet into a caller-owned buffer.
/// </summary>
public interface IOutgoingPacket : IPacket
{
    /// <summary>
    ///     Encodes the complete packet into the supplied writer.
    /// </summary>
    /// <param name="writer">
    ///     The writer for the caller-owned output buffer.
    /// </param>
    void Write(ref PacketWriter writer);
}
