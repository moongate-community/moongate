using System.Text;
using Moongate.Network.Packets.Attributes;
using Moongate.Network.Packets.Base;
using Moongate.Network.Packets.Types.Packets;
using Moongate.Network.Spans;
using Moongate.UO.Data.Ids;

namespace Moongate.Network.Packets.Outgoing.Trading;

[PacketHandler(0x9E, PacketSizing.Variable, Description = "Vendor Sell List")]
public class VendorSellListPacket : BaseGameNetworkPacket
{
    public Serial VendorSerial { get; set; }

    public List<VendorSellListEntry> Entries { get; } = new();

    public VendorSellListPacket()
        : base(0x9E) { }

    public override void Write(ref SpanWriter writer)
    {
        writer.Write(OpCode);
        writer.Write((ushort)0);
        writer.Write((uint)VendorSerial);
        writer.Write((ushort)Entries.Count);

        foreach (var entry in Entries)
        {
            var name = entry.Name ?? string.Empty;
            var nameBytes = Encoding.ASCII.GetBytes(name);

            writer.Write((uint)entry.ItemSerial);
            writer.Write((ushort)entry.ItemId);
            writer.Write((ushort)entry.Hue);
            writer.Write((ushort)entry.Amount);
            writer.Write((ushort)entry.Price);
            writer.Write((ushort)nameBytes.Length);
            writer.Write(nameBytes);
        }

        writer.WritePacketLength();
    }

    protected override bool ParsePayload(ref SpanReader reader)
        => throw new NotSupportedException();
}

public sealed class VendorSellListEntry
{
    public Serial ItemSerial { get; set; }
    public int ItemId { get; set; }
    public int Hue { get; set; }
    public int Amount { get; set; }
    public int Price { get; set; }
    public string Name { get; set; } = string.Empty;
}
