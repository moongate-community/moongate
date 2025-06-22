using Moongate.Core.Server.Packets;
using Moongate.Core.Spans;
using Moongate.UO.Data.Types;

namespace Moongate.UO.Data.Packets.World;

public class SetWeatherPacket : BaseUoPacket
{
    public WeatherType WeatherType { get; set; }
    public int NumOfEffects { get; set; }
    public int Temperature { get; set; }

    public SetWeatherPacket() : base(0x65)
    {
    }

    public SetWeatherPacket(WeatherType weatherType, int numOfEffects, int temperature) : this()
    {
        WeatherType = weatherType;
        NumOfEffects = numOfEffects;
        Temperature = temperature;
    }

    public override ReadOnlyMemory<byte> Write(SpanWriter writer)
    {
        writer.Write(OpCode);
        writer.Write((byte)WeatherType);
        writer.Write((byte)NumOfEffects);
        writer.Write((byte)Temperature);
        return writer.ToArray();
    }
}
