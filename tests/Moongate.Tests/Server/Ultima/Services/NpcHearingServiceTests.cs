using Lua;
using Moongate.Core.Geometry;
using Moongate.Core.Primitives;
using Moongate.Scripting.Data.Config;
using Moongate.Server.Ultima.Data.Templates.Mobiles;
using Moongate.Server.Ultima.Entities.World;
using Moongate.Server.Ultima.Services;
using Moongate.Tests.TestSupport.Scripting;
using Moongate.Tests.TestSupport.Ultima.Loaders;
using Moongate.Tests.TestSupport.Ultima.Sectors;
using Moongate.Ultima.Types;

namespace Moongate.Tests.Server.Ultima.Services;

public sealed class NpcHearingServiceTests
{
    private readonly FakeScriptEngine _engine = new();
    private readonly SectorService _sectors = TestSectors.Create();
    private NpcScriptService _scripts = null!;

    private readonly MobileEntity _aria = new()
    {
        Id = new Serial(2), Name = "Aria", AccountId = new Serial(0x42), Map = MapType.Trammel,
        Location = new Point3D(1600, 1600, 0)
    };

    [Fact]
    public async Task Heard_CallsOnSpeechOfTheNpcsWithAScriptWithin15Cells()
    {
        Add(0x100, "orc", 1615, 1600);
        var hearing = Create();
        await _scripts.StartAsync();

        hearing.Heard(_aria, "hello", [0x002, 0x034]);

        var call = Assert.Single(_engine.MemberCalls);
        Assert.Equal(("mobiles/wander.lua", "wander", "on_speech"), (call.Owner, call.Table, call.Function));
        Assert.Equal([0x100L, 2L, "hello"], call.Args.Take(3));
        var keywords = Assert.IsType<LuaTable>(call.Args[3]);
        Assert.Equal((2, 2L, 0x34L), (keywords.ArrayLength, keywords[1].Read<long>(), keywords[2].Read<long>()));
    }

    [Fact]
    public async Task Heard_GivesEachNpcItsOwnKeywordsTable()
    {
        Add(0x100, "orc", 1615, 1600);
        Add(0x101, "orc", 1610, 1600);
        var hearing = Create();
        await _scripts.StartAsync();

        hearing.Heard(_aria, "bank", [0x002]);

        Assert.Equal(2, _engine.MemberCalls.Count);
        Assert.NotSame(_engine.MemberCalls[0].Args[3], _engine.MemberCalls[1].Args[3]);
    }

    [Fact]
    public async Task Heard_SkipsFarOtherMapScriptlessNpcsAndPlayers()
    {
        Add(0x101, "orc", 1616, 1600);
        Add(0x102, "orc", 1600, 1600, MapType.Felucca);
        Add(0x103, "rabbit", 1601, 1600);
        _sectors.Add(
            new MobileEntity
            {
                Id = new Serial(3), Name = "Bob", AccountId = new Serial(0x43), TemplateId = "orc", Map = MapType.Trammel,
                Location = new Point3D(1601, 1600, 0)
            }
        );

        var hearing = Create();
        await _scripts.StartAsync();

        hearing.Heard(_aria, "hello");

        Assert.Empty(_engine.MemberCalls);
    }

    [Fact]
    public async Task Heard_AfterTheScriptsStopped_CallsNothing()
    {
        Add(0x100, "orc", 1601, 1600);
        var hearing = Create();
        await _scripts.StopAsync();

        hearing.Heard(_aria, "hello");

        Assert.Empty(_engine.MemberCalls);
    }

    private void Add(uint serial, string template, int x, int y, MapType map = MapType.Trammel)
    {
        _sectors.Add(
            new MobileEntity
            {
                Id = new Serial(serial), Name = "npc", TemplateId = template, Map = map, Location = new Point3D(x, y, 0)
            }
        );
    }

    private NpcHearingService Create()
    {
        var templates = new MobileTemplateService(
            new StubDataLoaderService().With(
                new MobileTemplate { Id = "orc", ScriptId = "wander" },
                new MobileTemplate { Id = "rabbit" }
            )
        );

        _scripts = new NpcScriptService(
            _engine,
            templates,
            new StubGameLoop(),
            new ScriptEngineOptions { ScriptsDirectory = "unused" }
        );

        return new(_scripts, _sectors);
    }
}
