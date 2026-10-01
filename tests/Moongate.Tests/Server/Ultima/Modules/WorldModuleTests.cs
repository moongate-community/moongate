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
using Moongate.Server.Ultima.Data.World;
using Moongate.Server.Ultima.Entities.World;
using Moongate.Server.Ultima.Modules;
using Moongate.Server.Ultima.Types.World;
using Moongate.Server.Ultima.Services;
using Moongate.Tests.TestSupport.Ultima.Sectors;
using Moongate.Tests.TestSupport.Ultima.World;
using Moongate.Ultima.Types;

namespace Moongate.Tests.Server.Ultima.Modules;

public sealed class WorldModuleTests : IAsyncLifetime
{
    private readonly SectorService _sectors = TestSectors.Create();
    private readonly StubClockService _clock = new() { Time = new GameTime(21, 5) };
    private readonly ItemService _items = TestItems.Create();
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

    private LuaValue[] Run(string chunk)
    {
        using var state = LuaState.Create();
        state.OpenBasicLibrary();
        var binder = new LuaModuleBinder(NoThreadGuard.Instance);
        binder.Bind(state, new WorldModule(_sectors, _clock, _fixture.Sessions, _items));
        binder.BindEnum(state, typeof(MapType));
        binder.BindEnum(state, typeof(MoonPhaseType));

        return SyncValueTask.Run(state.DoStringAsync(chunk, "t"));
    }
}
