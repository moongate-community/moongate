using Moongate.UO.Data.Ids;
using Moongate.UO.Data.Packets.Objects;
using Moongate.UO.Data.Persistence.Entities;
using Moongate.UO.Data.Types;

namespace Moongate.UO.Data.Extensions;

/// <summary>
/// Helper methods to create ObjectInfoPacket easily
/// </summary>
public static class ObjectInfoPacketExtensions
{
    /// <summary>
    /// Creates an ObjectInfoPacket for an animated item
    /// </summary>
    public static ObjectInfoPacket CreateForAnimatedItem(UOItemEntity item, byte animationFrame)
    {
        var packet = CreateForItem(item);

        /// Add increment counter for animation
        packet.IncrementCounter = animationFrame;

        return packet;
    }

    /// <summary>
    /// Creates an ObjectInfoPacket for a corpse
    /// </summary>
    public static ObjectInfoPacket CreateForCorpse(
        Serial corpseSerial,
        ushort corpseGraphic,
        ushort x,
        ushort y,
        sbyte z,
        ushort originalBodyGraphic
    )
    {
        var packet = new ObjectInfoPacket(corpseSerial, corpseGraphic, new(x, y, z));

        /// For corpses, ItemCount contains the original body graphic
        packet.ItemCount = originalBodyGraphic;

        return packet;
    }

    /// <summary>
    /// Creates an ObjectInfoPacket for a simple item
    /// </summary>
    public static ObjectInfoPacket CreateForItem(UOItemEntity item)
    {
        var packet = new ObjectInfoPacket(item.Id, (ushort)item.ItemId, item.Location);

        /// Add item count if it's a stackable item
        if (item.Amount > 1)
        {
            packet.ItemCount = (ushort)item.Amount;
        }

        /// Add hue if present
        if (item.Hue != 0)
        {
            packet.Dye = (ushort)item.Hue;
        }

        /// TODO: Add other item-specific properties

        return packet;
    }

    /// <summary>
    /// Creates an ObjectInfoPacket for a mobile/character
    /// </summary>
    public static ObjectInfoPacket CreateForMobile(UOMobileEntity mobile)
    {
        var packet = new ObjectInfoPacket(mobile.Id, (ushort)mobile.Race.Body(mobile), mobile.Location);

        /// Add facing direction for mobile
        packet.Facing = (byte)mobile.Direction;

        /// Add hue if present
        if (mobile.SkinHue != 0)
        {
            packet.Dye = (ushort)mobile.SkinHue;
        }

        /// Add flags for mobile
        var flags = ObjectInfoFlags.None;

        if (mobile.Gender == GenderType.Female)
        {
            flags |= ObjectInfoFlags.Female;
        }

        if (mobile.IsPoisoned)
        {
            flags |= ObjectInfoFlags.Poisoned;
        }

        if (mobile.IsWarMode)
        {
            flags |= ObjectInfoFlags.WarMode;
        }

        if (mobile.IsHidden)
        {
            flags |= ObjectInfoFlags.Hidden;
        }

        /// Add yellow hits if HP is low
        // if (mobile.CurrentHitPoints < mobile.MaxHitPoints * 0.3)
        //   flags |= ObjectInfoFlags.YellowHits;

        if (flags != ObjectInfoFlags.None)
        {
            packet.Flags = flags;
        }

        return packet;
    }
}

/// <summary>
/// Factory for creating common ObjectInfoPacket instances
/// </summary>
public static class ObjectInfoPacketFactory
{
    /// <summary>
    /// Creates packet for item dropped on ground
    /// </summary>
    public static ObjectInfoPacket CreateGroundItem(
        Serial serial,
        ushort graphic,
        ushort x,
        ushort y,
        sbyte z,
        ushort amount = 1,
        ushort hue = 0
    )
    {
        var packet = new ObjectInfoPacket(serial, graphic, new(x, y, z));

        if (amount > 1)
        {
            packet.ItemCount = amount;
        }

        if (hue != 0)
        {
            packet.Dye = hue;
        }

        return packet;
    }

    /// <summary>
    /// Creates packet for NPC
    /// </summary>
    public static ObjectInfoPacket CreateNPC(
        Serial serial,
        ushort bodyType,
        ushort x,
        ushort y,
        sbyte z,
        byte direction,
        ushort hue = 0,
        bool isPoisoned = false,
        bool isYellowHits = false
    )
    {
        var packet = new ObjectInfoPacket(serial, bodyType, new(x, y, z));

        packet.Facing = direction;

        if (hue != 0)
        {
            packet.Dye = hue;
        }

        var flags = ObjectInfoFlags.None;

        if (isPoisoned)
        {
            flags |= ObjectInfoFlags.Poisoned;
        }

        if (isYellowHits)
        {
            flags |= ObjectInfoFlags.YellowHits;
        }

        if (flags != ObjectInfoFlags.None)
        {
            packet.Flags = flags;
        }

        return packet;
    }

    /// <summary>
    /// Creates packet for player character
    /// </summary>
    public static ObjectInfoPacket CreatePlayer(
        Serial serial,
        ushort bodyType,
        ushort x,
        ushort y,
        sbyte z,
        byte direction,
        ushort hue = 0,
        bool isFemale = false,
        bool isWarMode = false,
        bool isHidden = false
    )
    {
        var packet = new ObjectInfoPacket(serial, bodyType, new(x, y, z));

        packet.Facing = direction;

        if (hue != 0)
        {
            packet.Dye = hue;
        }

        var flags = ObjectInfoFlags.None;

        if (isFemale)
        {
            flags |= ObjectInfoFlags.Female;
        }

        if (isWarMode)
        {
            flags |= ObjectInfoFlags.WarMode;
        }

        if (isHidden)
        {
            flags |= ObjectInfoFlags.Hidden;
        }

        if (flags != ObjectInfoFlags.None)
        {
            packet.Flags = flags;
        }

        return packet;
    }
}
