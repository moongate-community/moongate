using Moongate.Network.Packets.Attributes;
using Moongate.Network.Packets.Base;
using Moongate.Network.Packets.Interfaces;
using Moongate.Network.Packets.Spans;
using Moongate.Network.Packets.Types.Packets;

namespace Moongate.Server.Ultima.Packets.World;

/// <summary>
///     Sets the light level of the whole world for the client (0x4F): 0 is full daylight, higher is darker.
/// </summary>
[PacketHandler(0x4F, PacketSizing.Fixed, Length = 2)]
public sealed class GlobalLightLevelPacket : BaseFixedPacket<GlobalLightLevelPacket>, IOutgoingPacket
{
    public int Level { get; }

    public GlobalLightLevelPacket(int level)
    {
        Level = level;
    }

    public void Write(ref PacketWriter writer)
    {
        writer.EnsureCapacity(Length);
        writer.WriteByte(OpCode);
        writer.WriteByte((byte)Level);
    }
}
