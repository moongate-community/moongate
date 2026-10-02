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
///     Decodes a Unicode speech request (0xAD), including encoded-keyword text. As in the other emulators the text and
///     the language are read leniently: a missing terminator, a badly encoded character or an odd language code never
///     refuses the packet, since a refused packet disconnects the client. Only a truncated keyword list is refused.
/// </summary>
[PacketHandler(0xAD, PacketSizing.Variable, MinimumLength = 14, Description = "Unicode speech request")]
public sealed class UnicodeSpeechRequestPacket : BasePacket<UnicodeSpeechRequestPacket>, IIncomingPacket<UnicodeSpeechRequestPacket>
{
    private const byte EncodedBit = 0xC0;
    private const int MaximumKeywords = 50;

    private const string DefaultLanguage = "ENU";

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
        var encoded = (data[3] & EncodedBit) != 0;
        var textBytes = data[12..];
        int[] keywords = [];

        if (encoded)
        {
            if (textBytes.Length < 2)
            {
                return false;
            }

            var keywordCount = BinaryPrimitives.ReadUInt16BigEndian(textBytes) >> 4;

            if (keywordCount > MaximumKeywords)
            {
                return false;
            }

            var packedKeywordBytes = keywordCount / 2 * 3 + keywordCount % 2;

            if (textBytes.Length < 2 + packedKeywordBytes)
            {
                return false;
            }

            keywords = ReadKeywords(textBytes, keywordCount);
            textBytes = textBytes[(2 + packedKeywordBytes)..];
        }

        var text = encoded ? ReadUtf8(textBytes) : ReadBigEndianUnicode(textBytes);

        packet = new(
            data.Length,
            new(
                (SpeechType)(data[3] & ~EncodedBit),
                new Hue(BinaryPrimitives.ReadUInt16BigEndian(data[4..6])),
                (SpeechFontType)BinaryPrimitives.ReadUInt16BigEndian(data[6..8]),
                ReadLanguage(languageBytes),
                text
            ) { Keywords = keywords }
        );

        return true;
    }

    // Three ASCII letters in any case are a language; anything else (empty, digits, two letters) is English.
    private static string ReadLanguage(ReadOnlySpan<byte> bytes)
    {
        Span<char> code = stackalloc char[3];

        for (var index = 0; index < 3; index++)
        {
            var letter = (char)(bytes[index] & ~0x20);

            if (letter is < 'A' or > 'Z')
            {
                return DefaultLanguage;
            }

            code[index] = letter;
        }

        return new(code);
    }

    // To the terminator, or to the end when the client left it out; a byte that is not UTF-8 becomes U+FFFD.
    private static string ReadUtf8(ReadOnlySpan<byte> bytes)
    {
        var end = bytes.IndexOf((byte)0);

        return Encoding.UTF8.GetString(end < 0 ? bytes : bytes[..end]);
    }

    // To the terminator, or to the last whole character when the client left it out; a lone surrogate becomes U+FFFD.
    private static string ReadBigEndianUnicode(ReadOnlySpan<byte> bytes)
    {
        var end = bytes.Length - bytes.Length % 2;

        for (var index = 0; index < end; index += 2)
        {
            if (bytes[index] == 0 && bytes[index + 1] == 0)
            {
                end = index;

                break;
            }
        }

        return Encoding.BigEndianUnicode.GetString(bytes[..end]);
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
