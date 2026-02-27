using Moongate.Network.Packets.Interfaces;
using Moongate.Network.Packets.Outgoing.World;
using Moongate.Network.Spans;
using Moongate.UO.Data.Types;

namespace Moongate.Tests.Network.Packets;

public class SetWeatherPacketTests
{
    [Test]
    public void TryParse_ShouldReadFields()
    {
        var packet = new SetWeatherPacket();
        var payload = new byte[]
        {
            0x65,
            0x02,
            0x46,
            0x20
        };

        var parsed = packet.TryParse(payload);

        Assert.Multiple(
            () =>
            {
                Assert.That(parsed, Is.True);
                Assert.That(packet.Type, Is.EqualTo(WeatherType.Snow));
                Assert.That(packet.EffectCount, Is.EqualTo(70));
                Assert.That(packet.Temperature, Is.EqualTo(0x20));
            }
        );
    }

    [Test]
    public void Write_ShouldSerializeExpectedPayload()
    {
        var packet = new SetWeatherPacket(WeatherType.StormBrewing, 35, 0x18);

        var data = Write(packet);

        Assert.Multiple(
            () =>
            {
                Assert.That(data.Length, Is.EqualTo(4));
                Assert.That(data[0], Is.EqualTo(0x65));
                Assert.That(data[1], Is.EqualTo(0x03));
                Assert.That(data[2], Is.EqualTo(35));
                Assert.That(data[3], Is.EqualTo(0x18));
            }
        );
    }

    private static byte[] Write(IGameNetworkPacket packet)
    {
        var writer = new SpanWriter(8, true);
        packet.Write(ref writer);
        var data = writer.ToArray();
        writer.Dispose();

        return data;
    }
}
