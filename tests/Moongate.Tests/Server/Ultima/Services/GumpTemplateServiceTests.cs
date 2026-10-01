using System.Xml.Linq;
using Moongate.Server.Core.Data.Sessions;
using Moongate.Server.Ultima.Data.Gumps;
using Moongate.Server.Ultima.Data.Templates.Gumps;
using Moongate.Server.Ultima.Services;
using Moongate.Server.Ultima.Types.Gumps;
using Moongate.Tests.TestSupport.Ultima.Gumps;
using Moongate.Tests.TestSupport.Ultima.Loaders;
using Moongate.Tests.TestSupport.Ultima.Speech;

namespace Moongate.Tests.Server.Ultima.Services;

public sealed class GumpTemplateServiceTests : IAsyncLifetime
{
    private readonly RecordingGumpService _gumps = new();

    private BroadcastFixture _fixture = null!;
    private GameSession _session = null!;
    private GumpTemplateService _templates = null!;

    public async Task InitializeAsync()
    {
        _fixture = await BroadcastFixture.CreateAsync();
        _session = await _fixture.AddAsync(7);
        var root = XElement.Parse(
            """
            <gump id="confirm">
              <text x="1" y="1">Sure, ${name}?</text>
              <button x="1" y="1" up="1" down="2" on_click="yes" />
              <button x="1" y="1" up="1" down="2" id="5" />
            </gump>
            """
        );
        _templates = new(
            _gumps,
            new StubDataLoaderService().With(new GumpTemplate { Id = "confirm", File = "confirm.xml", Root = root }),
            _fixture.Network.Loop,
            _fixture.Sessions
        );
    }

    [Fact]
    public void Exists_TellsWhetherTheGumpIsInTemplates()
    {
        Assert.True(_templates.Exists("confirm"));
        Assert.False(_templates.Exists("other"));
    }

    [Fact]
    public void Open_FillsTheGump_AndTellsWhichOnClickWasPressed()
    {
        var answers = new List<GumpTemplateAnswer>();

        Assert.True(_templates.Open(_session, "confirm", new Dictionary<string, string> { ["name"] = "Aria" }, (_, answer) => answers.Add(answer)));

        var gump = Assert.Single(_gumps.Opened).Gump;
        Assert.Equal("Sure, Aria?", gump.Layout.Build().Strings[0]);
        gump.OnResponse(_session, Response(1));
        gump.OnResponse(_session, Response(5));
        Assert.Equal([("yes", 1), (null, 5)], answers.Select(answer => (answer.Click, answer.Response.ButtonId)));
    }

    [Fact]
    public void Open_AnUnknownGump_IsFalse()
    {
        Assert.False(_templates.Open(_session, "other", new Dictionary<string, string>(), (_, _) => { }));
        Assert.Empty(_gumps.Opened);
    }

    [Fact]
    public async Task AskAsync_CompletesWithTheOnClickPressed()
    {
        var asking = _templates.AskAsync(_session, "confirm", new Dictionary<string, string>());
        await WaitForOpenAsync();

        await _fixture.Network.ExecuteOnLoopAsync(() => _gumps.Opened[0].Gump.OnResponse(_session, Response(1)));

        Assert.Equal("yes", await asking);
    }

    [Fact]
    public async Task AskAsync_ClosedOrAnsweredWithoutAnOnClick_CompletesWithNull()
    {
        var closed = _templates.AskAsync(_session, "confirm", new Dictionary<string, string>());
        await WaitForOpenAsync();
        await _fixture.Network.ExecuteOnLoopAsync(() => _gumps.Opened[0].Gump.OnClosed!(_session, GumpCloseReasonType.Disconnect));

        var other = _templates.AskAsync(_session, "confirm", new Dictionary<string, string>());
        await WaitForOpenAsync(2);
        await _fixture.Network.ExecuteOnLoopAsync(() => _gumps.Opened[1].Gump.OnResponse(_session, Response(0)));

        Assert.Null(await closed);
        Assert.Null(await other);
        Assert.Null(await _templates.AskAsync(_session, "unknown", new Dictionary<string, string>()));
    }

    [Fact]
    public async Task AskAsync_ForASessionAlreadyGone_CompletesWithNullAndOpensNothing()
    {
        await _fixture.Network.ExecuteOnLoopAsync(() => _fixture.Sessions.Remove(_session.SessionId));

        Assert.Null(await _templates.AskAsync(_session, "confirm", new Dictionary<string, string>()));
        Assert.Empty(_gumps.Opened);
    }

    public async Task DisposeAsync()
    {
        await _fixture.DisposeAsync();
    }

    private async Task WaitForOpenAsync(int count = 1)
    {
        for (var tries = 0; tries < 200 && _gumps.Opened.Count < count; tries++)
        {
            await Task.Delay(5);
        }
    }

    private static GumpResponse Response(int button)
    {
        return new() { ButtonId = button, Switches = new HashSet<int>(), Texts = new Dictionary<int, string>() };
    }
}
