using Moongate.Core.Server.Packets;
using Moongate.Core.Spans;
using Moongate.UO.Data.Geometry;
using Moongate.UO.Data.Ids;
using Moongate.UO.Data.Persistence.Entities;
using Moongate.UO.Data.Types;

namespace Moongate.UO.Data.Packets.Objects;

/// <summary>
/// Object Info Packet (0x1A)
/// Sent by server to display an object in the world
/// Variable length packet containing object information
/// </summary>
public class ObjectInfoPacket : BaseUoPacket
{
    /// <summary>
    /// Serial of the object
    /// </summary>
    public Serial ObjectId { get; set; }

    /// <summary>
    /// Graphic ID of the object
    /// </summary>
    public ushort Graphic { get; set; }

    /// <summary>
    /// Item count (if Object ID has 0x80000000 flag) or Graphic for corpses
    /// </summary>
    public ushort? ItemCount { get; set; }

    /// <summary>
    /// Increment counter for graphic (if Graphic has 0x8000 flag)
    /// </summary>
    public byte? IncrementCounter { get; set; }

    /// <summary>
    /// Location of the object in world
    /// </summary>
    public Point3D Location { get; set; }

    /// <summary>
    /// Facing direction (if xLoc has 0x8000 flag)
    /// </summary>
    public byte? Facing { get; set; }

    /// <summary>
    /// Dye/Hue color (if yLoc has 0x8000 flag)
    /// </summary>
    public ushort? Dye { get; set; }

    /// <summary>
    /// Object flags (if yLoc has 0x4000 flag)
    /// </summary>
    public ObjectInfoFlags? Flags { get; set; }

    /// <summary>
    /// Default constructor
    /// </summary>
    public ObjectInfoPacket() : base(0x1A)
        => Location = new();

    /// <summary>
    /// Constructor with basic object information
    /// </summary>
    /// <param name="objectId">Serial of the object</param>
    /// <param name="graphic">Graphic ID</param>
    /// <param name="location">World location</param>
    public ObjectInfoPacket(Serial objectId, ushort graphic, Point3D location) : this()
    {
        ObjectId = objectId;
        Graphic = graphic;
        Location = location;
    }

    /// <summary>
    /// Constructor from UOItemEntity
    /// </summary>
    /// <param name="item">UO Item entity to create packet from</param>
    public ObjectInfoPacket(UOItemEntity item) : this()
    {
        ObjectId = item.Id;
        Graphic = (ushort)item.ItemId;
        Location = item.Location;

        /// Set item count if stackable and amount > 1
        if (item.IsStackable && item.Amount > 1)
        {
            ItemCount = (ushort)item.Amount;
        }

        /// Set hue/dye if present
        if (item.Hue != 0)
        {
            Dye = (ushort)item.Hue;
        }

        /// Set flags based on item properties
        var flags = ObjectInfoFlags.None;

        /// Items are movable by default unless they have special properties
        /// You can extend this based on your item properties
        switch (item.LootType)
        {
            case LootType.Blessed:
            case LootType.Newbied:
                // These items might have special movement rules
                break;
            case LootType.Regular:
                flags |= ObjectInfoFlags.Movable;

                break;
        }

        if (flags != ObjectInfoFlags.None)
        {
            Flags = flags;
        }
    }

    /// <summary>
    /// Constructor with all parameters
    /// </summary>
    /// <param name="objectId">Serial of the object</param>
    /// <param name="graphic">Graphic ID</param>
    /// <param name="location">World location</param>
    /// <param name="itemCount">Item count (optional)</param>
    /// <param name="incrementCounter">Increment counter (optional)</param>
    /// <param name="facing">Facing direction (optional)</param>
    /// <param name="dye">Dye/Hue color (optional)</param>
    /// <param name="flags">Object flags (optional)</param>
    public ObjectInfoPacket(
        Serial objectId,
        ushort graphic,
        Point3D location,
        ushort? itemCount = null,
        byte? incrementCounter = null,
        byte? facing = null,
        ushort? dye = null,
        ObjectInfoFlags? flags = null
    ) : this()
    {
        ObjectId = objectId;
        Graphic = graphic;
        Location = location;
        ItemCount = itemCount;
        IncrementCounter = incrementCounter;
        Facing = facing;
        Dye = dye;
        Flags = flags;
    }

    /// <summary>
    /// Get a string representation of the packet
    /// </summary>
    /// <returns>String description</returns>
    public override string ToString()
        => $"ObjectInfoPacket [0x{OpCode:X2}]: ObjectId={ObjectId}, Graphic=0x{Graphic:X4}, " +
           $"Location={Location}, ItemCount={ItemCount}, Facing={Facing}, Dye=0x{Dye:X4}, Flags={Flags}";

    /// <summary>
    /// Write packet data to buffer
    /// </summary>
    /// <param name="writer">Span writer for output</param>
    /// <returns>Written packet data</returns>
    public override ReadOnlyMemory<byte> Write(SpanWriter writer)
    {
        /// Calculate total packet size
        var packetSize = CalculatePacketSize();

        /// Write packet header
        writer.Write(OpCode); // 0x1A
        writer.Write((ushort)packetSize);

        /// Write object ID with flags if needed
        var objectIdToWrite = ObjectId.Value;

        if (ItemCount.HasValue)
        {
            objectIdToWrite |= 0x80000000; /// Set flag for item count
        }

        writer.Write(objectIdToWrite);

        /// Write graphic with flags if needed
        var graphicToWrite = Graphic;

        if (IncrementCounter.HasValue)
        {
            graphicToWrite |= 0x8000; /// Set flag for increment counter
        }

        writer.Write(graphicToWrite);

        /// Write item count if flagged
        if (ItemCount.HasValue)
        {
            writer.Write(ItemCount.Value);
        }

        /// Write increment counter if flagged
        if (IncrementCounter.HasValue)
        {
            writer.Write(IncrementCounter.Value);
        }

        /// Write X location with flags if needed (only use lowest 15 bits)
        var xLocToWrite = (ushort)(Location.X & 0x7FFF);

        if (Facing.HasValue)
        {
            xLocToWrite |= 0x8000; /// Set flag for facing
        }

        writer.Write(xLocToWrite);

        /// Write Y location with flags if needed
        var yLocToWrite = (ushort)Location.Y;

        if (Dye.HasValue)
        {
            yLocToWrite |= 0x8000; /// Set flag for dye
        }

        if (Flags.HasValue)
        {
            yLocToWrite |= 0x4000; /// Set flag for flags
        }

        writer.Write(yLocToWrite);

        /// Write facing if flagged
        if (Facing.HasValue)
        {
            writer.Write(Facing.Value);
        }

        /// Write Z location (always present)
        writer.Write((byte)Location.Z);

        /// Write dye if flagged
        if (Dye.HasValue)
        {
            writer.Write(Dye.Value);
        }

        /// Write flags if flagged
        if (Flags.HasValue)
        {
            writer.Write((byte)Flags.Value);
        }

        return writer.ToArray();
    }

    /// <summary>
    /// Calculate the total packet size based on optional fields
    /// </summary>
    /// <returns>Total packet size in bytes</returns>
    private int CalculatePacketSize()
    {
        var size = 1 + 2 + 4 + 2; /// OpCode + Length + Object ID + Graphic

        /// Add conditional fields
        if (ItemCount.HasValue)
        {
            size += 2;
        }

        if (IncrementCounter.HasValue)
        {
            size += 1;
        }

        size += 2 + 2 + 1; /// XLoc + YLoc + ZLoc (always present)

        if (Facing.HasValue)
        {
            size += 1;
        }

        if (Dye.HasValue)
        {
            size += 2;
        }

        if (Flags.HasValue)
        {
            size += 1;
        }

        return size;
    }
}
