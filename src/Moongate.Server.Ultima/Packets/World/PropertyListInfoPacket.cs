using Moongate.Core.Primitives;
using Moongate.Network.Packets.Attributes;
using Moongate.Network.Packets.Base;
using Moongate.Network.Packets.Interfaces;
using Moongate.Network.Packets.Spans;
using Moongate.Network.Packets.Types.Packets;

namespace Moongate.Server.Ultima.Packets.World;

/// <summary>
///     Tells the client the current revision of an object's tooltip (0xDC); a client whose copy differs asks for it
///     with 0xD6. As ModernUO, the revision is the list's hash with bit 30 set, which the client masks off.
/// </summary>
[PacketHandler(0xDC, PacketSizing.Fixed, Length = 9)]
public sealed class PropertyListInfoPacket : BaseFixedPacket<PropertyListInfoPacket>, IOutgoingPacket
{
    public Serial Serial { get; }

    public int Hash { get; }

    public PropertyListInfoPacket(Serial serial, int hash)
    {
        Serial = serial;
        Hash = hash;
    }

    public void Write(ref PacketWriter writer)
    {
        writer.EnsureCapacity(Length);
        writer.WriteByte(OpCode);
        writer.WriteSerial(Serial);
        writer.WriteUInt32BigEndian((uint)(Hash | 0x40000000));
    }
}
