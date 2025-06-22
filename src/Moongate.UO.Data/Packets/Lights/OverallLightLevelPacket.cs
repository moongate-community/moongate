using Moongate.Core.Server.Packets;
using Moongate.Core.Spans;
using Moongate.UO.Data.Types;

namespace Moongate.UO.Data.Packets.Lights;

public class OverallLightLevelPacket : BaseUoPacket
{
    public LightLevelType LightLevel { get; set; }

    public OverallLightLevelPacket(LightLevelType lightLevel) : this()
    {
        LightLevel = lightLevel;
    }

    public OverallLightLevelPacket() : base(0x4F)
    {
    }

    public override ReadOnlyMemory<byte> Write(SpanWriter writer)
    {
        writer.Write(OpCode);
        writer.Write((byte)LightLevel);
        return writer.ToArray();
    }
}
