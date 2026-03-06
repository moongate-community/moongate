using Moongate.Network.Packets.Outgoing.World;
using Moongate.UO.Data.Json.Weather;
using Moongate.UO.Data.Types;
using Moongate.UO.Data.Weather;

namespace Moongate.Tests.Network.Packets;

public class WeatherFactoryTests
{
    [Test]
    public void Create_ShouldClampEffectsToProtocolMaximum()
    {
        var packet = WeatherFactory.Create(WeatherType.Rain, 200, 10);

        Assert.Multiple(
            () =>
            {
                Assert.That(packet.Type, Is.EqualTo(WeatherType.Rain));
                Assert.That(packet.EffectCount, Is.EqualTo(SetWeatherPacket.MaximumEffectsOnScreen));
                Assert.That(packet.Temperature, Is.EqualTo(10));
            }
        );
    }

    [Test]
    public void CreateClear_ShouldDisableWeatherEffects()
    {
        var packet = WeatherFactory.CreateClear();

        Assert.Multiple(
            () =>
            {
                Assert.That(packet.Type, Is.EqualTo(WeatherType.None));
                Assert.That(packet.EffectCount, Is.EqualTo(0));
                Assert.That(packet.Temperature, Is.EqualTo(0));
            }
        );
    }

    [Test]
    public void CreateFromSnapshot_ShouldMapSnapshotToPacket()
    {
        var snapshot = new WeatherSnapshot(
            7,
            "Storm",
            20,
            18,
            JsonWeatherCondition.Storm,
            64,
            6,
            12,
            WeatherType.Storm,
            64
        );

        var packet = WeatherFactory.CreateFromSnapshot(snapshot);

        Assert.Multiple(
            () =>
            {
                Assert.That(packet.Type, Is.EqualTo(WeatherType.Storm));
                Assert.That(packet.EffectCount, Is.EqualTo((byte)64));
                Assert.That(packet.Temperature, Is.EqualTo((byte)12));
            }
        );
    }

    [Test]
    public void CreateSnow_ShouldUseExpectedDefaults()
    {
        var packet = WeatherFactory.CreateSnow();

        Assert.Multiple(
            () =>
            {
                Assert.That(packet.Type, Is.EqualTo(WeatherType.Snow));
                Assert.That(packet.EffectCount, Is.EqualTo(SetWeatherPacket.MaximumEffectsOnScreen));
                Assert.That(packet.Temperature, Is.EqualTo(unchecked((byte)-15)));
            }
        );
    }
}
