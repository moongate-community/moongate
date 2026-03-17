using Moongate.Network.Packets.Attributes;
using Moongate.Network.Packets.Base;
using Moongate.Network.Packets.Types.Packets;
using Moongate.Network.Spans;
using Moongate.UO.Data.Ids;

namespace Moongate.Network.Packets.Incoming.Trading;

[PacketHandler(0x3B, PacketSizing.Variable, Description = "Buy Item(s)")]

/// <summary>
/// Represents BuyItemsPacket.
/// </summary>
public class BuyItemsPacket : BaseGameNetworkPacket
{
    public byte Flag { get; private set; }
    public List<BuyItemsEntry> Items { get; } = new();
    public Serial VendorSerial { get; private set; }

    public BuyItemsPacket()
        : base(0x3B) { }

    protected override bool ParsePayload(ref SpanReader reader)
    {
        if (reader.Remaining < 7)
        {
            return false;
        }

        var declaredLength = reader.ReadUInt16();

        if (declaredLength != reader.Length || reader.Remaining < 5)
        {
            return false;
        }

        VendorSerial = (Serial)reader.ReadUInt32();
        Flag = reader.ReadByte();
        Items.Clear();

        while (reader.Remaining > 0)
        {
            if (reader.Remaining < 7)
            {
                return false;
            }

            Items.Add(
                new()
                {
                    Layer = reader.ReadByte(),
                    ItemSerial = (Serial)reader.ReadUInt32(),
                    Amount = reader.ReadInt16()
                }
            );
        }

        return true;
    }
}

public sealed class BuyItemsEntry
{
    public int Amount { get; set; }
    public Serial ItemSerial { get; set; }
    public byte Layer { get; set; }
}
