using System.Diagnostics.CodeAnalysis;
using System.Text;
using Moongate.Core.Primitives;
using Moongate.Network.Packets.Attributes;
using Moongate.Network.Packets.Base;
using Moongate.Network.Packets.Interfaces;
using Moongate.Network.Packets.Spans;
using Moongate.Network.Packets.Types.Packets;
using Moongate.Server.Ultima.Types.BulletinBoards;

namespace Moongate.Server.Ultima.Packets.BulletinBoards;

/// <summary>
///     What a client asks of a bulletin board (0x71, sub-commands 0x03 to 0x06): the text or the summary of a
///     message, the removal of one, or a new post with its subject and lines.
/// </summary>
[PacketHandler(0x71, PacketSizing.Variable, MinimumLength = HeaderLength, Description = "Bulletin board request")]
public sealed class BulletinBoardRequestPacket
    : BasePacket<BulletinBoardRequestPacket>, IIncomingPacket<BulletinBoardRequestPacket>
{
    private const int HeaderLength = 12;

    public override int Length { get; }

    public BulletinBoardCommandType Command { get; }

    public Serial Board { get; }

    /// <summary>
    ///     The message asked for or removed; on a post, the message it replies to, zero for a new thread.
    /// </summary>
    public Serial Message { get; }

    /// <summary>
    ///     The subject of a post; empty on the other requests.
    /// </summary>
    public string Subject { get; }

    /// <summary>
    ///     The lines of a post; none on the other requests.
    /// </summary>
    public IReadOnlyList<string> Lines { get; }

    private BulletinBoardRequestPacket(
        int length,
        BulletinBoardCommandType command,
        Serial board,
        Serial message,
        string subject,
        IReadOnlyList<string> lines
    )
    {
        Length = length;
        Command = command;
        Board = board;
        Message = message;
        Subject = subject;
        Lines = lines;
    }

    public static bool TryParse(ReadOnlySpan<byte> data, [NotNullWhen(true)] out BulletinBoardRequestPacket? packet)
    {
        packet = null;

        if (!HasValidHeader(data))
        {
            return false;
        }

        var reader = new PacketReader(data[3..]);

        if (!reader.TryReadByte(out var command) ||
            !Enum.IsDefined((BulletinBoardCommandType)command) ||
            !reader.TryReadSerial(out var board) ||
            !reader.TryReadSerial(out var message))
        {
            return false;
        }

        if ((BulletinBoardCommandType)command != BulletinBoardCommandType.Post)
        {
            packet = new(data.Length, (BulletinBoardCommandType)command, board, message, "", []);

            return true;
        }

        if (!TryReadString(ref reader, out var subject) || !reader.TryReadByte(out var count))
        {
            return false;
        }

        var lines = new List<string>(count);

        for (var index = 0; index < count; index++)
        {
            if (!TryReadString(ref reader, out var line))
            {
                return false;
            }

            lines.Add(line);
        }

        packet = new(data.Length, BulletinBoardCommandType.Post, board, message, subject, lines);

        return true;
    }

    // A length byte, then that many bytes: the text ends at the first zero among them.
    private static bool TryReadString(ref PacketReader reader, out string text)
    {
        text = "";

        if (!reader.TryReadByte(out var length) || !reader.TryReadBytes(length, out var bytes))
        {
            return false;
        }

        var end = bytes.IndexOf((byte)0);
        text = Encoding.UTF8.GetString(end < 0 ? bytes : bytes[..end]);

        return true;
    }
}
