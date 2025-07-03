using Moongate.Core.Server.Packets;
using Moongate.Core.Spans;
using Moongate.UO.Data.Geometry;
using Moongate.UO.Data.Ids;

namespace Moongate.UO.Data.Packets.Items;

public class DropItemPacket : BaseUoPacket
{
    public Serial ItemId { get; set; }

    public Point3D Location { get; set; } = new Point3D(0, 0, 0);

    public int GridIndex { get; set; }

    public Serial ContainerId { get; set; }

    public bool IsGround => !ContainerId.IsValid || ContainerId == Serial.Zero;

    public DropItemPacket() : base(0x08)
    {
    }

    protected override bool Read(SpanReader reader)
    {
        /*
         *
         * BYTE[1] 0x08
           BYTE[4] Item Serial
           BYTE[2] X Location
           BYTE[2] Y Location
           BYTE[1] Z Location
           BYTE[1] Backpack grid index (see notes)
           BYTE[4] Container Serial Dropped Onto (FF FF FF FF drop to ground)
         */

        ItemId = (Serial)reader.ReadUInt32();
        Location = new Point3D(reader.ReadInt16(), reader.ReadInt16(), reader.ReadByte());
        GridIndex = reader.ReadByte();
        ContainerId = (Serial)reader.ReadUInt32();

        return true;
    }
}
