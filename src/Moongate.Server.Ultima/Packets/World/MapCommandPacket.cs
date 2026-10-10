using Moongate.Network.Packets.Attributes;
using Moongate.Network.Packets.Base;
using Moongate.Network.Packets.Interfaces;
using Moongate.Network.Packets.Spans;
using Moongate.Network.Packets.Types.Packets;
using Moongate.Server.Ultima.Types.MapItems;

namespace Moongate.Server.Ultima.Packets.World;

/// <summary>
///     A command about an open map item (0x56): show it, draw a pin of its course at a point of the drawing, or answer
///     whether its course may be changed.
/// </summary>
[PacketHandler(0x56, PacketSizing.Fixed, Length = 11)]
public sealed class MapCommandPacket : BaseFixedPacket<MapCommandPacket>, IOutgoingPacket
{
    public uint Serial { get; }

    public MapCommandType Command { get; }

    public bool Flag { get; }

    public int X { get; }

    public int Y { get; }

    public MapCommandPacket(uint serial, MapCommandType command, bool flag, int x, int y)
    {
        Serial = serial;
        Command = command;
        Flag = flag;
        X = x;
        Y = y;
    }

    public void Write(ref PacketWriter writer)
    {
        writer.EnsureCapacity(Length);
        writer.WriteByte(OpCode);
        writer.WriteUInt32BigEndian(Serial);
        writer.WriteByte((byte)Command);
        writer.WriteByte(Flag ? (byte)1 : (byte)0);
        writer.WriteUInt16BigEndian(unchecked((ushort)X));
        writer.WriteUInt16BigEndian(unchecked((ushort)Y));
    }
}
