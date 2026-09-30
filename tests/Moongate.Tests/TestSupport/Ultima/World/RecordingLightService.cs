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

    public Task SetOverrideAsync(int? level, CancellationToken cancellationToken = default)
    {
        Calls++;
        Override = level;

        return Task.CompletedTask;
    }
}
