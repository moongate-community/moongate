using Moongate.Network.Packets.Base;
using Moongate.Network.Spans;
using Moongate.UO.Data.Ids;

namespace Moongate.Network.Packets.Outgoing.Entity;

public sealed class DisplayCharacterProfilePacket : BaseGameNetworkPacket
{
    public Serial TargetId { get; set; }

    public string Header { get; set; } = string.Empty;

    public string Footer { get; set; } = string.Empty;

    public string Body { get; set; } = string.Empty;

    public DisplayCharacterProfilePacket()
        : base(0xB8, -1) { }

    public DisplayCharacterProfilePacket(Serial targetId, string? header, string? footer, string? body)
        : this()
    {
        TargetId = targetId;
        Header = header ?? string.Empty;
        Footer = footer ?? string.Empty;
        Body = body ?? string.Empty;
    }

    public override void Write(ref SpanWriter writer)
    {
        writer.Write(OpCode);
        writer.Write((ushort)0);
        writer.Write((uint)TargetId);
        writer.WriteAsciiNull(Header);
        writer.WriteBigUniNull(Footer);
        writer.WriteBigUniNull(Body);
        writer.WritePacketLength();
    }

    protected override bool ParsePayload(ref SpanReader reader)
        => throw new NotSupportedException();
}
