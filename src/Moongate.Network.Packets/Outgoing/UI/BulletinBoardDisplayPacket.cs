using System.Text;
using Moongate.Network.Packets.Base;
using Moongate.Network.Spans;
using Moongate.UO.Data.Ids;

namespace Moongate.Network.Packets.Outgoing.UI;

public sealed class BulletinBoardDisplayPacket : BaseGameNetworkPacket
{
    public const uint DefaultListSerial = 0x402000FFu;
    public const string DefaultBoardName = "bulletin board";
    public const int FixedBoardNameLength = 22;

    public Serial BoardId { get; set; }

    public string BoardName { get; set; } = DefaultBoardName;

    public BulletinBoardDisplayPacket()
        : base(0x71) { }

    public BulletinBoardDisplayPacket(Serial boardId, string? boardName = null)
        : this()
    {
        BoardId = boardId;
        BoardName = string.IsNullOrWhiteSpace(boardName) ? DefaultBoardName : boardName;
    }

    public override void Write(ref SpanWriter writer)
    {
        writer.Write(OpCode);
        writer.Write((ushort)0);
        writer.Write((byte)0);
        writer.Write((uint)BoardId);
        WriteFixedBoardName(ref writer, BoardName);
        writer.Write(DefaultListSerial);
        writer.Write(0u);
        writer.WritePacketLength();
    }

    protected override bool ParsePayload(ref SpanReader reader)
        => throw new NotSupportedException();

    private static void WriteFixedBoardName(ref SpanWriter writer, string boardName)
    {
        var bytes = Encoding.ASCII.GetBytes(boardName);
        var truncatedLength = Math.Min(bytes.Length, FixedBoardNameLength);

        writer.Write(bytes.AsSpan(0, truncatedLength));

        for (var i = truncatedLength; i < FixedBoardNameLength; i++)
        {
            writer.Write((byte)0);
        }
    }
}
