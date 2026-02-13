using Moongate.Core.Server.Packets;
using Moongate.Core.Spans;
using Moongate.UO.Data.Persistence.Entities;

namespace Moongate.UO.Data.Packets.Characters;

public class PaperdollPacket : BaseUoPacket
{
    public UOMobileEntity Mobile { get; set; }

    public PaperdollPacket() : base(0x88) { }

    public PaperdollPacket(UOMobileEntity mobile) : this()
        => Mobile = mobile;

    public override ReadOnlyMemory<byte> Write(SpanWriter writer)
    {
        writer.Write(OpCode);
        writer.Write(Mobile.Id.Value);
        writer.WriteAscii($"{Mobile.Name} {Mobile.Title}", 60);

        var baseFlags = Mobile.GetPacketFlags(true);

        if (Mobile.IsWarMode)
        {
            baseFlags |= 0x40;
        }

        baseFlags |= 0x02;

        writer.Write(baseFlags);

        return writer.ToArray();
    }
}
