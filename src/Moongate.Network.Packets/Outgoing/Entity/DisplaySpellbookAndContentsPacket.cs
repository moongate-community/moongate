using Moongate.Network.Packets.Base;
using Moongate.Network.Packets.Outgoing.World;
using Moongate.Network.Spans;
using Moongate.UO.Data.Ids;
using Moongate.UO.Data.Persistence.Entities;

namespace Moongate.Network.Packets.Outgoing.Entity;

/// <summary>
/// Writes the spellbook open packet followed by spellbook content payload.
/// </summary>
public sealed class DisplaySpellbookAndContentsPacket : BaseGameNetworkPacket
{
    private const ushort SpellbookGumpId = 0xFFFF;
    private const short SpellbookContainerX = 0x7D;

    public Serial SpellbookSerial { get; set; }

    public int Graphic { get; set; }

    public int Offset { get; set; } = 1;

    public ulong Content { get; set; }

    public DisplaySpellbookAndContentsPacket()
        : base(0x24) { }

    public DisplaySpellbookAndContentsPacket(UOItemEntity spellbook, ulong content, int offset = 1)
        : this()
    {
        ArgumentNullException.ThrowIfNull(spellbook);

        SpellbookSerial = spellbook.Id;
        Graphic = spellbook.ItemId;
        Offset = offset;
        Content = content;
    }

    public override void Write(ref SpanWriter writer)
    {
        writer.Write(OpCode);
        writer.Write(SpellbookSerial.Value);
        writer.Write(SpellbookGumpId);
        writer.Write(SpellbookContainerX);

        var contentPacket = GeneralInformationFactory.CreateNewSpellbookContent(
            SpellbookSerial,
            Graphic,
            Offset,
            Content
        );
        contentPacket.Write(ref writer);
    }

    protected override bool ParsePayload(ref SpanReader reader)
        => true;
}
