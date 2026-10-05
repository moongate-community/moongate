using System.Xml.Linq;
using Moongate.Core.Geometry;
using Moongate.Core.Primitives;
using Moongate.Server.Core.Data.Commands;
using Moongate.Server.Core.Data.Sessions;
using Moongate.Server.Core.Interfaces.Services;
using Moongate.Server.Core.Types.Commands;
using Moongate.Server.Services.Sessions;
using Moongate.Server.Ultima.Commands;
using Moongate.Server.Ultima.Data.Templates.Gumps;
using Moongate.Server.Ultima.Entities.World;
using Moongate.Server.Ultima.Interfaces;
using Moongate.Server.Ultima.Modules;
using Moongate.Server.Ultima.Services;
using Moongate.Tests.Support.Sessions;
using Moongate.Tests.TestSupport.Localization;
using Moongate.Tests.TestSupport.Ultima.Gumps;
using Moongate.Tests.TestSupport.Ultima.Loaders;
using Moongate.Tests.TestSupport.Ultima.Movement;
using Moongate.Tests.TestSupport.Ultima.Sectors;
using Moongate.Ultima.Types;

namespace Moongate.Tests.Server.Ultima.Commands;

public sealed class GmToolsCommandTests : IAsyncDisposable
{
    private readonly MobileService _mobiles = new(new StubMovementService(), TestSectors.Create());
    private readonly RecordingGumpService _gumps = new();

    private SessionFixture? _fixture;

    [Fact]
    public async Task OpensTheGumpOfTheTools()
    {
        var context = await RunAsync();

        Assert.Empty(context.Output);
        Assert.Equal("tools", Assert.Single(_gumps.Opened).Gump.Layout.Build().Strings[0]);
    }

    [Fact]
    public async Task WithoutTheGumpTemplate_SaysItIsMissing()
    {
        var context = await RunAsync(withTemplate: false);

        Assert.Equal(
            (CommandOutputLevel.Error, "The gmtools gump is missing: templates/gumps/gmtools.xml."),
            (Assert.Single(context.Output).Level, context.Output[0].Text)
        );
        Assert.Empty(_gumps.Opened);
    }

    [Fact]
    public async Task TheTextIsInTheServerLanguage()
    {
        var context = await RunAsync(
            withTemplate: false,
            localization: TestLocalization.With((30186, "Manca il gump gmtools: templates/gumps/gmtools.xml."))
        );

        Assert.Equal("Manca il gump gmtools: templates/gumps/gmtools.xml.", Assert.Single(context.Output).Text);
    }

    [Fact]
    public async Task FromTheConsole_IsRefused()
    {
        _fixture = await SessionFixture.CreateAsync();
        var context = new CommandContext("gmtools", "gmtools", [], CommandSourceType.Console, null);

        await new GmToolsCommand(_mobiles, _fixture.Loop).ExecuteAsync(context);

        Assert.Equal(CommandOutputLevel.Error, Assert.Single(context.Output).Level);
        Assert.Empty(_gumps.Opened);
    }

    private async Task<CommandContext> RunAsync(bool withTemplate = true, ILocalizationService? localization = null)
    {
        _fixture = await SessionFixture.CreateAsync();
        var sessions = new SessionService(_fixture.Loop);
        var session = sessions.GetOrCreate(_fixture.Client);
        await _fixture.ExecuteOnLoopAsync(() => session.Set(SessionKeys.CharacterId, new Serial(2)));
        _mobiles.EnterWorld(new MobileEntity { Id = new Serial(2), Name = "Aria", Map = MapType.Trammel, Location = new Point3D(1600, 1600, 0) });
        var context = new CommandContext(".gmtools", "gmtools", [], CommandSourceType.InGame, session);
        var templates = new GumpTemplateService(
            _gumps,
            withTemplate
                ? new StubDataLoaderService().With(
                    new GumpTemplate { Id = "gmtools", File = "gmtools.xml", Root = XElement.Parse("""<gump id="gmtools"><text x="1" y="1">tools</text></gump>""") }
                )
                : new StubDataLoaderService().With<GumpTemplate>(),
            _fixture.Loop,
            sessions
        );
        var gumps = new GumpModule(sessions, _gumps, templates, new Lazy<IGumpScriptService>(new RecordingGumpScriptService()), _fixture.Loop);

        await new GmToolsCommand(_mobiles, _fixture.Loop, localization, gumps).ExecuteAsync(context);

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
