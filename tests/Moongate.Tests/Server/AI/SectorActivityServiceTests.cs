using Moongate.Core.Geometry;
using Moongate.Core.Primitives;
using Moongate.Persistence.Entities;
using Moongate.Server.Abstractions.Data.Config;
using Moongate.Server.Abstractions.Data.Events;
using Moongate.Server.Services.AI;
using Moongate.Tests.Support;

namespace Moongate.Tests.Server.AI;

public class SectorActivityServiceTests
{
    [Fact]
    public void TrackPlayer_OnePlayer_ActivatesNineSectors()
    {
        var (service, bus, metrics, _) = Build();

        service.TrackPlayer(Player(0x1, 0, 10, 10));

        Assert.Equal(new(9, 0), service.Current);
        Assert.Equal(9, bus.Published.OfType<SectorActivatedEvent>().Count());
        Assert.Equal(9, metrics.Current.ActiveSectors);
        Assert.Equal(0, metrics.Current.GraceSectors);
        Assert.True(service.IsActive(0, 9, 9));
        Assert.True(service.IsActive(0, 10, 10));
        Assert.True(service.IsActive(0, 11, 11));
    }

    [Fact]
    public void TrackPlayer_OverlappingPlayers_IncrementsSharedSectorsWithoutDuplicateActivations()
    {
        var (service, bus, _, _) = Build();
        service.TrackPlayer(Player(0x1, 0, 10, 10));

        service.TrackPlayer(Player(0x2, 0, 11, 10));

        Assert.Equal(new(12, 0), service.Current);
        Assert.Equal(12, bus.Published.OfType<SectorActivatedEvent>().Count());
        Assert.True(service.IsActive(0, 10, 9));
        Assert.True(service.IsActive(0, 11, 11));
    }

    [Fact]
    public void MovePlayer_OneSectorEast_ActivatesNewColumnAndReleasesOldColumn()
    {
        var (service, bus, _, _) = Build();
        service.TrackPlayer(Player(0x1, 0, 10, 10));

        service.MovePlayer(new(0x1), 0, 11, 10);

        Assert.Equal(new(9, 3), service.Current);
        Assert.Equal(12, bus.Published.OfType<SectorActivatedEvent>().Count());
        Assert.Empty(bus.Published.OfType<SectorDeactivatedEvent>());
        Assert.True(service.IsActive(0, 9, 10));
        Assert.True(service.IsActive(0, 12, 10));
    }

    [Fact]
    public void UntrackPlayer_LastReferenceReleased_EntersGraceAndRemainsActive()
    {
        var (service, _, metrics, _) = Build();
        service.TrackPlayer(Player(0x1, 0, 10, 10));

        service.UntrackPlayer(new(0x1));

        Assert.Equal(new(0, 9), service.Current);
        Assert.Equal(0, metrics.Current.ActiveSectors);
        Assert.Equal(9, metrics.Current.GraceSectors);
        Assert.True(service.IsActive(0, 10, 10));
    }

    [Fact]
    public void Tick_BeforeGraceExpires_DoesNotDeactivateSectors()
    {
        var (service, bus, _, time) = Build();
        service.TrackPlayer(Player(0x1, 0, 10, 10));
        service.UntrackPlayer(new(0x1));

        time.Advance(TimeSpan.FromSeconds(59));
        service.Tick();

        Assert.Empty(bus.Published.OfType<SectorDeactivatedEvent>());
        Assert.True(service.IsActive(0, 10, 10));
        Assert.Equal(new(0, 9), service.Current);
    }

    [Fact]
    public void Tick_AtGraceExpiry_DeactivatesEachExpiredSectorOnce()
    {
        var (service, bus, _, time) = Build();
        service.TrackPlayer(Player(0x1, 0, 10, 10));
        service.UntrackPlayer(new(0x1));

        time.Advance(TimeSpan.FromSeconds(60));
        service.Tick();

        Assert.Equal(9, bus.Published.OfType<SectorDeactivatedEvent>().Count());
        Assert.False(service.IsActive(0, 10, 10));
        Assert.Equal(new(0, 0), service.Current);

        service.Tick();
        Assert.Equal(9, bus.Published.OfType<SectorDeactivatedEvent>().Count());
    }

    [Fact]
    public void TrackPlayer_DuringGrace_CancelsDeactivation()
    {
        var (service, bus, _, time) = Build();
        var player = Player(0x1, 0, 10, 10);
        service.TrackPlayer(player);
        service.UntrackPlayer(player.Id);

        time.Advance(TimeSpan.FromSeconds(30));
        service.TrackPlayer(player);
        time.Advance(TimeSpan.FromSeconds(60));
        service.Tick();

        Assert.Equal(new(9, 0), service.Current);
        Assert.Empty(bus.Published.OfType<SectorDeactivatedEvent>());
        Assert.True(service.IsActive(0, 10, 10));
    }

    [Fact]
    public void UntrackPlayer_Twice_IsIdempotent()
    {
        var (service, bus, _, _) = Build();
        service.TrackPlayer(Player(0x1, 0, 10, 10));

        service.UntrackPlayer(new(0x1));
        service.UntrackPlayer(new(0x1));

        Assert.Equal(new(0, 9), service.Current);
        Assert.Empty(bus.Published.OfType<SectorDeactivatedEvent>());
    }

    [Fact]
    public void TrackPlayer_OnAnotherMap_ReleasesOldCoverageAndActivatesNewCoverage()
    {
        var (service, bus, _, _) = Build();
        var player = Player(0x1, 0, 10, 10);
        service.TrackPlayer(player);

        service.TrackPlayer(Player(player.Id, 1, 20, 20));

        Assert.Equal(new(9, 9), service.Current);
        Assert.Equal(18, bus.Published.OfType<SectorActivatedEvent>().Count());
        Assert.True(service.IsActive(0, 10, 10));
        Assert.True(service.IsActive(1, 20, 20));
    }

    [Fact]
    public async Task StartAsync_RegistersOneGraceTimer_AndStopAsyncCancelsIt()
    {
        var (service, _, _, _) = Build();
        var loop = new StubGameLoopContext();
        service = Create(loop, new StubEventBus(), new NpcAiMetrics(), MutableTimeProvider.StartingNow());

        await service.StartAsync();

        Assert.True(loop.Repeating.ContainsKey("npc-sector-activity"));
        Assert.Equal(TimeSpan.FromMilliseconds(100), loop.RepeatingInterval);
        Assert.Null(loop.RepeatingDelay);

        await service.StopAsync();

        Assert.False(loop.Repeating.ContainsKey("npc-sector-activity"));
    }

    private static (SectorActivityService Service, StubEventBus Bus, NpcAiMetrics Metrics, MutableTimeProvider Time) Build()
    {
        var bus = new StubEventBus();
        var metrics = new NpcAiMetrics();
        var time = new MutableTimeProvider(new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));
        var service = Create(new StubGameLoopContext(), bus, metrics, time);

        return (service, bus, metrics, time);
    }

    private static SectorActivityService Create(
        StubGameLoopContext loop,
        StubEventBus bus,
        NpcAiMetrics metrics,
        MutableTimeProvider time
    )
        => new(loop, bus, time, new MoongateConfig(), metrics);

    private static MobileEntity Player(uint serial, int mapId, int sectorX, int sectorY)
        => new() { Id = new(serial), MapId = mapId, Position = new Point3D(sectorX << 4, sectorY << 4, 0) };
}
