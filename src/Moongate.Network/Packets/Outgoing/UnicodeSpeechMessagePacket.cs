using Moongate.Core.Primitives;
using Moongate.Network.Attributes;
using Moongate.Network.Interfaces;
using Moongate.Network.Types;
using Moongate.UO.Data.Hues;
using Moongate.UO.Data.Types;
using SquidStd.Network.Spans;

namespace Moongate.Network.Packets.Outgoing;

/// <summary>
/// Unicode speech message (0xAE): the only outgoing chat packet, sent to every recipient of a
/// message including the speaker — never a reuse of the incoming packet, per real UO server
/// behavior (confirmed across ModernUO, UOX3 and polserver). <see cref="Speaker" /> is
/// <see cref="Serial.Zero" /> for system/broadcast messages, matching this codebase's existing
/// "zero is no entity" convention on <see cref="Serial" />.
/// </summary>
[PacketDocumentation(PacketFamilyType.Chat, IsVariableLength = true, Name = "Unicode Speech Message")]
public readonly record struct UnicodeSpeechMessagePacket(
    Serial Speaker,
    ushort Body,
    ChatMessageType Type,
    Hue Hue,
    string SpeakerName,
    string Text
) : IOutgoingPacket
{
    public const byte PacketId = 0xAE;

    private const ushort Font = 3; // constant, matches ModernUO's always-3 for its unicode rebroadcast
    private const string Language = "ENU";
    private const int HeaderLength = 48; // id(1)+length(2)+serial(4)+body(2)+type(1)+hue(2)+font(2)+lang(4)+name(30)

    public void Write(ref SpanWriter writer)
    {
        writer.Write(PacketId);
        writer.Write((ushort)(HeaderLength + (Text.Length + 1) * 2)); // + null-terminated big-endian text
        writer.Write(Speaker);
        writer.Write(Body);
        writer.Write((byte)Type);
        writer.Write(Hue);
        writer.Write(Font);
        writer.WriteAscii(Language, 4);
        writer.WriteAscii(SpeakerName, 30);
        writer.WriteBigUniNull(Text);
    }
}
