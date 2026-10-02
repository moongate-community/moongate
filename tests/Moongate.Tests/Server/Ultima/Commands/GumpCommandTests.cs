using System.Xml.Linq;
using Moongate.Server.Core.Data.Commands;
using Moongate.Server.Core.Data.Sessions;
using Moongate.Server.Core.Types.Commands;
using Moongate.Server.Ultima.Commands;
using Moongate.Server.Ultima.Data.Templates.Gumps;
using Moongate.Server.Ultima.Modules;
using Moongate.Server.Ultima.Services;
using Moongate.Tests.TestSupport.Ultima.Gumps;
using Moongate.Tests.TestSupport.Ultima.Loaders;
using Moongate.Tests.TestSupport.Ultima.Speech;

namespace Moongate.Tests.Server.Ultima.Commands;

public sealed class GumpCommandTests : IAsyncLifetime
{
    private readonly RecordingGumpService _gumps = new();

    private BroadcastFixture _fixture = null!;
    private GameSession _session = null!;
    private GumpCommand _command = null!;

    public async Task InitializeAsync()
    {
        _fixture = await BroadcastFixture.CreateAsync();
        _session = await _fixture.AddAsync(7);
        var root = XElement.Parse("""<gump id="hello"><text x="1" y="1">Hi ${name} ${title}</text></gump>""");
        var templates = new GumpTemplateService(
            _gumps,
            new StubDataLoaderService().With(new GumpTemplate { Id = "hello", File = "hello.xml", Root = root }),
            _fixture.Network.Loop,
            _fixture.Sessions
        );
        var module = new GumpModule(_fixture.Sessions, _gumps, templates, new Lazy<Moongate.Server.Ultima.Interfaces.IGumpScriptService>(new RecordingGumpScriptService()), _fixture.Network.Loop);
        _command = new(module, templates, _fixture.Network.Loop);
    }

    [Fact]
    public async Task OpensTheGumpOnYou_WithTheGivenArguments()
    {
        var context = await RunAsync("hello", "name=Aria", "title=the=brave");

        Assert.Empty(context.Output);
        var (session, gump) = Assert.Single(_gumps.Opened);
        Assert.Same(_session, session);
        Assert.Equal("Hi Aria the=brave", gump.Layout.Build().Strings[0]);
    }

    [Fact]
    public async Task AnUnknownGump_SaysSo()
    {
        var context = await RunAsync("nowhere");

        Assert.Equal((CommandOutputLevel.Error, "No gump nowhere in templates/gumps."), (Assert.Single(context.Output).Level, context.Output[0].Text));
        Assert.Empty(_gumps.Opened);
    }

    [Fact]
    public async Task ArgumentNames_AreLowerCased_AsThePlaceholders()
    {
        await RunAsync("hello", "Name=Aria");

        Assert.Equal("Hi Aria ", Assert.Single(_gumps.Opened).Gump.Layout.Build().Strings[0]);
    }

    [Fact]
    public async Task AGumpThatCannotOpen_SaysSo()
    {
        await _fixture.Network.ExecuteOnLoopAsync(() => _fixture.Sessions.Remove(_session.SessionId));

        var context = await RunAsync("hello");

        Assert.Equal((CommandOutputLevel.Error, "Gump hello could not open."), (Assert.Single(context.Output).Level, context.Output[0].Text));
    }

    [Theory, InlineData(), InlineData("hello", "no_equals_sign"), InlineData("hello", "=empty")]
    public async Task BadArguments_ShowTheUsage(params string[] arguments)
    {
        var context = await RunAsync(arguments);

        Assert.Equal("Usage: gump <id> [name=value ...]", Assert.Single(context.Output).Text);
    }

    [Fact]
    public async Task FromTheConsole_IsRefused()
    {
        var context = new CommandContext("gump hello", "gump", ["hello"], CommandSourceType.Console, null);

        await _command.ExecuteAsync(context);

        Assert.Equal(CommandOutputLevel.Error, Assert.Single(context.Output).Level);
    }

    public async Task DisposeAsync()
    {
        await _fixture.DisposeAsync();
    }

    private async Task<CommandContext> RunAsync(params string[] arguments)
    {
        var context = new CommandContext(".gump", "gump", arguments, CommandSourceType.InGame, _session);
        await _command.ExecuteAsync(context);

        return context;
    }
}
