using Moongate.Core.Server.Packets;
using Moongate.Core.Spans;
using Moongate.UO.Data.Persistence.Entities;

namespace Moongate.UO.Data.Packets.Characters;

public class WarModePacket : BaseUoPacket
{
    public UOMobileEntity Mobile { get; set; }

    public WarModePacket(UOMobileEntity mobile) : this()
        => Mobile = mobile;

    public WarModePacket() : base(0x72) { }

    public override ReadOnlyMemory<byte> Write(SpanWriter writer)
    {
        writer.Write(OpCode);
        writer.Write(Mobile.IsWarMode);
        writer.Write((byte)0);
        writer.Write((byte)0x32);
        writer.Write((byte)0);

        return writer.ToArray();
    }
}
