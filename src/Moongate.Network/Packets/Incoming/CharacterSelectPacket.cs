using Moongate.Network.Attributes;
using Moongate.Network.Interfaces;
using Moongate.Network.Types;
using SquidStd.Network.Spans;

namespace Moongate.Network.Packets.Incoming;

/// <summary>
/// Play character / character login (0x5D): the client picks an existing character slot to enter the
/// world with. 73 bytes fixed. Only the character name and the chosen slot are meaningful to us.
/// </summary>
[PacketDocumentation(PacketFamilyType.Characters)]
public readonly record struct CharacterSelectPacket(string Name, int Slot)
    : IIncomingPacket<CharacterSelectPacket>
{
    public static byte PacketId => 0x5D;

    public static CharacterSelectPacket Read(ref SpanReader reader)
    {
        reader.ReadByte();   // packet id
        reader.ReadUInt32(); // 0xEDEDEDED pattern
        var name = reader.ReadAscii(30);
        reader.ReadUInt16();  // unknown
        reader.ReadInt32();   // client flags
        reader.ReadBytes(24); // unknown
        var slot = reader.ReadInt32();
        reader.ReadUInt32(); // client IP

        return new(name, slot);
    }
}
