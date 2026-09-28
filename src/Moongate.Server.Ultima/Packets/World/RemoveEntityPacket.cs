using Moongate.Core.Primitives;
using Moongate.Network.Packets.Attributes;
using Moongate.Network.Packets.Base;
using Moongate.Network.Packets.Interfaces;
using Moongate.Network.Packets.Spans;
using Moongate.Network.Packets.Types.Packets;

namespace Moongate.Server.Ultima.Packets.World;

/// <summary>
///     Removes an item or a mobile from the client (0x1D).
/// </summary>
[PacketHandler(0x1D, PacketSizing.Fixed, Length = 5)]
public sealed class RemoveEntityPacket : BaseFixedPacket<RemoveEntityPacket>, IOutgoingPacket
{
    public Serial Serial { get; }

    public RemoveEntityPacket(Serial serial)
    {
        Serial = serial;
    }

    public void Write(ref PacketWriter writer)
    {
        writer.EnsureCapacity(Length);
        writer.WriteByte(OpCode);
        writer.WriteSerial(Serial);
    }
}
