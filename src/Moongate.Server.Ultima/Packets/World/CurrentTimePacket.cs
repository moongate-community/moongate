using Moongate.Network.Packets.Attributes;
using Moongate.Network.Packets.Base;
using Moongate.Network.Packets.Interfaces;
using Moongate.Network.Packets.Spans;
using Moongate.Network.Packets.Types.Packets;

namespace Moongate.Server.Ultima.Packets.World;

/// <summary>
///     Sets the time of day the client shows (0x5B).
/// </summary>
[PacketHandler(0x5B, PacketSizing.Fixed, Length = 4)]
public sealed class CurrentTimePacket : BaseFixedPacket<CurrentTimePacket>, IOutgoingPacket
{
    public TimeOnly Time { get; }

    public CurrentTimePacket(TimeOnly time)
    {
        Time = time;
    }

    public void Write(ref PacketWriter writer)
    {
        writer.EnsureCapacity(Length);
        writer.WriteByte(OpCode);
        writer.WriteByte((byte)Time.Hour);
        writer.WriteByte((byte)Time.Minute);
        writer.WriteByte((byte)Time.Second);
    }
}
