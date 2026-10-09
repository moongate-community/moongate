using Lua;
using Lua.Standard;
using Moongate.Core.Geometry;
using Moongate.Core.Primitives;
using Moongate.Scripting.Binding;
using Moongate.Scripting.Internal;
using Moongate.Server.Ultima.Data.Cities;
using Moongate.Server.Ultima.Data.Config;
using Moongate.Server.Ultima.Entities.World;
using Moongate.Server.Ultima.Modules;
using Moongate.Server.Ultima.Services;
using Moongate.Tests.TestSupport.Ultima.Loaders;
using Moongate.Tests.TestSupport.Ultima.Movement;
using Moongate.Tests.TestSupport.Ultima.Sectors;
using Moongate.Ultima.Types;

namespace Moongate.Tests.Server.Ultima.Modules;

public sealed class HelpModuleTests
{
    private readonly HelpConfig _config = new() { StuckWaitSeconds = 7, StuckCooldownMinutes = 3 };
    private readonly MobileService _mobiles = new(new StubMovementService(), TestSectors.Create());

    public HelpModuleTests()
    {
        _mobiles.EnterWorld(
            new MobileEntity
            {
                Id = new Serial(2), Name = "Aria", AccountId = new Serial(0x42), Map = MapType.Trammel,
                Location = new Point3D(1600, 1600, 0)
            }
        );
    }

    [Fact]
    public void Settings_AreTheConfiguredOnes()
    {
        var result = Run(Module(), "local s = help.settings() return s.wait_seconds, s.cooldown_minutes");

        Assert.Equal([7, 3], result.Select(value => value.Read<int>()));
    }

    [Fact]
    public void NearestCity_IsTheClosestOnTheMapOfTheCharacter()
    {
        var module = Module(
            City("Far", 3500, 2500, MapType.Trammel),
            City("Near", 1620, 1590, MapType.Trammel),
            City("OtherMap", 1600, 1600, MapType.Felucca)
        );

        var result = Run(module, "local c = help.nearest_city(2) return c.town, c.x, c.y, c.map");

        Assert.Equal("Near", result[0].Read<string>());
        Assert.Equal([1620, 1590, (int)MapType.Trammel], result[1..].Select(value => value.Read<int>()));
    }

    [Fact]
    public void NearestCity_WithNoCityOnThatMap_IsTheFirstOfTheFile()
    {
        var module = Module(City("First", 100, 100, MapType.Malas), City("Second", 200, 200, MapType.Tokuno));

        Assert.Equal("First", Run(module, "return help.nearest_city(2).town")[0].Read<string>());
    }

    [Fact]
    public void NearestCity_WithNoCityAtAll_IsNil()
    {
        Assert.Equal(LuaValue.Nil, Run(Module(), "return help.nearest_city(2)")[0]);
    }

    [Theory, InlineData("999"), InlineData("0"), InlineData("-1")]
    public void NearestCity_ForSomeoneNotInTheWorld_IsNil(string serial)
    {
        var module = Module(City("Near", 1, 1, MapType.Trammel));

        Assert.Equal(LuaValue.Nil, Run(module, $"return help.nearest_city({serial})")[0]);
    }

    private static StartingCityContent City(string town, int x, int y, MapType map)
    {
        return new StartingCityContent { Town = town, Description = "inn", Location = new Point3D(x, y, 0), Map = map };
    }

    private HelpModule Module(params StartingCityContent[] cities)
    {
        return new HelpModule(_mobiles, new StubDataLoaderService().With(cities), _config);
    }

    private static LuaValue[] Run(HelpModule module, string chunk)
    {
        using var state = LuaState.Create();
        state.OpenBasicLibrary();
        var binder = new LuaModuleBinder(NoThreadGuard.Instance);
        binder.Bind(state, module);

        return SyncValueTask.Run(state.DoStringAsync(chunk, "t"));
    }
}
