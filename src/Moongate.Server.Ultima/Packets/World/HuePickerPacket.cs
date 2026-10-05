using Moongate.Network.Packets.Attributes;
using Moongate.Network.Packets.Base;
using Moongate.Network.Packets.Interfaces;
using Moongate.Network.Packets.Spans;
using Moongate.Network.Packets.Types.Packets;

namespace Moongate.Server.Ultima.Packets.World;

/// <summary>
///     Opens the client's hue picker (0x95) with an id the answer must carry and the graphic shown in it, as ModernUO
///     sends it: the id is the picker's own, not the serial of an item.
/// </summary>
[PacketHandler(0x95, PacketSizing.Fixed, Length = 9)]
public sealed class HuePickerPacket : BaseFixedPacket<HuePickerPacket>, IOutgoingPacket
{
    public int PickerId { get; }

    public ushort Graphic { get; }

    public HuePickerPacket(int pickerId, ushort graphic)
    {
        PickerId = pickerId;
        Graphic = graphic;
    }

    public void Write(ref PacketWriter writer)
    {
        writer.EnsureCapacity(Length);
        writer.WriteByte(OpCode);
        writer.WriteUInt32BigEndian(unchecked((uint)PickerId));
        writer.WriteUInt16BigEndian(0);
        writer.WriteUInt16BigEndian(Graphic);
    }
}
