using Moongate.Network.Attributes;
using Moongate.Network.Interfaces;
using Moongate.Network.Types;
using Moongate.UO.Data.Hues;
using Moongate.UO.Data.Types;
using SquidStd.Network.Spans;

namespace Moongate.Network.Packets.Incoming;

/// <summary>
/// Unicode speech request (0xAD): the only speech packet a modern client (ClassicUO 7.x) sends —
/// ASCII TalkRequest (0x03) is intentionally not implemented. <see cref="IsEncoded" /> is true for
/// classic-client "encoded" speech (12-bit-packed keyword triggers for NPC menus); its payload has
/// no fixed size without decoding it, so <see cref="Read" /> stops at the type byte and returns an
/// empty result rather than guessing where the text starts. No NPC system exists yet to consume
/// keywords anyway.
/// </summary>
[PacketDocumentation(PacketFamilyType.Chat, IsVariableLength = true, Name = "Unicode Ascii Speech Request")]
public readonly record struct UnicodeSpeechPacket(bool IsEncoded, ChatMessageType Type, Hue Hue, string Text)
    : IIncomingPacket<UnicodeSpeechPacket>
{
    public static byte PacketId => 0xAD;

    public static UnicodeSpeechPacket Read(ref SpanReader reader)
    {
        reader.ReadByte();   // packet id
        reader.ReadUInt16(); // total length; the frame is already delimited to it
        var type = (ChatMessageType)reader.ReadByte();

        if ((type & ChatMessageType.Encoded) == ChatMessageType.Encoded)
        {
            return new(true, type, Hue.Default, string.Empty);
        }

        var hue = new Hue(reader.ReadUInt16());
        reader.ReadUInt16();     // font; no server-side font system
        reader.ReadAsciiSafe(4); // language; single-language server for now
        var text = reader.ReadBigUniSafe().Trim();

        return new(false, type, hue, text);
    }
}
