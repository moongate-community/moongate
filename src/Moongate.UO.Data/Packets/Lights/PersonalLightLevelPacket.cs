using Moongate.Core.Server.Packets;
using Moongate.Core.Spans;
using Moongate.UO.Data.Persistence.Entities;
using Moongate.UO.Data.Types;

namespace Moongate.UO.Data.Packets.Lights;

public class PersonalLightLevelPacket : BaseUoPacket
{
    public LightLevelType LightLevel { get; set; }

    public UOMobileEntity Mobile { get; set; }

    public PersonalLightLevelPacket(LightLevelType lightLevel, UOMobileEntity mobile) : this()
    {
        LightLevel = lightLevel;
        Mobile = mobile;
    }

    public PersonalLightLevelPacket() : base(0x4E) { }

    public override ReadOnlyMemory<byte> Write(SpanWriter writer)
    {
        writer.Write(OpCode);
        writer.Write(Mobile.Id.Value);
        writer.Write((byte)LightLevel);

        return writer.ToArray();
    }
}
