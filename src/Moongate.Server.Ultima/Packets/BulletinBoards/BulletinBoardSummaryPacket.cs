using Moongate.Core.Primitives;
using Moongate.Network.Packets.Attributes;
using Moongate.Network.Packets.Base;
using Moongate.Network.Packets.Interfaces;
using Moongate.Network.Packets.Spans;
using Moongate.Network.Packets.Types.Packets;
using Moongate.Server.Ultima.Packets.BulletinBoards.Internal;

namespace Moongate.Server.Ultima.Packets.BulletinBoards;

/// <summary>
///     The line of a message in the list of a bulletin board (0x71, sub-command 0x01): who posted it, what about and
///     when, and the thread it replies to. The answer to a client that asked for it with sub-command 0x04.
/// </summary>
[PacketHandler(
    0x71,
    PacketSizing.Variable,
    MinimumLength = HeaderLength + 3 * 2,
    Description = "Bulletin board: message summary"
)]
public sealed class BulletinBoardSummaryPacket : BasePacket<BulletinBoardSummaryPacket>, IOutgoingPacket
{
    private const byte Subcommand = 0x01;
    private const int HeaderLength = 16;

    private readonly byte[] _poster;
    private readonly byte[] _subject;
    private readonly byte[] _date;

    public override int Length { get; }

    public Serial Board { get; }

    public Serial Message { get; }

    /// <summary>
    ///     The first message of the thread this one replies to; zero when it is that first message.
    /// </summary>
    public Serial Thread { get; }

    public BulletinBoardSummaryPacket(
        Serial board, Serial message, Serial thread, string poster, string subject, string date
    )
    {
        Board = board;
        Message = message;
        Thread = thread;
        _poster = BulletinBoardText.Cut(poster, BulletinBoardText.MaxString);
        _subject = BulletinBoardText.Cut(subject, BulletinBoardText.MaxString);
        _date = BulletinBoardText.Cut(date, BulletinBoardText.MaxString);
        Length = HeaderLength + _poster.Length + _subject.Length + _date.Length + 3 * 2;
    }

    public void Write(ref PacketWriter writer)
    {
        writer.EnsureCapacity(Length);
        writer.WriteByte(OpCode);
        writer.WriteUInt16BigEndian((ushort)Length);
        writer.WriteByte(Subcommand);
        writer.WriteSerial(Board);
        writer.WriteSerial(Message);
        writer.WriteSerial(Thread);
        WriteString(ref writer, _poster);
        WriteString(ref writer, _subject);
        WriteString(ref writer, _date);
    }

    // The length counts the zero that ends the text.
    private static void WriteString(ref PacketWriter writer, byte[] text)
    {
        writer.WriteByte((byte)(text.Length + 1));
        writer.WriteBytes(text);
        writer.WriteByte(0);
    }
}
