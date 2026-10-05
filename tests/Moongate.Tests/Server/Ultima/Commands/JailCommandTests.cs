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
using Moongate.Ultima.Types;

namespace Moongate.Tests.Server.Ultima.Commands;

public sealed class JailCommandTests : IAsyncDisposable
{
    private readonly MobileService _mobiles = new(new StubMovementService(), TestSectors.Create());
    private readonly RecordingGumpService _gumps = new();
    private readonly StubJailService _jail = new();

    private SessionFixture? _fixture;

    [Fact]
    public async Task FromTheConsole_IsRefused()
    {
        _fixture = await SessionFixture.CreateAsync();
        var context = new CommandContext("jail", "jail", [], CommandSourceType.Console, null);

        await new JailCommand(_jail, _mobiles, _fixture.Loop).ExecuteAsync(context);

        var line = Assert.Single(context.Output);
        Assert.Equal((CommandOutputLevel.Error, "jail works in game only."), (line.Level, line.Text));
    }

    [Fact]
    public async Task WithoutTheJailFile_SaysSo_AndOpensNoGump()
    {
        _jail.IsEnabled = false;

        var context = await RunAsync();

        var line = Assert.Single(context.Output);
        Assert.Equal((CommandOutputLevel.Error, "The jail is not set up: data/jail.toml is missing."), (line.Level, line.Text));
    }

    [Fact]
    public async Task OpensTheGumpAtOnce_WithNoTargetAndOneDay()
    {
        var context = await RunAsync();

        // The target is picked from the gump.
        Assert.Equal("jail:::1", Assert.Single(_gumps.Opened).Gump.Layout.Build().Strings[0]);
        Assert.Empty(context.Output);
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

        await new JailCommand(_jail, _mobiles, _fixture.Loop, null, gumps).ExecuteAsync(context);

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
