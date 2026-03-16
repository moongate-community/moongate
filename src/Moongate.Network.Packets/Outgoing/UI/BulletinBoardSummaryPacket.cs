using System.Text;
using Moongate.Network.Packets.Base;
using Moongate.Network.Spans;
using Moongate.UO.Data.Ids;

namespace Moongate.Network.Packets.Outgoing.UI;

public sealed class BulletinBoardSummaryPacket : BaseGameNetworkPacket
{
    public Serial BoardId { get; set; }

    public Serial MessageId { get; set; }

    public Serial ParentId { get; set; }

    public string Poster { get; set; } = string.Empty;

    public string Subject { get; set; } = string.Empty;

    public string PostedAtText { get; set; } = string.Empty;

    public BulletinBoardSummaryPacket()
        : base(0x71) { }

    public override void Write(ref SpanWriter writer)
    {
        writer.Write(OpCode);
        writer.Write((ushort)0);
        writer.Write((byte)1);
        writer.Write((uint)BoardId);
        writer.Write((uint)MessageId);
        writer.Write((uint)ParentId);
        WriteAsciiLengthPrefixedNull(ref writer, Poster);
        WriteAsciiLengthPrefixedNull(ref writer, Subject);
        WriteAsciiLengthPrefixedNull(ref writer, PostedAtText);
        writer.WritePacketLength();
    }

    protected override bool ParsePayload(ref SpanReader reader)
        => throw new NotSupportedException();

    internal static void WriteAsciiLengthPrefixedNull(ref SpanWriter writer, string value)
    {
        var bytes = Encoding.ASCII.GetBytes(value ?? string.Empty);
        writer.Write((byte)(bytes.Length + 1));
        writer.Write(bytes);
        writer.Write((byte)0);
    }
}
