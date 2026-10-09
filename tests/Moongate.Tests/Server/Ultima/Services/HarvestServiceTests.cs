using Moongate.Server.Ultima.Data.Harvest;
using Moongate.Server.Ultima.Services;
using Moongate.Tests.Support.Timing;
using Moongate.Tests.TestSupport.Randomness;
using Moongate.Tests.TestSupport.Ultima.Loaders;
using Moongate.Ultima.Types;

namespace Moongate.Tests.Server.Ultima.Services;

public sealed class HarvestServiceTests
{
    private readonly ManualTimeProvider _time = new();
    private readonly ScriptedRandom _random = new();
    private readonly HarvestService _harvest;

    public HarvestServiceTests()
    {
        _harvest = new(
            new StubDataLoaderService().With(
                new HarvestResource
                {
                    Id = "fish", Area = 8, AmountMin = 5, AmountMax = 15, RespawnMinMinutes = 10, RespawnMaxMinutes = 20
                },
                new HarvestResource
                {
                    Id = "wood", Area = 4, AmountMin = 2, AmountMax = 2, RespawnMinMinutes = 20, RespawnMaxMinutes = 20,
                    Vein = [new() { Id = "plain", Weight = 490 }, new() { Id = "oak", Weight = 300 }, new() { Id = "yew", Weight = 210 }]
                }
            ),
            _time,
            _random
        );
    }

    [Fact]
    public void Amount_OfAnAreaNobodyTouched_IsDrawnBetweenItsBounds_AndKept()
    {
        // The draw gives 3 above the least: 8 fish.
        _random.Integers(3);

        Assert.Equal(8, _harvest.Amount("fish", MapType.Trammel, 1600, 1600));
        Assert.Equal(8, _harvest.Amount("fish", MapType.Trammel, 1600, 1600));
    }

    [Fact]
    public void TryTake_TakesOneAtATime_AndRefusesAnEmptyArea()
    {
        // The least: 5 fish.
        var taken = Enumerable.Range(0, 7).Select(_ => _harvest.TryTake("fish", MapType.Trammel, 1600, 1600)).ToArray();

        Assert.Equal([true, true, true, true, true, false, false], taken);
        Assert.Equal(0, _harvest.Amount("fish", MapType.Trammel, 1600, 1600));
    }

    [Fact]
    public void TheCellsOfAnArea_ShareItsFish_TheNextAreaAndAnotherMapDoNot()
    {
        // 1600 to 1607 is one area of 8.
        Assert.True(_harvest.TryTake("fish", MapType.Trammel, 1600, 1600));
        Assert.Equal(4, _harvest.Amount("fish", MapType.Trammel, 1607, 1607));

        Assert.Equal(5, _harvest.Amount("fish", MapType.Trammel, 1608, 1600));
        Assert.Equal(5, _harvest.Amount("fish", MapType.Trammel, 1600, 1599));
        Assert.Equal(5, _harvest.Amount("fish", MapType.Felucca, 1600, 1600));
    }

    [Fact]
    public void AnArea_RefillsAllAtOnce_AfterTheTimeDrawnAtItsFirstTake_AndNotBefore()
    {
        // 5 fish, and the refill comes 4 minutes after the least: 14 minutes.
        _random.Integers(0, 4);
        _harvest.TryTake("fish", MapType.Trammel, 1600, 1600);
        _time.Advance(TimeSpan.FromMinutes(5));
        _harvest.TryTake("fish", MapType.Trammel, 1600, 1600);

        // Counted from the first take, not from the last.
        _time.Advance(TimeSpan.FromMinutes(8.9));
        Assert.Equal(3, _harvest.Amount("fish", MapType.Trammel, 1600, 1600));

        // Drawn again when it refills: 2 above the least.
        _random.Integers(2);
        _time.Advance(TimeSpan.FromMinutes(0.2));
        Assert.Equal(7, _harvest.Amount("fish", MapType.Trammel, 1600, 1600));
    }

    [Fact]
    public void AnEmptyArea_ComesBackToo()
    {
        for (var take = 0; take < 5; take++)
        {
            _harvest.TryTake("fish", MapType.Trammel, 1600, 1600);
        }

        Assert.False(_harvest.TryTake("fish", MapType.Trammel, 1600, 1600));

        _time.Advance(TimeSpan.FromMinutes(10));

        Assert.True(_harvest.TryTake("fish", MapType.Trammel, 1600, 1600));
    }

    [Theory]
    [InlineData("gold", 1600, 1600)]
    [InlineData("FISH", 1600, 1600)]
    [InlineData("fish", -1, 1600)]
    [InlineData("fish", 1600, -9)]
    public void AnUnknownResource_OrAPlaceBelowZero_HasNothing_AndRefuses(string resource, int x, int y)
    {
        Assert.Null(_harvest.Amount(resource, MapType.Trammel, x, y));
        Assert.False(_harvest.TryTake(resource, MapType.Trammel, x, y));
    }

    [Theory]
    [InlineData(0, "plain")]
    [InlineData(489, "plain")]
    [InlineData(490, "oak")]
    [InlineData(789, "oak")]
    [InlineData(790, "yew")]
    [InlineData(999, "yew")]
    public void Vein_OfAnArea_IsDrawnByWeightWhenTheAreaFills_AndSharedByItsCells(int draw, string vein)
    {
        // The amount first, then the vein among the 1000 of the weights.
        _random.Integers(0, draw);

        Assert.Equal(vein, _harvest.Vein("wood", MapType.Trammel, 1600, 1600));
        Assert.Equal(vein, _harvest.Vein("wood", MapType.Trammel, 1603, 1603));
        Assert.Equal(2, _harvest.Amount("wood", MapType.Trammel, 1600, 1600));
    }

    [Fact]
    public void Vein_StaysWhileTheAreaEmpties_AndIsDrawnAgainWhenItRefills()
    {
        _random.Integers(0, 500);
        Assert.Equal("oak", _harvest.Vein("wood", MapType.Trammel, 1600, 1600));
        _harvest.TryTake("wood", MapType.Trammel, 1600, 1600);
        _harvest.TryTake("wood", MapType.Trammel, 1600, 1600);
        Assert.Equal("oak", _harvest.Vein("wood", MapType.Trammel, 1600, 1600));

        _time.Advance(TimeSpan.FromMinutes(20));
        _random.Integers(0, 900);

        Assert.Equal("yew", _harvest.Vein("wood", MapType.Trammel, 1600, 1600));
    }

    [Fact]
    public void Vein_OfAResourceWithoutVeins_OrUnknown_IsNone()
    {
        Assert.Null(_harvest.Vein("fish", MapType.Trammel, 1600, 1600));
        Assert.Null(_harvest.Vein("gold", MapType.Trammel, 1600, 1600));
        Assert.Null(_harvest.Vein("wood", MapType.Trammel, -1, 1600));
    }

    [Fact]
    public void Has_TellsTheResourcesOfTheFile()
    {
        Assert.True(_harvest.Has("fish"));
        Assert.False(_harvest.Has("gold"));
    }
}
