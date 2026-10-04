using System.Xml.Linq;
using Moongate.Core.Geometry;
using Moongate.Core.Primitives;
using Moongate.Server.Core.Data.Commands;
using Moongate.Server.Core.Data.Sessions;
using Moongate.Server.Core.Types.Commands;
using Moongate.Server.Services.Sessions;
using Moongate.Server.Ultima.Commands;
using Moongate.Server.Ultima.Data.Targeting;
using Moongate.Server.Ultima.Data.Templates.Gumps;
using Moongate.Server.Ultima.Entities.World;
using Moongate.Server.Ultima.Interfaces;
using Moongate.Server.Ultima.Modules;
using Moongate.Server.Ultima.Services;
using Moongate.Server.Ultima.Types.Targeting;
using Moongate.Tests.Support.Sessions;
using Moongate.Tests.TestSupport.Ultima.Gumps;
using Moongate.Tests.TestSupport.Ultima.Jail;
using Moongate.Tests.TestSupport.Ultima.Loaders;
using Moongate.Tests.TestSupport.Ultima.Movement;
using Moongate.Tests.TestSupport.Ultima.Sectors;
using Moongate.Tests.TestSupport.Ultima.Targeting;
using Moongate.Ultima.Types;

namespace Moongate.Tests.Server.Ultima.Commands;

public sealed class JailCommandTests : IAsyncDisposable
{
    private readonly MobileService _mobiles = new(new StubMovementService(), TestSectors.Create());
    private readonly RecordingGumpService _gumps = new();
    private readonly StubTargetService _targets = new();
    private readonly StubJailService _jail = new();

    private SessionFixture? _fixture;

    [Fact]
    public async Task FromTheConsole_IsRefused()
    {
        _fixture = await SessionFixture.CreateAsync();
        var context = new CommandContext("jail", "jail", [], CommandSourceType.Console, null);

        await new JailCommand(_jail, _targets, _mobiles, _fixture.Loop).ExecuteAsync(context);

        var line = Assert.Single(context.Output);
        Assert.Equal((CommandOutputLevel.Error, "jail works in game only."), (line.Level, line.Text));
        Assert.Equal(0, _targets.Requests);
    }

    [Fact]
    public async Task WithoutTheJailFile_SaysSo_AndAsksNoTarget()
    {
        _jail.IsEnabled = false;

        var context = await RunAsync();

        var line = Assert.Single(context.Output);
        Assert.Equal((CommandOutputLevel.Error, "The jail is not set up: data/jail.toml is missing."), (line.Level, line.Text));
        Assert.Equal(0, _targets.Requests);
    }

    [Fact]
    public async Task ACanceledTarget_SaysCanceled()
    {
        var context = await RunAsync();

        Assert.Equal("Target canceled.", Assert.Single(context.Output).Text);
        Assert.Empty(_gumps.Opened);
    }

    [Theory, InlineData(0x40000001u), InlineData(77u)]
    public async Task AnItemOrAMobileThatIsGone_IsNotACharacter(uint serial)
    {
        _targets.Result = TargetResult.ForObject(new Serial(serial));

        var context = await RunAsync();

        var line = Assert.Single(context.Output);
        Assert.Equal((CommandOutputLevel.Error, "That is not a character."), (line.Level, line.Text));
        Assert.Empty(_gumps.Opened);
    }

    [Fact]
    public async Task TheGround_IsNotACharacter()
    {
        _targets.Result = TargetResult.ForLocation(MapType.Trammel, new Point3D(1600, 1600, 0));

        var context = await RunAsync();

        Assert.Equal("That is not a character.", Assert.Single(context.Output).Text);
    }

    [Fact]
    public async Task AMobile_OpensTheGumpWithItsSerialItsNameAndOneDay()
    {
        _mobiles.EnterWorld(new MobileEntity { Id = new Serial(9), Name = "Gino", Map = MapType.Trammel });
        _targets.Result = TargetResult.ForObject(new Serial(9));

        var context = await RunAsync();

        Assert.Equal("jail:9:Gino:1", Assert.Single(_gumps.Opened).Gump.Layout.Build().Strings[0]);
        Assert.Empty(context.Output);
        Assert.Equal(1, _targets.Requests);
    }

    private async Task<CommandContext> RunAsync()
    {
        _fixture = await SessionFixture.CreateAsync();
        var sessions = new SessionService(_fixture.Loop);
        var session = sessions.GetOrCreate(_fixture.Client);
        await _fixture.ExecuteOnLoopAsync(() => session.Set(SessionKeys.CharacterId, new Serial(2)));
        _mobiles.EnterWorld(new MobileEntity { Id = new Serial(2), Name = "Giachi", Map = MapType.Trammel });
        var context = new CommandContext(".jail", "jail", [], CommandSourceType.InGame, session);

        // The gump shows the arguments it was opened with.
        var templates = new GumpTemplateService(
            _gumps,
            new StubDataLoaderService().With(
                new GumpTemplate
                {
                    Id = JailCommand.GumpId, File = "jail_sentence.xml",
                    Root = XElement.Parse("""<gump id="jail_sentence"><text x="1" y="1">jail:${target}:${name}:${days}</text></gump>""")
                }
            ),
            _fixture.Loop,
            sessions
        );
        var gumps = new GumpModule(sessions, _gumps, templates, new Lazy<IGumpScriptService>(new RecordingGumpScriptService()), _fixture.Loop);

        await new JailCommand(_jail, _targets, _mobiles, _fixture.Loop, null, gumps).ExecuteAsync(context);

        return context;
    }

    public async ValueTask DisposeAsync()
    {
        if (_fixture is not null)
        {
            await _fixture.DisposeAsync();
        }
    }
}
