using Moongate.UO.Data.Types;
using Moongate.UO.Data.Weather;

namespace Moongate.Network.Packets.Outgoing.World;

/// <summary>
/// Creates commonly used weather packets with sane defaults.
/// </summary>
public static class WeatherFactory
{
    public static SetWeatherPacket Create(WeatherType type, int effectCount, int temperature)
    {
        var clampedEffects = Math.Clamp(effectCount, 0, SetWeatherPacket.MaximumEffectsOnScreen);

        return new(type, (byte)clampedEffects, unchecked((byte)temperature));
    }

    public static SetWeatherPacket CreateClear(int temperature = 0)
        => Create(WeatherType.None, 0, temperature);

    public static SetWeatherPacket CreateFromSnapshot(WeatherSnapshot snapshot)
        => Create(snapshot.Type, snapshot.EffectCount, snapshot.EffectiveTemperature);

    public static SetWeatherPacket CreateRain(
        int effectCount = SetWeatherPacket.MaximumEffectsOnScreen,
        int temperature = 15
    )
        => Create(WeatherType.Rain, effectCount, temperature);

    public static SetWeatherPacket CreateSnow(
        int effectCount = SetWeatherPacket.MaximumEffectsOnScreen,
        int temperature = -15
    )
        => Create(WeatherType.Snow, effectCount, temperature);

    public static SetWeatherPacket CreateStorm(
        int effectCount = SetWeatherPacket.MaximumEffectsOnScreen,
        int temperature = 10
    )
        => Create(WeatherType.Storm, effectCount, temperature);
}
