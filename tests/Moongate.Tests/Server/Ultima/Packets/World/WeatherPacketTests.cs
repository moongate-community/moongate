using Moongate.Network.Packets.Serialization;
using Moongate.Server.Ultima.Packets.World;
using Moongate.Server.Ultima.Types.Weather;

namespace Moongate.Tests.Server.Ultima.Packets.World;

public sealed class WeatherPacketTests
{
    [Fact]
    public void Encode_WritesKindDensityAndTemperature()
    {
        Assert.Equal(Convert.FromHexString("65024614"), PacketCodec.Encode(new WeatherPacket(WeatherKindType.Snow, 70, 20)));
    }

    [Fact]
    public void Encode_ANegativeTemperature_IsASignedByte_AndNoneIsFF()
    {
        Assert.Equal(Convert.FromHexString("65FF00F1"), PacketCodec.Encode(new WeatherPacket(WeatherKindType.None, 0, -15)));
    }
}
