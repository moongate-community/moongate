using Moongate.Core.Primitives;
using Moongate.Server.Ultima.Data.Regions;
using Moongate.Server.Ultima.Entities.World;
using Moongate.Server.Ultima.Interfaces;
using Moongate.Ultima.Types;

namespace Moongate.Tests.TestSupport.Ultima.World;

/// <summary>
///     Answers <see cref="Here" /> for every player and records the tracks played.
/// </summary>
public sealed class StubMusicService : IMusicService
{
    public MusicType Here { get; set; } = MusicType.Britain1;

    public List<(MobileEntity Player, MusicType Music)> Played { get; } = [];

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

    public MusicType MusicOf(MobileEntity player)
    {
        return Here;
    }

    public void Play(MobileEntity player, MusicType music)
    {
        Played.Add((player, music));
    }
}
