using Moongate.Tests.TestSupport.Persistence;
using Moongate.Tests.TestSupport.Timing;
using Moongate.Tests.TestSupport.Ultima.Items;
using Moongate.Tests.TestSupport.Ultima.Speech;
using Moongate.Server.Core.Types.Accounts;
using Moongate.Server.Core.Data.Sessions;
using Lua;
using Lua.Standard;
using Moongate.Core.Geometry;
using Moongate.Core.Primitives;
using Moongate.Scripting.Binding;
using Moongate.Scripting.Internal;
using Moongate.Server.Ultima.Data.Regions;
using Moongate.Server.Ultima.Data.World;
using Moongate.Server.Ultima.Entities.World;
using Moongate.Server.Ultima.Modules;
using Moongate.Server.Ultima.Types.World;
using Moongate.Server.Ultima.Services;
using Moongate.Tests.TestSupport.Ultima.Loaders;
using Moongate.Tests.TestSupport.Ultima.Sectors;
using Moongate.Tests.TestSupport.Ultima.World;
using Moongate.Server.Ultima.Types.Weather;
using Moongate.Tests.TestSupport.Ultima.Commands;
using Moongate.Tests.TestSupport.Ultima.Movement;
using Moongate.Ultima.Types;

namespace Moongate.Tests.Server.Ultima.Modules;

public sealed class WorldModuleTests : IAsyncLifetime
{
    private readonly SectorService _sectors = TestSectors.Create();
    private readonly SettableClock _time = new();
    private readonly WorldPropsService _props = new(new RecordingDataAccess<WorldStateEntity>());
    private readonly StubClockService _clock = new() { Time = new GameTime(21, 5) };
    private readonly ItemService _items = TestItems.Create();
    private readonly RegionService _regions = new(
        new StubDataLoaderService().With(
            new RegionContent
            {
                Map = MapType.Trammel, Name = "Britain", Guarded = true,
                Areas = [new RegionAreaContent { X1 = 1400, Y1 = 1500, X2 = 1700, Y2 = 1800 }]
            },
            new RegionContent
            {
                Map = MapType.Trammel, Name = "Covetous", Areas = [new RegionAreaContent { X1 = 2400, Y1 = 400, X2 = 2600, Y2 = 600 }]
            }
        )
    );
    private readonly StubLineOfSightService _sight = new();
    private readonly StubMovementService _movement = new() { SpawnZ = (x, _) => x == 1600 ? 7 : null };
    private readonly StubWeatherService _weather = new();
    private readonly StubSeasonService _seasons = new();
    private readonly ControlledBroadcastService _broadcast = new();
    private BroadcastFixture _fixture = null!;

    public WorldModuleTests()
    {
        _sectors.Add(new MobileEntity { Id = new Serial(0x100), Name = "orc", Map = MapType.Trammel, Location = new Point3D(1600, 1600, 0) });
        _sectors.Add(
            new MobileEntity
            {
                Id = new Serial(2), Name = "Aria", AccountId = new Serial(0x42), Map = MapType.Felucca, Location = new Point3D(1400, 1600, 0)
            }
        );
    }

    public async Task InitializeAsync()
    {
        _fixture = await BroadcastFixture.CreateAsync();
        await _fixture.AddAsync(2);
        var backpack = new ItemEntity { Id = new Serial(0x40000001), TemplateId = "backpack", ItemId = 0x0E75, Amount = 1 };
        backpack.Equip(new Serial(2), LayerType.Backpack);
        var pouch = new ItemEntity { Id = new Serial(0x40000002), TemplateId = "pouch", ItemId = 0x0E79, Amount = 1 };
        pouch.PutInContainer(backpack.Id, new Point2D(44, 65));
        var key = new ItemEntity
        {
            Id = new Serial(0x40000003), TemplateId = "0x1010_iron_key", ItemId = 0x1010, Amount = 1,
            Props = new() { ["key.value"] = 1234L }
        };
        key.PutInContainer(pouch.Id, new Point2D(44, 65));
        var bank = new ItemEntity { Id = new Serial(0x40000004), TemplateId = "bank_box", ItemId = 0x0E7C, Amount = 1 };
        bank.Equip(new Serial(2), LayerType.Bank);
        var banked = new ItemEntity
        {
            Id = new Serial(0x40000005), TemplateId = "0x1010_iron_key", ItemId = 0x1010, Amount = 1,
            Props = new() { ["key.value"] = 777L }
        };
        banked.PutInContainer(bank.Id, new Point2D(44, 65));
        _items.Add([backpack, pouch, key, bank, banked]);
        var gm = await _fixture.AddAsync(3);
        await _fixture.Network.ExecuteOnLoopAsync(() => gm.Set(SessionKeys.AccountType, AccountType.GameMaster));
    }

    public async Task DisposeAsync()
    {
        await _fixture.DisposeAsync();
    }

    [Theory,
     InlineData("return world.is_staff(3)", true),
     InlineData("return world.is_staff(2)", false),
     InlineData("return world.is_staff(0x100)", false)]
    public void IsStaff_TellsWhetherThePlayersAccountIsAGameMasterOrAbove(string chunk, bool expected)
    {
        Assert.Equal(expected, Run(chunk)[0].Read<bool>());
    }

    [Theory,
     InlineData("return world.carries(2, 'key.value', 1234)", true),
     InlineData("return world.carries(2, 'key.value', 999)", false),
     InlineData("return world.carries(3, 'key.value', 1234)", false),
     InlineData("return world.carries(2, 'door.open', true)", false),
     InlineData("return world.carries(2, 'key.value', 777)", false)]
    public void Carries_LooksThroughEverythingThePlayerWearsAndCarries_ButTheBank(string chunk, bool expected)
    {
        Assert.Equal(expected, Run(chunk)[0].Read<bool>());
    }

    [Theory,
     InlineData("return world.is_occupied(MapType.Trammel, 1600, 1600)", true),
     InlineData("return world.is_occupied('Felucca', 1400, 1600)", true),
     InlineData("return world.is_occupied(MapType.Trammel, 1601, 1600)", false),
     InlineData("return world.is_occupied(MapType.Felucca, 1600, 1600)", false)]
    public void IsOccupied_TellsWhetherAMobileStandsOnTheTile(string chunk, bool expected)
    {
        Assert.Equal(expected, Run(chunk)[0].Read<bool>());
    }

    [Fact]
    public void Time_GivesTheHoursAndMinutesOfTheMap()
    {
        var result = Run("local t = world.time(MapType.Trammel, 1600) return t.hours, t.minutes");

        Assert.Equal((21, 5), (result[0].Read<int>(), result[1].Read<int>()));
    }

    [Fact]
    public void Moon_GivesThePhaseOfTheMoon_AsAMoonPhaseType()
    {
        var result = Run("return world.moon(MapType.Trammel, 1600) == MoonPhaseType.FullMoon");

        Assert.True(result[0].Read<bool>());
    }

    [Theory,
     InlineData("return world.is_guarded(MapType.Trammel, 1496, 1628, 10)", true),
     InlineData("return world.is_guarded(MapType.Felucca, 1496, 1628, 10)", false),
     InlineData("return world.is_guarded(MapType.Trammel, 1000, 1000, 0)", false),
     InlineData("return world.is_guarded(MapType.Trammel, 2500, 500, 0)", false),
     InlineData("return world.is_guarded(MapType.Trammel, 1496, 1628, 300)", false)]
    public void IsGuarded_TellsWhetherTheRegionOfThePlaceHasGuards(string chunk, bool expected)
    {
        Assert.Equal(expected, Run(chunk)[0].Read<bool>());
    }

    [Fact]
    public void Region_NamesTheRegionOfAPlace()
    {
        var result = Run("return world.region('Trammel', 1500, 1600, 0), world.region('Trammel', 3000, 3000, 0), world.region('Trammel', 1500, 1600, 500)");

        Assert.Equal("Britain", result[0].Read<string>());
        Assert.Equal((LuaValue.Nil, LuaValue.Nil), (result[1], result[2]));
    }

    [Fact]
    public void MobilesInRange_ListsWhoStandsAround()
    {
        var result = Run(
            "local near = world.mobiles_in_range('Trammel', 1598, 1600, 2) " +
            "return #near, near[1], #world.mobiles_in_range('Trammel', 1598, 1600, 1), #world.mobiles_in_range('Trammel', 1598, 1600, 33)"
        );

        Assert.Equal([1, 0x100, 0, 0], result.Select(value => value.Read<long>()));
    }

    [Fact]
    public void ItemsInRange_ListsTheGroundItemsAround()
    {
        var gold = new ItemEntity { Id = new Serial(0x40000050), TemplateId = "gold", ItemId = 0x0EED, Amount = 1 };
        var items = TestItems.Create(_sectors);
        items.Add([gold]);
        items.PlaceOnGround(gold, MapType.Trammel, new Point3D(1601, 1600, 0));

        var result = Run("local found = world.items_in_range('Trammel', 1600, 1600, 1) return #found, found[1], #world.items_in_range('Trammel', 1500, 1600, 1)");

        Assert.Equal([1, 0x40000050, 0], result.Select(value => value.Read<long>()));
    }

    [Fact]
    public void Players_ListsTheCharactersInTheWorld()
    {
        var result = Run("local all = world.players() table.sort(all) return #all, all[1], all[2]");

        Assert.Equal([2, 2, 3], result.Select(value => value.Read<long>()));
    }

    [Fact]
    public void LineOfSight_AsksTheMap()
    {
        Assert.True(Run("return world.line_of_sight('Trammel', 1, 1, 0, 5, 5, 0)")[0].Read<bool>());

        _sight.Allow = false;

        Assert.False(Run("return world.line_of_sight('Trammel', 1, 1, 0, 5, 5, 0)")[0].Read<bool>());
        _sight.Allow = true;
        Assert.False(Run("return world.line_of_sight('Trammel', 1, 1, 0, 5, 5, 300)")[0].Read<bool>());
    }

    [Fact]
    public void StandingZ_IsWhereAMobileCanStand_OrNil()
    {
        var result = Run("return world.standing_z('Trammel', 1600, 1600, 20), world.standing_z('Trammel', 1601, 1600, 20), world.standing_z('Trammel', -5, 1600, 20)");

        Assert.Equal(7, result[0].Read<int>());
        Assert.Equal((LuaValue.Nil, LuaValue.Nil), (result[1], result[2]));
    }

    [Fact]
    public void WeatherAndSeason_ReadTheSky()
    {
        Assert.True(_fixture.Mobiles.TryGet(new Serial(2), out var aria));
        aria.AccountId = new Serial(0x42);

        var result = Run(
            "local sky = world.weather(2) return sky.kind == WeatherKindType.Rain, sky.density, sky.temperature, world.weather(256), " +
            "world.season('Trammel') == SeasonType.Summer"
        );

        Assert.True(result[0].Read<bool>());
        Assert.Equal((40, 12), (result[1].Read<int>(), result[2].Read<int>()));
        Assert.Equal(LuaValue.Nil, result[3]);
        Assert.True(result[4].Read<bool>());
    }

    [Fact]
    public void Broadcast_SendsTheTextToEveryone_CutTo128Characters()
    {
        var result = Run("return world.broadcast(string.rep('a', 200)), world.broadcast('  ')");

        Assert.True(result[0].Read<bool>());
        Assert.False(result[1].Read<bool>());
        Assert.Equal(128, Assert.Single(_broadcast.Messages).Length);
    }

    [Fact]
    public void Props_AreKeptForTheWholeShard_ReplacedAndRemoved()
    {
        var result = Run(
            """
            local set = world.set_prop("event.day", 12) and world.set_prop("motto", "hail") and world.set_prop("open", true)
            world.set_prop("motto", nil)
            return set, world.get_prop("event.day"), world.get_prop("open"), world.get_prop("motto"), world.get_prop("never")
            """
        );

        Assert.True(result[0].Read<bool>());
        Assert.Equal(12, result[1].Read<int>());
        Assert.True(result[2].Read<bool>());
        Assert.Equal([LuaValueType.Nil, LuaValueType.Nil], result[3..].Select(value => value.Type));
        Assert.Equal(12L, _props.Get("event.day"));
    }

    [Theory]
    [InlineData("world.set_prop('', 1)")]
    [InlineData("world.set_prop('   ', 1)")]
    [InlineData("world.set_prop('list', {})")]
    [InlineData("world.set_prop('fn', function() end)")]
    public void SetProp_ABlankKeyOrAValueThatCannotBeKept_IsFalse(string call)
    {
        Assert.False(Run("return " + call)[0].Read<bool>());

        Assert.Null(_props.State.Props);
    }

    [Fact]
    public void Now_IsTheSecondsSince1970_OfTheServerClock()
    {
        _time.Now = new DateTimeOffset(2026, 10, 3, 12, 0, 5, TimeSpan.Zero);

        Assert.Equal(1_791_028_805, Run("return world.now()")[0].Read<long>());
    }

    private LuaValue[] Run(string chunk)
    {
        using var state = LuaState.Create();
        state.OpenBasicLibrary();
        var binder = new LuaModuleBinder(NoThreadGuard.Instance);
        binder.Bind(
            state,
            new WorldModule(_sectors, _clock, _fixture.Sessions, _items, _regions, _sight, _movement, _weather, _seasons, _broadcast, _fixture.Mobiles, _time, _props)
        );
        binder.BindEnum(state, typeof(MapType));
        binder.BindEnum(state, typeof(MoonPhaseType));
        binder.BindEnum(state, typeof(SeasonType));
        binder.BindEnum(state, typeof(WeatherKindType));
        state.OpenStringLibrary();
        state.OpenTableLibrary();

        return SyncValueTask.Run(state.DoStringAsync(chunk, "t"));
    }
}
