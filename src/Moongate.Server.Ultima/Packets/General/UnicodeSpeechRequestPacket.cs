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
///     Decodes a Unicode speech request (0xAD), including encoded-keyword text.
/// </summary>
[PacketHandler(0xAD, PacketSizing.Variable, MinimumLength = 14, Description = "Unicode speech request")]
public sealed class UnicodeSpeechRequestPacket : BasePacket<UnicodeSpeechRequestPacket>, IIncomingPacket<UnicodeSpeechRequestPacket>
{
    private const byte EncodedBit = 0xC0;
    private const int MaximumKeywords = 50;

    private static readonly Encoding StrictBigEndianUnicode = new UnicodeEncoding(true, false, true);
    private static readonly Encoding StrictUtf8 = new UTF8Encoding(false, true);

    public override int Length { get; }

    public SpeechRequestData Speech { get; }

    private UnicodeSpeechRequestPacket(int length, SpeechRequestData speech)
    {
        Length = length;
        Speech = speech;
    }

    public static bool TryParse(ReadOnlySpan<byte> data, [NotNullWhen(true)] out UnicodeSpeechRequestPacket? packet)
    {
        packet = null;

        if (!HasValidHeader(data))
        {
            return false;
        }

        var languageBytes = data[8..12];

        if (languageBytes[..3].IndexOfAnyExceptInRange((byte)'A', (byte)'Z') >= 0 ||
            languageBytes[3] is not (0 or (byte)' '))
        {
            return false;
        }

        var encoded = (data[3] & EncodedBit) != 0;
        var textBytes = data[12..];
        int[] keywords = [];

        if (encoded)
        {
            if (textBytes.Length < 3)
            {
                return false;
            }

            var keywordCount = BinaryPrimitives.ReadUInt16BigEndian(textBytes) >> 4;

            if (keywordCount > MaximumKeywords)
            {
                return false;
            }

            var packedKeywordBytes = keywordCount / 2 * 3 + keywordCount % 2;

            if (textBytes.Length < 2 + packedKeywordBytes + 1)
            {
                return false;
            }

            keywords = ReadKeywords(textBytes, keywordCount);
            textBytes = textBytes[(2 + packedKeywordBytes)..];
        }

        if (encoded ? textBytes[^1] != 0 || textBytes[..^1].Contains((byte)0) :
            textBytes.Length < 2 || textBytes.Length % 2 != 0 ||
            textBytes[^2] != 0 || textBytes[^1] != 0)
        {
            return false;
        }

        try
        {
            var text = encoded ? StrictUtf8.GetString(textBytes[..^1]) :
                StrictBigEndianUnicode.GetString(textBytes[..^2]);

            if (text.Contains('\0'))
            {
                return false;
            }

            packet = new(
                data.Length,
                new(
                    (SpeechType)(data[3] & ~EncodedBit),
                    new Hue(BinaryPrimitives.ReadUInt16BigEndian(data[4..6])),
                    (SpeechFontType)BinaryPrimitives.ReadUInt16BigEndian(data[6..8]),
                    Encoding.ASCII.GetString(languageBytes[..3]),
                    text
                ) { Keywords = keywords }
            );

            return true;
        }
        catch (DecoderFallbackException)
        {
            return false;
        }
    }

    // The ids are 12 bits each after the 12-bit count, as ModernUO reads them: the count's low nibble starts the first.
    private static int[] ReadKeywords(ReadOnlySpan<byte> textBytes, int count)
    {
        var keywords = new int[count];
        var hold = textBytes[1] & 0x0F;
        var offset = 2;

        for (var index = 0; index < count; index++)
        {
            if (index % 2 == 0)
            {
                keywords[index] = (hold << 8) | textBytes[offset++];
            }
            else
            {
                var value = BinaryPrimitives.ReadUInt16BigEndian(textBytes[offset..]);
                offset += 2;
                keywords[index] = value >> 4;
                hold = value & 0x0F;
            }
        }

        return keywords;
    }
}
