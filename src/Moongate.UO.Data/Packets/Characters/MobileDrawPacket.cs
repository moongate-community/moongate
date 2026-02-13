using Moongate.Core.Server.Packets;
using Moongate.Core.Spans;
using Moongate.UO.Data.Persistence.Entities;
using Moongate.UO.Data.Types;

namespace Moongate.UO.Data.Packets.Characters;

public class MobileDrawPacket : BaseUoPacket
{
    public UOMobileEntity Beholder { get; set; }
    public UOMobileEntity Beheld { get; set; }
    public bool StygianAbyss { get; set; }
    public bool NewMobileIncoming { get; set; }

    public MobileDrawPacket() : base(0x78) { }

    public MobileDrawPacket(
        UOMobileEntity beholder,
        UOMobileEntity beheld,
        bool stygianAbyss = false,
        bool newMobileIncoming = false
    ) : this()
    {
        Beholder = beholder;
        Beheld = beheld;
        NewMobileIncoming = newMobileIncoming;
    }

    public override ReadOnlyMemory<byte> Write(SpanWriter writer)
    {
        if (Beheld == null)
        {
            return ReadOnlyMemory<byte>.Empty;
        }

        // Span to track already processed layers
        Span<bool> layers = stackalloc bool[256];
        layers.Clear();

        var items = Beheld.Equipment;

        // Item ID mask based on packet type
        var itemIdMask = NewMobileIncoming ? 0xFFFF : 0x7FFF;

        // Mobile hue (override if present)
        //var hue = Beheld.SolidHueOverride >= 0 ? Beheld.SolidHueOverride : Beheld.Hue;

        // Packet header
        writer.Write(OpCode);
        writer.Seek(2, SeekOrigin.Current); // Space for length

        // Mobile data
        writer.Write(Beheld.Id.Value);
        writer.Write((short)Beheld.Body);
        writer.Write((short)Beheld.X);
        writer.Write((short)Beheld.Y);
        writer.Write((sbyte)Beheld.Z);
        writer.Write((byte)Beheld.Direction);
        writer.Write((short)Beheld.SkinHue);
        writer.Write(Beheld.GetPacketFlags(StygianAbyss));

        // Calculate notoriety between beholder and beheld
        var notoriety = (byte)Beheld.Notoriety;

        if (Beholder != null)
        {
            // Here you should implement the logic of Notoriety.Compute(beholder, beheld)
            // For now using the mobile's default value
        }

        writer.Write(notoriety);

        // Process equipped items
        foreach (var (layer, item) in items)
        {
            var layerByte = (byte)layer;

            if (Beheld != Beholder && !IsVisibleLayer(layer))
            {
                continue;
            }

            // Check if item is valid and visible
            if (item.Id.Value == 0 || layers[layerByte])
            {
                continue;
            }

            // Check if beholder can see the item
            if (Beholder != null && !CanSeeItem(item))
            {
                continue;
            }

            layers[layerByte] = true;

            //var itemHue = Beheld.SolidHueOverride >= 0 ? Beheld.SolidHueOverride : item.Hue;

            var itemID = item.ItemId & itemIdMask;
            var writeHue = NewMobileIncoming || item.Hue != 0;

            if (!NewMobileIncoming && writeHue)
            {
                itemID |= 0x8000;
            }

            writer.Write(item.Id.Value);
            writer.Write((ushort)itemID);
            writer.Write(layerByte);

            if (writeHue)
            {
                writer.Write((ushort)item.Hue);
            }
        }

        // Hair handling (if not already processed)
        if (HasHair() && !layers[(int)ItemLayerType.Hair])
        {
            layers[(int)ItemLayerType.Hair] = true;
            var hairHue = GetHairHue();

            var hairItemID = GetHairItemID() & itemIdMask;
            var writeHue = NewMobileIncoming || hairHue != 0;

            if (!NewMobileIncoming && writeHue)
            {
                hairItemID |= 0x8000;
            }

            writer.Write(GetHairSerial()); // Virtual serial for hair
            writer.Write((ushort)hairItemID);
            writer.Write((byte)ItemLayerType.Hair);

            if (writeHue)
            {
                writer.Write((ushort)hairHue);
            }
        }

        // Facial hair handling (if not already processed)
        if (HasFacialHair() && !layers[(int)ItemLayerType.FacialHair])
        {
            layers[(int)ItemLayerType.FacialHair] = true;
            var facialHairHue = GetFacialHairHue();

            var facialHairItemID = GetFacialHairItemID() & itemIdMask;
            var writeHue = NewMobileIncoming || facialHairHue != 0;

            if (!NewMobileIncoming && writeHue)
            {
                facialHairItemID |= 0x8000;
            }

            writer.Write(GetFacialHairSerial()); // Virtual serial for facial hair
            writer.Write((ushort)facialHairItemID);
            writer.Write((byte)ItemLayerType.FacialHair);

            if (writeHue)
            {
                writer.Write((ushort)facialHairHue);
            }
        }

        // Terminator
        writer.Write(0);

        writer.WritePacketLength();

        return writer.ToArray();
    }

    /// <summary>
    /// Check if beholder can see the item
    /// </summary>
    private bool CanSeeItem(ItemReference item)

        // Implement visibility logic
        // For now return true as default
        => true;

    /// <summary>
    /// Get facial hair hue
    /// </summary>
    private int GetFacialHairHue()
        => Beheld.FacialHairHue;

    /// <summary>
    /// Get facial hair item ID
    /// </summary>
    private int GetFacialHairItemID()
        => Beheld.FacialHairStyle;

    /// <summary>
    /// Get virtual facial hair serial
    /// </summary>
    private uint GetFacialHairSerial()
        => Beheld.Id.Value + 0x50000000;

    /// <summary>
    /// Get hair hue
    /// </summary>
    private int GetHairHue()
        => Beheld.HairHue;

    /// <summary>
    /// Get hair item ID
    /// </summary>
    private int GetHairItemID()
        => Beheld.HairStyle;

    /// <summary>
    /// Get virtual hair serial
    /// </summary>
    private uint GetHairSerial()
        => Beheld.Id.Value + 0x40000000;

    /// <summary>
    /// Check if mobile has facial hair
    /// </summary>
    private bool HasFacialHair()
        => Beheld.FacialHairStyle > 0;

    private bool HasHair()
        => Beheld.HairStyle > 0;

    private bool IsVisibleLayer(ItemLayerType layer)
    {
        return layer != ItemLayerType.Backpack &&
               layer != ItemLayerType.Bank &&
               layer != ItemLayerType.ShopBuy &&
               layer != ItemLayerType.ShopSell &&
               layer != ItemLayerType.ShopResale;
    }
}
