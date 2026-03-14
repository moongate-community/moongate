using Moongate.Network.Packets.Attributes;
using Moongate.Network.Packets.Base;
using Moongate.Network.Packets.Data.BulletinBoard;
using Moongate.Network.Packets.Types.Packets;
using Moongate.Network.Spans;

namespace Moongate.Network.Packets.Incoming.Interaction;

[PacketHandler(0x71, PacketSizing.Variable, Description = "Bulletin Board Messages")]

/// <summary>
/// Represents BulletinBoardMessagesPacket.
/// </summary>
public class BulletinBoardMessagesPacket : BaseGameNetworkPacket
{
    public BulletinBoardSubcommand Subcommand { get; private set; }

    public uint BoardId { get; private set; }

    public uint MessageId { get; private set; }

    public uint ParentId { get; private set; }

    public string Subject { get; private set; } = string.Empty;

    public IReadOnlyList<string> BodyLines => _bodyLines;

    private readonly List<string> _bodyLines = [];

    public BulletinBoardMessagesPacket()
        : base(0x71) { }

    protected override bool ParsePayload(ref SpanReader reader)
    {
        if (reader.Remaining < 3)
        {
            return false;
        }

        var length = reader.ReadUInt16();

        if (length < 4 || length != reader.Length)
        {
            return false;
        }

        Subcommand = (BulletinBoardSubcommand)reader.ReadByte();
        _bodyLines.Clear();

        switch (Subcommand)
        {
            case BulletinBoardSubcommand.RequestMessage:
            case BulletinBoardSubcommand.RequestMessageSummary:
                return TryParseMessageRequest(ref reader);
            case BulletinBoardSubcommand.PostMessage:
                return TryParsePostMessage(ref reader);
            case BulletinBoardSubcommand.RemovePostedMessage:
                return TryParseRemoveMessage(ref reader);
            default:
                return false;
        }
    }

    private bool TryParseMessageRequest(ref SpanReader reader)
    {
        if (reader.Remaining != 8)
        {
            return false;
        }

        BoardId = reader.ReadUInt32();
        MessageId = reader.ReadUInt32();

        return reader.Remaining == 0;
    }

    private bool TryParsePostMessage(ref SpanReader reader)
    {
        if (reader.Remaining < 10)
        {
            return false;
        }

        BoardId = reader.ReadUInt32();
        ParentId = reader.ReadUInt32();

        var subjectLength = reader.ReadByte();

        if (subjectLength == 0 || subjectLength > reader.Remaining)
        {
            return false;
        }

        Subject = ReadAsciiNullTerminated(ref reader, subjectLength);

        if (reader.Remaining < 1)
        {
            return false;
        }

        var lineCount = reader.ReadByte();

        for (var i = 0; i < lineCount; i++)
        {
            if (reader.Remaining < 1)
            {
                return false;
            }

            var lineLength = reader.ReadByte();

            if (lineLength == 0 || lineLength > reader.Remaining)
            {
                return false;
            }

            _bodyLines.Add(ReadAsciiNullTerminated(ref reader, lineLength));
        }

        return reader.Remaining == 0;
    }

    private bool TryParseRemoveMessage(ref SpanReader reader)
    {
        if (reader.Remaining != 8)
        {
            return false;
        }

        BoardId = reader.ReadUInt32();
        MessageId = reader.ReadUInt32();

        return reader.Remaining == 0;
    }

    private static string ReadAsciiNullTerminated(ref SpanReader reader, int byteLength)
    {
        var raw = reader.ReadBytes(byteLength);
        var value = global::System.Text.Encoding.ASCII.GetString(raw);

        return value.TrimEnd('\0');
    }
}
