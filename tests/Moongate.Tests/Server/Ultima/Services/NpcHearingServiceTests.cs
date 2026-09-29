using Moongate.Core.Geometry;
using Moongate.Core.Primitives;
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
    private readonly MobileEntity _aria = new()
    {
        Id = new Serial(2), Name = "Aria", AccountId = new Serial(0x42), Map = MapType.Trammel,
        Location = new Point3D(1600, 1600, 0)
    };

    [Fact]
    public void Heard_CallsOnSpeechOfTheNpcsWithAScriptWithin15Cells()
    {
        Add(0x100, "orc", 1615, 1600);

        Create().Heard(_aria, "hello");

        var call = Assert.Single(_engine.MemberCalls);
        Assert.Equal(("wander", "on_speech"), (call.Table, call.Function));
        Assert.Equal([0x100L, 2L, "hello"], call.Args);
    }

    [Fact]
    public void Heard_SkipsFarOtherMapScriptlessNpcsAndPlayers()
    {
        Add(0x101, "orc", 1616, 1600);
        Add(0x102, "orc", 1600, 1600, MapType.Felucca);
        Add(0x103, "rabbit", 1601, 1600);
        _sectors.Add(new MobileEntity
            {
                Id = new Serial(3), Name = "Bob", AccountId = new Serial(0x43), TemplateId = "orc", Map = MapType.Trammel,
                Location = new Point3D(1601, 1600, 0)
            }
        );

        Create().Heard(_aria, "hello");

        Assert.Empty(_engine.MemberCalls);
    }

    private void Add(uint serial, string template, int x, int y, MapType map = MapType.Trammel)
    {
        _sectors.Add(new MobileEntity
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

        return new(_engine, templates, _sectors);
    }
}
