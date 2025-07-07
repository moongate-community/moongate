using Moongate.Core.Server.Packets;
using Moongate.Core.Spans;
using Moongate.UO.Data.Ids;
using Moongate.UO.Data.Interfaces.Entities;

namespace Moongate.UO.Data.Packets.Objects;

public class DeleteObjectPacket : BaseUoPacket
{
    public Serial ObjectId { get; set; }


    public DeleteObjectPacket() : base(0x1D)
    {
    }

    public DeleteObjectPacket(Serial objectId) : this()
    {
        ObjectId = objectId;
    }

    public DeleteObjectPacket(ISerialEntity entity) : this(entity.Id)
    {

    }

    public override ReadOnlyMemory<byte> Write(SpanWriter writer)
    {
        writer.Write(OpCode);
        writer.Write(ObjectId.Value);
        return writer.ToArray();
    }
}
