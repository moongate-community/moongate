using System.Diagnostics.CodeAnalysis;
using Moongate.Network.Packets.Attributes;
using Moongate.Network.Packets.Base;
using Moongate.Network.Packets.Interfaces;
using Moongate.Network.Packets.Spans;
using Moongate.Network.Packets.Types.Packets;

namespace Moongate.Server.Ultima.Packets.General;

/// <summary>
///     A text command of the client (0x12, variable): its kind and a text that depends on it. Kind 0x24 uses a skill,
///     the text starting with the number of the skill; the others, such as casting a spell or opening a door, are
///     read and not acted on yet.
/// </summary>
[PacketHandler(0x12, PacketSizing.Variable, MinimumLength = 3, Description = "Text command")]
public sealed class TextCommandPacket : BasePacket<TextCommandPacket>, IIncomingPacket<TextCommandPacket>
{
    /// <summary>
    ///     The kind of a command that uses a skill.
    /// </summary>
    public const byte UseSkill = 0x24;

    private const int HeaderLength = 4;

    public override int Length { get; }

    /// <summary>
    ///     Gets the kind of the command; 0 for a packet too short to carry one.
    /// </summary>
    public byte Kind { get; }

    /// <summary>
    ///     Gets the text of the command, up to its first zero byte.
    /// </summary>
    public string Text { get; }

    private TextCommandPacket(int length, byte kind, string text)
    {
        Length = length;
        Kind = kind;
        Text = text;
    }

    public static bool TryParse(ReadOnlySpan<byte> data, [NotNullWhen(true)] out TextCommandPacket? packet)
    {
        packet = null;

        if (!HasValidHeader(data))
        {
            return false;
        }

        if (data.Length < HeaderLength)
        {
            packet = new(data.Length, 0, "");

            return true;
        }

        var body = data[HeaderLength..];
        var end = body.IndexOf((byte)0);
        body = end < 0 ? body : body[..end];
        var reader = new PacketReader(body);
        packet = new(data.Length, data[HeaderLength - 1], reader.TryReadAscii(body.Length, out var text) ? text ?? "" : "");

        return true;
    }
}
