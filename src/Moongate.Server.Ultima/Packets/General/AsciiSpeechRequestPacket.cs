using System.Buffers.Binary;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using Moongate.Core.Primitives;
using Moongate.Network.Packets.Attributes;
using Moongate.Network.Packets.Base;
using Moongate.Network.Packets.Interfaces;
using Moongate.Network.Packets.Types.Packets;
using Moongate.Server.Ultima.Data.Speech;
using Moongate.Server.Ultima.Types.Speech;

namespace Moongate.Server.Ultima.Packets.General;

/// <summary>
///     Decodes an ASCII speech request (0x03).
/// </summary>
[PacketHandler(0x03, PacketSizing.Variable, MinimumLength = 9, Description = "ASCII speech request")]
public sealed class AsciiSpeechRequestPacket : BasePacket<AsciiSpeechRequestPacket>, IIncomingPacket<AsciiSpeechRequestPacket>
{
    public override int Length { get; }

    public SpeechRequestData Speech { get; }

    private AsciiSpeechRequestPacket(int length, SpeechRequestData speech)
    {
        Length = length;
        Speech = speech;
    }

    public static bool TryParse(ReadOnlySpan<byte> data, [NotNullWhen(true)] out AsciiSpeechRequestPacket? packet)
    {
        packet = null;

        if (!HasValidHeader(data))
        {
            return false;
        }

        var textBytes = data[8..];

        if (textBytes[^1] != 0 || textBytes[..^1].Contains((byte)0))
        {
            return false;
        }

        packet = new(
            data.Length,
            new(
                (SpeechType)data[3],
                new Hue(BinaryPrimitives.ReadUInt16BigEndian(data[4..6])),
                (SpeechFontType)BinaryPrimitives.ReadUInt16BigEndian(data[6..8]),
                "ENU",
                Encoding.Latin1.GetString(textBytes[..^1])
            )
        );

        return true;
    }
}
