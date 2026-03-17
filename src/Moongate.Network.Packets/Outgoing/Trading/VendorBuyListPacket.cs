using System.Text;
using Moongate.Network.Packets.Attributes;
using Moongate.Network.Packets.Base;
using Moongate.Network.Packets.Types.Packets;
using Moongate.Network.Spans;
using Moongate.UO.Data.Ids;

namespace Moongate.Network.Packets.Outgoing.Trading;

[PacketHandler(0x74, PacketSizing.Variable, Description = "Vendor Buy List")]
public class VendorBuyListPacket : BaseGameNetworkPacket
{
    public Serial ShopContainerSerial { get; set; }

    public List<VendorBuyListEntry> Entries { get; } = new();

    public VendorBuyListPacket()
        : base(0x74) { }

    public override void Write(ref SpanWriter writer)
    {
        writer.Write(OpCode);
        writer.Write((ushort)0);
        writer.Write((uint)ShopContainerSerial);
        writer.Write((byte)Entries.Count);

        foreach (var entry in Entries)
        {
            var description = entry.Description ?? string.Empty;
            var descriptionBytes = Encoding.ASCII.GetBytes(description);

            writer.Write(entry.Price);
            writer.Write((byte)(descriptionBytes.Length + 1));
            writer.Write(descriptionBytes);
            writer.Write((byte)0);
        }

        writer.WritePacketLength();
    }

    protected override bool ParsePayload(ref SpanReader reader)
        => throw new NotSupportedException();
}

public sealed class VendorBuyListEntry
{
    public int Price { get; set; }
    public string Description { get; set; } = string.Empty;
}
