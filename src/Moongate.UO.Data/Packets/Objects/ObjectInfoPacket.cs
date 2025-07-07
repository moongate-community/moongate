using Moongate.Core.Server.Packets;
using Moongate.Core.Spans;
using Moongate.UO.Data.Geometry;
using Moongate.UO.Data.Ids;
using Moongate.UO.Data.Types;

namespace Moongate.UO.Data.Packets.Objects;

/// <summary>
/// Object Info Packet (0x1A)
/// Sent by server to display an object in the world
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
    /// Item count (if Object ID has 0x80000000 flag)
    /// </summary>
    public ushort? ItemCount { get; set; }

    /// <summary>
    /// Increment counter for graphic (if Graphic has 0x8000 flag)
    /// </summary>
    public byte? IncrementCounter { get; set; }


    public Point3D? Location { get; set; }

    /// <summary>
    /// Facing direction (if xLoc has 0x8000 flag)
    /// </summary>
    public byte? Facing { get; set; }
    /// <summary>
    /// Dye/Hue (if yLoc has 0x8000 flag)
    /// </summary>
    public ushort? Dye { get; set; }

    /// <summary>
    /// Flags (if yLoc has 0x4000 flag)
    /// </summary>
    public ObjectInfoFlags? Flags { get; set; }

    public ObjectInfoPacket() : base(0x1A)
    {
    }

    public ObjectInfoPacket(Serial objectId, ushort graphic, Point3D location) : this()
    {
        ObjectId = objectId;
        Graphic = graphic;
    }

    public override ReadOnlyMemory<byte> Write(SpanWriter writer)
    {
        /*
         * BYTE[1] 0x1A
         * BYTE[2] Length
         * BYTE[4] Object ID
         * BYTE[2] Graphic
         *
         * If (Object ID & 0x80000000)
         *     BYTE[2] item count (or Graphic for corpses)
         *
         * If (Graphic & 0x8000)
         *     BYTE Increment Counter (increment graphic by this #)
         *
         * BYTE[2] xLoc (only use lowest significant 15 bits)
         * BYTE[2] yLoc
         *
         * If (xLoc & 0x8000)
         *     BYTE facing
         *
         * BYTE zLoc
         *
         * If (yLoc & 0x8000)
         *     BYTE[2] dye
         *
         * If (yLoc & 0x4000)
         *     BYTE[1] Flag
         */

        /// Calculate packet size
        var packetSize = CalculatePacketSize();

        /// Write packet header
        writer.Write(OpCode);
        writer.Write((short)packetSize);

        /// Write object ID (with flags if needed)
        uint objectIdToWrite = ObjectId.Value;
        if (ItemCount.HasValue)
        {
            objectIdToWrite |= 0x80000000; /// Set flag for item count
        }
        writer.Write(objectIdToWrite);

        /// Write graphic (with flags if needed)
        ushort graphicToWrite = Graphic;
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

        /// Write X location (with flags if needed)
        ushort xLocToWrite = (ushort)(Location.Value.X & 0x7FFF); /// Only use lowest 15 bits
        if (Facing.HasValue)
        {
            xLocToWrite |= 0x8000; /// Set flag for facing
        }
        writer.Write(xLocToWrite);

        /// Write Y location (with flags if needed)
        ushort yLocToWrite = (ushort)Location.Value.Y;
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

        /// Write Z location
        writer.Write((byte)Location.Value.Z);

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

    private int CalculatePacketSize()
    {
        var size = 1 + 2 + 4 + 2; /// Header + Length + Object ID + Graphic

        /// Add conditional fields
        if (ItemCount.HasValue)
            size += 2;

        if (IncrementCounter.HasValue)
            size += 1;

        size += 2 + 2 + 1; /// XLoc + YLoc + ZLoc (always present)

        if (Facing.HasValue)
            size += 1;

        if (Dye.HasValue)
            size += 2;

        if (Flags.HasValue)
            size += 1;

        return size;
    }
}
