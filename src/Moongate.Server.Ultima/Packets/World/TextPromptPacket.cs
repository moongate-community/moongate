using Moongate.Core.Primitives;
using Moongate.Network.Packets.Attributes;
using Moongate.Network.Packets.Base;
using Moongate.Network.Packets.Interfaces;
using Moongate.Network.Packets.Spans;
using Moongate.Network.Packets.Types.Packets;

namespace Moongate.Server.Ultima.Packets.World;

/// <summary>
///     Asks the player for a line of text (0xC2, the Unicode prompt): the client lets it type in the journal line and
///     answers with the same packet id, carrying this prompt's id. As ModernUO sends it: 21 bytes, the last 10 zero.
/// </summary>
[PacketHandler(0xC2, PacketSizing.Variable, MinimumLength = PacketLength)]
public sealed class TextPromptPacket : BasePacket<TextPromptPacket>, IOutgoingPacket
{
    private const int PacketLength = 21;
    private const int Padding = 10;

    public override int Length => PacketLength;

    public Serial Serial { get; }

    public int PromptId { get; }

    public TextPromptPacket(Serial serial, int promptId)
    {
        Serial = serial;
        PromptId = promptId;
    }

    public void Write(ref PacketWriter writer)
    {
        writer.EnsureCapacity(Length);
        writer.WriteByte(OpCode);
        writer.WriteUInt16BigEndian((ushort)Length);
        writer.WriteSerial(Serial);
        writer.WriteUInt32BigEndian(unchecked((uint)PromptId));

        for (var i = 0; i < Padding; i++)
        {
            writer.WriteByte(0);
        }
    }
}
