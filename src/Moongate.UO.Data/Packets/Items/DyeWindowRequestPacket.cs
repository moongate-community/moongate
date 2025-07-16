using Moongate.Core.Server.Packets;
using Moongate.Core.Spans;
using Moongate.UO.Data.Ids;
using Moongate.UO.Data.Interfaces.Entities;

namespace Moongate.UO.Data.Packets.Items;

public class DyeWindowRequestPacket : BaseUoPacket
{

    public Serial Serial { get; set; }

    public DyeWindowRequestPacket() : base(0x95)
    {
    }

    public DyeWindowRequestPacket(ISerialEntity serialEntity) : this()
    {
        Serial = serialEntity.Id;
    }

    public DyeWindowRequestPacket(Serial serial) : this()
    {
        Serial = serial;
    }

    public override ReadOnlyMemory<byte> Write(SpanWriter writer)
    {
        /**
         * BYTE[1] cmd
           BYTE[4] itemID of dye target
           BYTE[2] ignored on send, model on return
           BYTE[2] model on send, color on return from client (default on server send is 0x0FAB)
         */

        writer.Write(OpCode);
        writer.Write(Serial.Value);
        writer.Write((ushort)0);
        writer.Write((ushort)0x0FAB);
        return writer.ToArray();
    }
}
