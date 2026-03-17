using Moongate.Network.Packets.Attributes;
using Moongate.Network.Packets.Base;
using Moongate.Network.Packets.Types.Packets;
using Moongate.Network.Spans;
using Moongate.UO.Data.Ids;

namespace Moongate.Network.Packets.Incoming.Trading;

[PacketHandler(0x9F, PacketSizing.Variable, Description = "Sell List Reply")]

/// <summary>
/// Represents SellListReplyPacket.
/// </summary>
public class SellListReplyPacket : BaseGameNetworkPacket
{
    public List<SellListReplyEntry> Items { get; } = new();
    public Serial VendorSerial { get; private set; }

    public SellListReplyPacket()
        : base(0x9F) { }

    protected override bool ParsePayload(ref SpanReader reader)
    {
        if (reader.Remaining < 8)
        {
            return false;
        }

        var declaredLength = reader.ReadUInt16();

        if (declaredLength != reader.Length || reader.Remaining < 6)
        {
            return false;
        }

        VendorSerial = (Serial)reader.ReadUInt32();
        var count = reader.ReadUInt16();
        Items.Clear();

        if (reader.Remaining != count * 6)
        {
            return false;
        }

        for (var i = 0; i < count; i++)
        {
            Items.Add(
                new()
                {
                    ItemSerial = (Serial)reader.ReadUInt32(),
                    Amount = reader.ReadInt16()
                }
            );
        }

        return reader.Remaining == 0;
    }
}

public sealed class SellListReplyEntry
{
    public int Amount { get; set; }
    public Serial ItemSerial { get; set; }
}
