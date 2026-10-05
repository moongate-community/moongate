using Moongate.Core.Primitives;
using Moongate.Network.Packets.Attributes;
using Moongate.Network.Packets.Base;
using Moongate.Network.Packets.Interfaces;
using Moongate.Network.Packets.Spans;
using Moongate.Network.Packets.Types.Packets;
using Moongate.Server.Ultima.Data.BulletinBoards;
using Moongate.Server.Ultima.Packets.BulletinBoards.Internal;

namespace Moongate.Server.Ultima.Packets.BulletinBoards;

/// <summary>
///     A message of a bulletin board in full (0x71, sub-command 0x02): who posted it, what about and when, how the
///     poster looked, and its lines. The answer to a client that asked for it with sub-command 0x03.
/// </summary>
[PacketHandler(0x71, PacketSizing.Variable, MinimumLength = HeaderLength + 3 * 2 + 6, Description = "Bulletin board: message")]
public sealed class BulletinBoardMessagePacket : BasePacket<BulletinBoardMessagePacket>, IOutgoingPacket
{
    private const byte Subcommand = 0x02;
    private const int HeaderLength = 12;

    // The two counts are one byte each.
    private const int MaxCount = 255;

    private readonly byte[] _poster;
    private readonly byte[] _subject;
    private readonly byte[] _date;
    private readonly BulletinEquipment[] _equipment;
    private readonly byte[][] _lines;

    public override int Length { get; }

    public Serial Board { get; }

    public Serial Message { get; }

    public int Body { get; }

    public int Hue { get; }

    public BulletinBoardMessagePacket(
        Serial board,
        Serial message,
        string poster,
        string subject,
        string date,
        int body,
        int hue,
        IReadOnlyList<BulletinEquipment> equipment,
        IReadOnlyList<string> lines
    )
    {
        Board = board;
        Message = message;
        Body = body;
        Hue = hue;
        _poster = BulletinBoardText.Cut(poster, BulletinBoardText.MaxString);
        _subject = BulletinBoardText.Cut(subject, BulletinBoardText.MaxString);
        _date = BulletinBoardText.Cut(date, BulletinBoardText.MaxString);
        _equipment = equipment.Take(MaxCount).ToArray();
        _lines = lines.Take(MaxCount).Select(line => BulletinBoardText.Cut(line, BulletinBoardText.MaxLine)).ToArray();
        Length = HeaderLength + _poster.Length + _subject.Length + _date.Length + 3 * 2 +
                 4 + 1 + _equipment.Length * 4 +
                 1 + _lines.Sum(line => line.Length + 3);
    }

    public void Write(ref PacketWriter writer)
    {
        writer.EnsureCapacity(Length);
        writer.WriteByte(OpCode);
        writer.WriteUInt16BigEndian((ushort)Length);
        writer.WriteByte(Subcommand);
        writer.WriteSerial(Board);
        writer.WriteSerial(Message);
        WriteString(ref writer, _poster);
        WriteString(ref writer, _subject);
        WriteString(ref writer, _date);
        writer.WriteUInt16BigEndian(unchecked((ushort)Body));
        writer.WriteUInt16BigEndian(unchecked((ushort)Hue));
        writer.WriteByte((byte)_equipment.Length);

        foreach (var piece in _equipment)
        {
            writer.WriteUInt16BigEndian(unchecked((ushort)piece.ItemId));
            writer.WriteUInt16BigEndian(unchecked((ushort)piece.Hue));
        }

        writer.WriteByte((byte)_lines.Length);

        foreach (var line in _lines)
        {
            // Two zeros, counted in the length: with one, old clients lose the last letter of the line.
            writer.WriteByte((byte)(line.Length + 2));
            writer.WriteBytes(line);
            writer.WriteByte(0);
            writer.WriteByte(0);
        }
    }

    // The length counts the zero that ends the text.
    private static void WriteString(ref PacketWriter writer, byte[] text)
    {
        writer.WriteByte((byte)(text.Length + 1));
        writer.WriteBytes(text);
        writer.WriteByte(0);
    }
}
