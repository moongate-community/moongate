using Moongate.Network.Attributes;
using Moongate.Network.Interfaces;
using Moongate.Network.Types;
using SquidStd.Network.Spans;

namespace Moongate.Network.Packets.Outgoing;

/// <summary>
/// Character list update (0x86): the account's character list after it changed, so the client can
/// redraw the selection screen. Length is <c>4 + 60*SlotCount</c>. Unlike the login character list
/// (0xA9) it carries no starting cities.
/// </summary>
[PacketDocumentation(PacketFamilyType.Characters, Length = 304)]
public readonly record struct CharacterListUpdatePacket(IReadOnlyList<string> Characters, byte SlotCount)
    : IOutgoingPacket
{
    public const byte PacketId = 0x86;

    private const int SlotLength = 60; // name + password
    private const int HeaderLength = 4;

    public void Write(ref SpanWriter writer)
    {
        writer.Write(PacketId);
        writer.Write((ushort)(HeaderLength + SlotLength * SlotCount));
        writer.Write(SlotCount);

        for (var i = 0; i < SlotCount; i++)
        {
            writer.WriteAscii(i < Characters.Count ? Characters[i] : string.Empty, 30); // name
            writer.WriteAscii(string.Empty, 30);                                        // password
        }
    }
}
