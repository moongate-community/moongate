using Moongate.Server.Ultima.Data.Config;
using Moongate.Server.Ultima.Data.World;
using Moongate.Server.Ultima.Services;
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

    private ClockService Clock()
    {
        return new(_now, _world);
    }
}
