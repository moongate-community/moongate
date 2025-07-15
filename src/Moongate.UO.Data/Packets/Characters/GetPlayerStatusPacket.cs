using Moongate.Core.Server.Packets;
using Moongate.Core.Spans;
using Moongate.UO.Data.Ids;
using Moongate.UO.Data.Types;

namespace Moongate.UO.Data.Packets.Characters;

public class GetPlayerStatusPacket : BaseUoPacket
{

    public GetPlayerStatusType StatusType { get; set; }
    public Serial MobileId { get; set; }


    public GetPlayerStatusPacket() : base(0x34)
    {
    }


    protected override bool Read(SpanReader reader)
    {

        reader.ReadInt32();
        StatusType = (GetPlayerStatusType)reader.ReadByte();
        MobileId = (Serial)reader.ReadUInt32();

        return true;
    }
}
