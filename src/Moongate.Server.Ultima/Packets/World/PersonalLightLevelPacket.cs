using Moongate.Core.Primitives;
using Moongate.Network.Packets.Attributes;
using Moongate.Network.Packets.Base;
using Moongate.Network.Packets.Interfaces;
using Moongate.Network.Packets.Spans;
using Moongate.Network.Packets.Types.Packets;

namespace Moongate.Server.Ultima.Packets.World;

/// <summary>
///     Sets the light level around one mobile, such as night sight (0x4E): 0 is full daylight, higher is darker.
/// </summary>
[PacketHandler(0x4E, PacketSizing.Fixed, Length = 6)]
public sealed class PersonalLightLevelPacket : BaseFixedPacket<PersonalLightLevelPacket>, IOutgoingPacket
{
    public Serial Serial { get; }

    public int Level { get; }

    public PersonalLightLevelPacket(Serial serial, int level)
    {
        Serial = serial;
        Level = level;
    }

    public void Write(ref PacketWriter writer)
    {
        writer.EnsureCapacity(Length);
        writer.WriteByte(OpCode);
        writer.WriteSerial(Serial);
        writer.WriteByte((byte)Level);
    }
}
