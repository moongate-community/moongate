using Moongate.Network.Attributes;
using Moongate.Network.Interfaces;
using Moongate.Network.Types;
using SquidStd.Network.Spans;

namespace Moongate.Network.Packets.Incoming;

/// <summary>
/// Delete character (0x83): the client asks to delete the character in the given slot. 39 bytes fixed.
/// The password field is a leftover from the days the client sent it here and is ignored.
/// </summary>
[PacketDocumentation(PacketFamilyType.Characters)]
public readonly record struct DeleteCharacterPacket(int Slot) : IIncomingPacket<DeleteCharacterPacket>
{
    public static byte PacketId => 0x83;

    public static DeleteCharacterPacket Read(ref SpanReader reader)
    {
        reader.ReadByte();    // packet id
        reader.ReadBytes(30); // password, unused
        var slot = reader.ReadInt32();
        reader.ReadUInt32(); // client IP

        return new(slot);
    }
}
