using Moongate.Core.Primitives;
using Moongate.Server.Ultima.Data.Regions;
using Moongate.Server.Ultima.Data.Weather;
using Moongate.Server.Ultima.Entities.World;
using Moongate.Server.Ultima.Interfaces;
using Moongate.Server.Ultima.Types.Weather;

namespace Moongate.Tests.TestSupport.Ultima.World;

/// <summary>
///     Every player is in the profile "temperate", whose weather a test sets or the command forces.
/// </summary>
public sealed class StubWeatherService : IWeatherService
{
    public WeatherState State { get; set; } = new(WeatherKindType.Rain, 40, 12);

    public List<(string Profile, WeatherKindType Kind)> Forced { get; } = [];

    public List<MobileEntity> Resent { get; } = [];

    public Task StartAsync()
    {
        return Task.CompletedTask;
    }

    public Task StopAsync()
    {
        return Task.CompletedTask;
    }

    public void RegionChanged(MobileEntity player, RegionContent? previous, RegionContent? current)
    {
    }

    public void Left(Serial player)
    {
    }

    public string ProfileOf(MobileEntity player)
    {
        return "temperate";
    }

    public WeatherState StateOf(string profile)
    {
        return State;
    }

    public void Force(string profile, WeatherKindType kind)
    {
        Forced.Add((profile, kind));
    }

    public void Resend(MobileEntity player)
    {
        Resent.Add(player);
    }
}
