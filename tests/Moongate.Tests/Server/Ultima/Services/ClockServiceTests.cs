using Moongate.Server.Ultima.Data.Config;
using Moongate.Server.Ultima.Data.World;
using Moongate.Server.Ultima.Services;
using Moongate.Server.Ultima.Types.World;
using Moongate.Tests.TestSupport.Timing;
using Moongate.Ultima.Types;

namespace Moongate.Tests.Server.Ultima.Services;

public sealed class ClockServiceTests
{
    private static readonly DateTimeOffset WorldStart = new(1997, 9, 1, 0, 0, 0, TimeSpan.Zero);

    private readonly SettableClock _now = new() { Now = WorldStart };
    private readonly WorldConfig _world = new();

    [Fact]
    public void GetTime_AtTheWorldStart_OnFeluccaAtTheWestEdge_IsMidnight()
    {
        Assert.Equal(new GameTime(0, 0), Clock().GetTime(MapType.Felucca, 0));
    }

    [Fact]
    public void GetTime_AGameMinuteLastsSecondsPerUoMinute()
    {
        _now.Advance(TimeSpan.FromSeconds(90 * 5 + 4));

        Assert.Equal(new GameTime(1, 30), Clock().GetTime(MapType.Felucca, 0));
    }

    [Fact]
    public void GetTime_EachMapIs320MinutesAhead_AndEvery16TilesEastOneMinute()
    {
        Assert.Equal(new GameTime(5, 20), Clock().GetTime(MapType.Trammel, 0));
        Assert.Equal(new GameTime(10, 40), Clock().GetTime(MapType.Ilshenar, 0));
        Assert.Equal(new GameTime(0, 10), Clock().GetTime(MapType.Felucca, 175));
    }

    [Fact]
    public void GetTime_WrapsAfter24Hours()
    {
        _now.Advance(TimeSpan.FromSeconds((24 * 60 + 1) * 5));

        Assert.Equal(new GameTime(0, 1), Clock().GetTime(MapType.Felucca, 0));
    }

    [Fact]
    public void GetTime_FollowsTheConfiguredSecondsPerUoMinute()
    {
        _world.SecondsPerUoMinute = 10;
        _now.Advance(TimeSpan.FromSeconds(450));

        Assert.Equal(new GameTime(0, 45), Clock().GetTime(MapType.Felucca, 0));
    }

    [Fact]
    public void GetDay_CountsTheGameDaysOfTheMap_WithItsOffset()
    {
        Assert.Equal((0, 0), (Clock().GetDay(MapType.Felucca), Clock().GetDay(MapType.Trammel)));

        // 1120 game minutes later Trammel, 320 minutes ahead, starts its second day.
        _now.Advance(TimeSpan.FromSeconds(1120 * 5));

        Assert.Equal((0, 1), (Clock().GetDay(MapType.Felucca), Clock().GetDay(MapType.Trammel)));
    }

    [Fact]
    public void GetMoonPhase_FeluccaTurnsEvery10GameMinutes_AndTrammelEvery30()
    {
        // At the world start Trammel's clock is already 320 minutes ahead: 320 / 30 = 10, the third phase.
        Assert.Equal(
            (MoonPhaseType.NewMoon, MoonPhaseType.FirstQuarter),
            (Clock().GetMoonPhase(MapType.Felucca, 0), Clock().GetMoonPhase(MapType.Trammel, 0))
        );

        _now.Advance(TimeSpan.FromSeconds(10 * 5));
        Assert.Equal(MoonPhaseType.WaxingCrescent, Clock().GetMoonPhase(MapType.Felucca, 0));

        _now.Advance(TimeSpan.FromSeconds(70 * 5));
        Assert.Equal(MoonPhaseType.NewMoon, Clock().GetMoonPhase(MapType.Felucca, 0));
    }

    [Fact]
    public void GetMoonPhase_MovesWithTheLongitudeAsTheTime()
    {
        Assert.Equal(MoonPhaseType.WaxingCrescent, Clock().GetMoonPhase(MapType.Felucca, 160));
    }

    private ClockService Clock()
    {
        return new(_now, _world);
    }
}
