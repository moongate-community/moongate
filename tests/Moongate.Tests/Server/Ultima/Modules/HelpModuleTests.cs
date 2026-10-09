using Lua;
using Lua.Standard;
using Moongate.Core.Geometry;
using Moongate.Core.Primitives;
using Moongate.Scripting.Binding;
using Moongate.Scripting.Internal;
using Moongate.Server.Ultima.Data.Cities;
using Moongate.Server.Ultima.Data.Config;
using Moongate.Server.Ultima.Entities.World;
using Moongate.Server.Ultima.Interfaces;
using Moongate.Server.Ultima.Modules;
using Moongate.Server.Ultima.Services;
using Moongate.Tests.TestSupport.Ultima.Loaders;
using Moongate.Server.Ultima.Types.Help;
using Moongate.Tests.TestSupport.Ultima.Help;
using Moongate.Tests.TestSupport.Ultima.Speech;
using Moongate.Ultima.Types;

namespace Moongate.Tests.Server.Ultima.Modules;

public sealed class HelpModuleTests : IAsyncLifetime
{
    private const long Aria = 2;
    private const long Gino = 3;

    private readonly HelpConfig _config = new() { StuckWaitSeconds = 7, StuckCooldownMinutes = 3 };

    private BroadcastFixture _fixture = null!;
    private HelpPageServices _pages = null!;

    private IMobileService _mobiles => _fixture.Mobiles;

    public async Task InitializeAsync()
    {
        _fixture = await BroadcastFixture.CreateAsync();
        await _fixture.AddAsync(Aria);
        await _fixture.AddAsync(Gino);
        var aria = Mobile(Aria);
        aria.Name = "Aria";
        aria.AccountId = new Serial(0x42);
        _mobiles.MoveTo(aria, MapType.Trammel, new Point3D(1600, 1600, 0));
        Mobile(Gino).Name = "Gino";
        _pages = HelpPageServices.Create(_fixture);
        _pages.Config.PageCooldownSeconds = 60;
        await _pages.Service.StartAsync();
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

    [Fact]
    public void CreatePage_ReturnsTheNumber_AndThePageIsListed()
    {
        var result = Run(
            Module(),
            "local made = help.create_page(2, HelpPageKindType.Bug, 'The door is stuck') local p = help.page(made.id) return made.id, p.name, p.kind, p.status, p.text, p.online, p.account"
        );

        Assert.True(result[0].Read<int>() > 0);
        Assert.Equal("Aria", result[1].Read<string>());
        Assert.Equal(
            [(int)HelpPageKindType.Bug, (int)HelpPageStatusType.Open],
            result[2..4].Select(value => value.Read<int>())
        );
        Assert.Equal("The door is stuck", result[4].Read<string>());
        Assert.True(result[5].Read<bool>());
        Assert.Equal(0x42, result[6].Read<int>());
    }

    [Fact]
    public void CreatePage_Twice_AnswersOpen_AndNothingIsAdded()
    {
        var result = Run(
            Module(),
            "help.create_page(2, 0, 'a') local again = help.create_page(2, 0, 'b') return again.id, again.reason, #help.pages()"
        );

        Assert.Equal(LuaValue.Nil, result[0]);
        Assert.Equal(("open", 1), (result[1].Read<string>(), result[2].Read<int>()));
    }

    [Fact]
    public void CreatePage_InsideThePause_AnswersWaitAndTheSeconds()
    {
        var result = Run(
            Module(),
            "local made = help.create_page(2, 0, 'a') help.close(made.id, 3) local again = help.create_page(2, 0, 'b') return again.reason, again.seconds"
        );

        Assert.Equal(("wait", 60), (result[0].Read<string>(), result[1].Read<int>()));
    }

    [Theory,
     InlineData("help.create_page(2, 0, '   ')", "text"),
     InlineData("help.create_page(2, 9, 'a')", "text"),
     InlineData("help.create_page(999, 0, 'a')", "gone"),
     InlineData("help.create_page(0, 0, 'a')", "gone"),
     InlineData("help.create_page(-4, 0, 'a')", "gone")]
    public void CreatePage_ABadRequest_AnswersWhy(string call, string reason)
    {
        var result = Run(Module(), $"local made = {call} return made.id, made.reason");

        Assert.Equal(LuaValue.Nil, result[0]);
        Assert.Equal(reason, result[1].Read<string>());
    }

    [Fact]
    public void CanPage_TellsWhetherAPageWouldBeAccepted()
    {
        var module = Module();

        var first = Run(module, "local c = help.can_page(2) return c.ok, c.reason");
        Run(module, "help.create_page(2, 0, 'a')");
        var second = Run(module, "local c = help.can_page(2) return c.ok, c.reason");

        Assert.True(first[0].Read<bool>());
        Assert.Equal((false, "open"), (second[0].Read<bool>(), second[1].Read<string>()));
        Assert.Equal(1, Run(module, "return #help.pages()")[0].Read<int>());
    }

    [Fact]
    public void Pages_ListsTheActiveOnesOldestFirst_WithTheirAge()
    {
        var module = Module();
        Run(module, "help.create_page(2, 0, 'first')");
        _pages.Clock.Advance(TimeSpan.FromSeconds(125));
        Run(module, "help.create_page(3, 1, 'second')");

        var result = Run(
            module,
            "local list = help.pages() return #list, list[1].text, list[1].age_seconds, list[2].text, list[2].age_seconds"
        );

        Assert.Equal(2, result[0].Read<int>());
        Assert.Equal(
            ("first", 125, "second", 0),
            (result[1].Read<string>(), result[2].Read<int>(), result[3].Read<string>(), result[4].Read<int>())
        );
    }

    [Fact]
    public void Take_Answer_Close_UseTheNameOfTheStaffMobile()
    {
        var module = Module();

        var result = Run(
            module,
            "local id = help.create_page(2, 0, 'x').id return help.take(id, 3), help.page(id).taken_by, help.answer(id, 3, 'Go north'), help.page(id).status, help.page(id).answer, help.answer(id, 3, 'again'), help.close(id, 3), help.waiting()"
        );

        Assert.True(result[0].Read<bool>());
        Assert.Equal("Gino", result[1].Read<string>());
        Assert.True(result[2].Read<bool>());
        Assert.Equal((int)HelpPageStatusType.Closed, result[3].Read<int>());
        Assert.Equal("Go north", result[4].Read<string>());
        Assert.Equal([false, false], result[5..7].Select(value => value.Read<bool>()));
        Assert.Equal(0, result[7].Read<int>());
        Assert.Equal(
            ["Game master Gino answers: Go north"],
            _pages.Speech.Told.Where(told => told.Player.Id.Value == Aria).Select(told => told.Text)
        );
    }

    [Fact]
    public void Take_ForAStaffNotInTheWorld_IsFalse_AndThePageStaysOpen()
    {
        var result = Run(
            Module(),
            "local id = help.create_page(2, 0, 'x').id return help.take(id, 999), help.answer(id, 999, 'x'), help.close(id, 999), help.page(id).status"
        );

        Assert.Equal([false, false, false], result[..3].Select(value => value.Read<bool>()));
        Assert.Equal((int)HelpPageStatusType.Open, result[3].Read<int>());
    }

    [Fact]
    public void Page_Unknown_IsNil_AndSerialsOutOfRangeAreFalse()
    {
        var result = Run(
            Module(),
            "return help.page(0), help.page(77), help.take(-1, 3), help.answer(1099511627776, 3, 'x'), help.close(0, 3)"
        );

        Assert.Equal([LuaValue.Nil, LuaValue.Nil, false, false, false], result);
    }

    public async Task DisposeAsync()
    {
        _pages.Dispose();
        await _fixture.DisposeAsync();
    }

    private MobileEntity Mobile(long serial)
    {
        Assert.True(_mobiles.TryGet(new Serial((uint)serial), out var mobile));

        return mobile;
    }

    private static StartingCityContent City(string town, int x, int y, MapType map)
    {
        return new StartingCityContent { Town = town, Description = "inn", Location = new Point3D(x, y, 0), Map = map };
    }

    private HelpModule Module(params StartingCityContent[] cities)
    {
        return new HelpModule(_mobiles, new StubDataLoaderService().With(cities), _config, _pages.Service, _pages.Clock);
    }

    private static LuaValue[] Run(HelpModule module, string chunk)
    {
        using var state = LuaState.Create();
        state.OpenBasicLibrary();
        var binder = new LuaModuleBinder(NoThreadGuard.Instance);
        binder.Bind(state, module);
        binder.BindEnum(state, typeof(HelpPageKindType));
        binder.BindEnum(state, typeof(HelpPageStatusType));

        return SyncValueTask.Run(state.DoStringAsync(chunk, "t"));
    }
}
