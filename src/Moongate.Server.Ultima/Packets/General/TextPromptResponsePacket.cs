using System.Diagnostics.CodeAnalysis;
using System.Text;
using Moongate.Core.Primitives;
using Moongate.Network.Packets.Attributes;
using Moongate.Network.Packets.Base;
using Moongate.Network.Packets.Interfaces;
using Moongate.Network.Packets.Spans;
using Moongate.Network.Packets.Types.Packets;

namespace Moongate.Server.Ultima.Packets.General;

/// <summary>
///     The player's answer to a text prompt (0xC2): the prompt's id, whether it was escaped (type 0), a language code
///     and the text in little-endian UTF-16, read without its control characters as ModernUO does.
/// </summary>
[PacketHandler(0xC2, PacketSizing.Variable, MinimumLength = HeaderLength, Description = "Text prompt response")]
public sealed class TextPromptResponsePacket
    : BasePacket<TextPromptResponsePacket>, IIncomingPacket<TextPromptResponsePacket>
{
    private const int HeaderLength = 19;
    private const int LanguageLength = 4;

    public override int Length { get; }

    public required Serial Serial { get; init; }

    public required int PromptId { get; init; }

    /// <summary>
    ///     Gets whether the player left the prompt with escape instead of answering it.
    /// </summary>
    public required bool IsCancel { get; init; }

    public required string Text { get; init; }

    public TextPromptResponsePacket(int length)
    {
        Length = length;
    }

    public static bool TryParse(ReadOnlySpan<byte> data, [NotNullWhen(true)] out TextPromptResponsePacket? packet)
    {
        packet = null;

        if (!HasValidHeader(data))
        {
            return false;
        }

        var reader = new PacketReader(data[3..]);

        if (!reader.TryReadSerial(out var serial) ||
            !reader.TryReadUInt32BigEndian(out var promptId) ||
            !reader.TryReadUInt32BigEndian(out var type) ||
            !reader.TryReadBytes(LanguageLength, out _) ||
            !reader.TryReadBytes(reader.Remaining & ~1, out var text))
        {
            return false;
        }

        packet = new(data.Length)
        {
            Serial = serial, PromptId = unchecked((int)promptId), IsCancel = type == 0,
            Text = Clean(Encoding.Unicode.GetString(text))
        };

        return true;
    }

    // Up to the terminator, without the characters that are not text.
    private static string Clean(string text)
    {
        var end = text.IndexOf('\0');

        return new((end < 0 ? text : text[..end]).Where(character => !char.IsControl(character)).ToArray());
    }
}
