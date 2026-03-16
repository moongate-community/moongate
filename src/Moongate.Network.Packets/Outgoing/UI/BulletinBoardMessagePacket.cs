using Moongate.Network.Packets.Base;
using Moongate.Network.Spans;
using Moongate.UO.Data.Ids;

namespace Moongate.Network.Packets.Outgoing.UI;

public sealed class BulletinBoardMessagePacket : BaseGameNetworkPacket
{
    private static readonly byte[] BodyHeader =
    [
        0x01, 0x91, 0x84, 0x0A, 0x06, 0x1E, 0xFD, 0x01, 0x0B, 0x15,
        0x2E, 0x01, 0x0B, 0x17, 0x0B, 0x01, 0xBB, 0x20, 0x46, 0x04,
        0x66, 0x13, 0xF8, 0x00, 0x00, 0x0E, 0x75, 0x00, 0x00
    ];

    public Serial BoardId { get; set; }

    public Serial MessageId { get; set; }

    public string Poster { get; set; } = string.Empty;

    public string Subject { get; set; } = string.Empty;

    public string PostedAtText { get; set; } = string.Empty;

    public List<string> BodyLines { get; } = [];

    public BulletinBoardMessagePacket()
        : base(0x71) { }

    public override void Write(ref SpanWriter writer)
    {
        writer.Write(OpCode);
        writer.Write((ushort)0);
        writer.Write((byte)2);
        writer.Write((uint)BoardId);
        writer.Write((uint)MessageId);
        BulletinBoardSummaryPacket.WriteAsciiLengthPrefixedNull(ref writer, Poster);
        BulletinBoardSummaryPacket.WriteAsciiLengthPrefixedNull(ref writer, Subject);
        BulletinBoardSummaryPacket.WriteAsciiLengthPrefixedNull(ref writer, PostedAtText);
        writer.Write(BodyHeader);
        writer.Write((byte)BodyLines.Count);

        foreach (var line in BodyLines)
        {
            BulletinBoardSummaryPacket.WriteAsciiLengthPrefixedNull(ref writer, line);
        }

        writer.WritePacketLength();
    }

    protected override bool ParsePayload(ref SpanReader reader)
        => throw new NotSupportedException();
}
