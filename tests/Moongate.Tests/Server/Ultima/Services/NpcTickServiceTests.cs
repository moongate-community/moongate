using Moongate.Core.Geometry;
using Moongate.Core.Primitives;
using Moongate.Server.Ultima.Data.Config;
using Moongate.Server.Ultima.Entities.World;
using Moongate.Server.Ultima.Services;
using Moongate.Tests.TestSupport.Scripting;
using Moongate.Tests.TestSupport.Ultima.Npcs;
using Moongate.Ultima.Types;

using Moongate.Server.Ultima.Interfaces;
using Moongate.Tests.TestSupport.Timing;
using Moongate.Tests.TestSupport.Ultima.Mobiles;
namespace Moongate.Tests.Server.Ultima.Services;

public sealed class NpcTickServiceTests
{
    [Fact]
    public void Wake_RegistersOneRepeatingTimerAtTheConfiguredInterval()
    {
        var timers = new RecordingTimerService();
        var ticks = new NpcTickService(timers, new NpcsConfig { ThinkIntervalMs = 750 });

        ticks.Wake(Npc(0x100));

        var timer = Assert.Single(timers.Timers);
        Assert.Equal("npc_think", timer.Name);
        Assert.Equal(TimeSpan.FromMilliseconds(750), timer.Interval);
        Assert.True(timer.Repeat);
        Assert.InRange(timer.Delay!.Value, TimeSpan.FromMilliseconds(1), TimeSpan.FromMilliseconds(256));
        Assert.True(ticks.IsAwake(new Serial(0x100)));
        Assert.Equal(1, ticks.AwakeCount);
    }

    [Fact]
    public void Wake_Twice_RegistersOnce()
    {
        var timers = new RecordingTimerService();
        var ticks = new NpcTickService(timers, new NpcsConfig());
        var npc = Npc(0x100);

        ticks.Wake(npc);
        ticks.Wake(npc);

        Assert.Single(timers.Timers);
    }

    [Fact]
    public void Wake_APlayer_IsIgnored()
    {
        var timers = new RecordingTimerService();
        var ticks = new NpcTickService(timers, new NpcsConfig());
        var player = Npc(2);
        player.AccountId = new Serial(0x42);

        ticks.Wake(player);

        Assert.Empty(timers.Timers);
        Assert.Equal(0, ticks.AwakeCount);
    }

    [Fact]
    public void Sleep_UnregistersTheTimer()
    {
        var timers = new RecordingTimerService();
        var ticks = new NpcTickService(timers, new NpcsConfig());
        var npc = Npc(0x100);
        ticks.Wake(npc);

        ticks.Sleep(npc);

        Assert.Empty(timers.Timers);
        Assert.False(ticks.IsAwake(npc.Id));
        Assert.Equal(0, ticks.AwakeCount);
    }

    [Fact]
    public void Sleep_WhenAsleep_DoesNothing()
    {
        var timers = new RecordingTimerService();
        var ticks = new NpcTickService(timers, new NpcsConfig());

        ticks.Sleep(Npc(0x100));

        Assert.Empty(timers.Unregistered);
    }

    [Fact]
    public void Fire_CountsTheThinkAndCallsTheThinker()
    {
        var timers = new RecordingTimerService();
        var thinker = new RecordingNpcThinker();
        var ticks = new NpcTickService(timers, new NpcsConfig(), thinker);
        var npc = Npc(0x100);
        ticks.Wake(npc);

        timers.Fire(timers.Timers[0].Id);
        timers.Fire(timers.Timers[0].Id);

        Assert.Equal(2, ticks.ThinkCount);
        Assert.Equal([npc, npc], thinker.Thought);
    }

    [Fact]
    public void Fire_AThink_RegeneratesTheNpc()
    {
        var timers = new RecordingTimerService();
        var clock = new SettableClock();
        var state = new RecordingMobileStateService { Apply = true };
        var regeneration = new RegenerationService(state, new RegenerationConfig(), clock);
        var ticks = new NpcTickService(timers, new NpcsConfig(), null, new Lazy<IRegenerationService>(() => regeneration));
        var npc = Npc(0x100);
        npc.Hits = 5;
        npc.HitsMax = 10;
        ticks.Wake(npc);

        timers.Fire(timers.Timers[0].Id);
        clock.Advance(TimeSpan.FromSeconds(11));
        timers.Fire(timers.Timers[0].Id);

        Assert.Equal(6, npc.Hits);
    }

    [Fact]
    public void Fire_WithoutAThinker_StillCounts()
    {
        var timers = new RecordingTimerService();
        var ticks = new NpcTickService(timers, new NpcsConfig());
        ticks.Wake(Npc(0x100));

        timers.Fire(timers.Timers[0].Id);

        Assert.Equal(1, ticks.ThinkCount);
    }

    [Fact]
    public void Fire_AThinkerThatThrows_DoesNotEscapeAndKeepsTheNpcAwake()
    {
        // A timer callback that throws closes the whole timer wheel.
        var timers = new RecordingTimerService();
        var thinker = new RecordingNpcThinker { Throw = true };
        var ticks = new NpcTickService(timers, new NpcsConfig(), thinker);
        var npc = Npc(0x100);
        ticks.Wake(npc);

        timers.Fire(timers.Timers[0].Id);

        Assert.True(ticks.IsAwake(npc.Id));
        Assert.Single(timers.Timers);
    }

    [Fact]
    public void Wake_ManyNpcs_NeverAsksForAZeroDelay()
    {
        // The wheel refuses a first delay of zero; 2000 draws of 0-255 would hit it almost surely.
        var timers = new RecordingTimerService();
        var ticks = new NpcTickService(timers, new NpcsConfig());

        for (uint serial = 0x100; serial < 0x100 + 2000; serial++)
        {
            ticks.Wake(Npc(serial));
        }

        Assert.Equal(2000, ticks.AwakeCount);
        Assert.All(timers.Timers, timer => Assert.True(timer.Delay > TimeSpan.Zero));
    }

    [Fact]
    public void Wake_WhenTheWheelRefuses_LeavesTheNpcAsleepWithoutThrowing()
    {
        // A throw would leave the sector that called Wake half updated.
        var timers = new RecordingTimerService { ThrowOnRegister = true };
        var ticks = new NpcTickService(timers, new NpcsConfig());
        var npc = Npc(0x100);

        ticks.Wake(npc);

        Assert.False(ticks.IsAwake(npc.Id));
        Assert.Equal(0, ticks.AwakeCount);
    }

    private static MobileEntity Npc(uint serial)
    {
        return new()
        {
            Id = new Serial(serial),
            Name = $"M{serial}",
            Map = MapType.Trammel,
            Location = new Point3D(1600, 1600, 0)
        };
    }
}
