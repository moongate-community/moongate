using Moongate.Core.Primitives;
using Moongate.Server.Ultima.Data.Regions;
using Moongate.Server.Ultima.Entities.World;
using Moongate.Server.Ultima.Interfaces;

namespace Moongate.Tests.TestSupport.Ultima.World;

/// <summary>
///     Keeps the override it is given; every level is the override or 0.
/// </summary>
public sealed class RecordingLightService : ILightService
{
    public int? Override { get; private set; }

    public int Calls { get; private set; }

    public Task StartAsync()
    {
        return Task.CompletedTask;
    }

    public Task StopAsync()
    {
        return Task.CompletedTask;
    }

    public int LevelFor(MobileEntity mobile)
    {
        return Override ?? 0;
    }

    public int LevelOnLogin(MobileEntity character)
    {
        return LevelFor(character);
    }

    public void SetOverride(int? level)
    {
        Calls++;
        Override = level;
    }

    public Task SetOverrideAsync(int? level, CancellationToken cancellationToken = default)
    {
        Calls++;
        Override = level;

        return Task.CompletedTask;
    }

    public void RegionChanged(MobileEntity player, RegionContent? previous, RegionContent? current)
    {
    }

    public void Left(Serial player)
    {
    }
}
