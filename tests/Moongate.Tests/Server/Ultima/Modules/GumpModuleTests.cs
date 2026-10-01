using System.Xml.Linq;
using Lua;
using Lua.Standard;
using Moongate.Scripting.Binding;
using Moongate.Scripting.Internal;
using Moongate.Scripting.Utils;
using Moongate.Server.Core.Data.Sessions;
using Moongate.Server.Ultima.Data.Gumps;
using Moongate.Server.Ultima.Data.Templates.Gumps;
using Moongate.Server.Ultima.Modules;
using Moongate.Server.Ultima.Services;
using Moongate.Server.Ultima.Types.Gumps;
using Moongate.Tests.TestSupport.Ultima.Gumps;
using Moongate.Tests.TestSupport.Ultima.Loaders;
using Moongate.Tests.TestSupport.Ultima.Speech;

namespace Moongate.Tests.Server.Ultima.Modules;

public sealed class GumpModuleTests : IAsyncLifetime
{
    private readonly RecordingGumpService _gumps = new();
    private readonly RecordingGumpScriptService _scripts = new();

    private BroadcastFixture _fixture = null!;
    private GameSession _session = null!;

    public async Task InitializeAsync()
    {
        _fixture = await BroadcastFixture.CreateAsync();
        _session = await _fixture.AddAsync(7);
    }

    [Fact]
    public void Open_SendsTheGumpFilledWithTheArguments()
    {
        var result = Run("return gump.open(7, 'release_pet', { pet_name = 'Fido', count = 3, tame = true })");

        Assert.True(result[0].Read<bool>());
        var (session, gump) = Assert.Single(_gumps.Opened);
        Assert.Same(_session, session);
        Assert.Equal(("release_pet", 100, 50), (gump.Id, gump.X, gump.Y));
        Assert.Equal("Release Fido 3 true", gump.Layout.Build().Strings[0]);
    }

    [Theory,
     InlineData("return gump.open(99, 'release_pet')"),
     InlineData("return gump.open(7, 'no_such_gump')")]
    public void Open_AnUnknownPlayerOrGump_IsFalse(string chunk)
    {
        Assert.False(Run(chunk)[0].Read<bool>());
        Assert.Empty(_gumps.Opened);
    }

    [Fact]
    public void Close_ClosesThePlayersGump()
    {
        Assert.True(Run("return gump.close(7, 'release_pet')")[0].Read<bool>());

        Assert.Equal([(_session, "release_pet")], _gumps.Closed);
    }

    [Fact]
    public void AnOnClickButton_CallsItsFunction_WithTheAnswerAndTheArguments()
    {
        Run("gump.open(7, 'release_pet', { pet_name = 'Fido' })");

        Answer(1, switches: [10], texts: new() { [5] = "hi" });

        var (gump, function, args) = Assert.Single(_scripts.Calls);
        Assert.Equal(("release_pet", "release", 7L), (gump, function, args[0]));
        var response = Assert.IsType<LuaTable>(args[1]);
        Assert.Equal(1d, response["button"].Read<double>());
        Assert.True(response["switches"].Read<LuaTable>()[10].Read<bool>());
        Assert.Equal("hi", response["text"].Read<LuaTable>()[5].Read<string>());
        Assert.Equal("Fido", Assert.IsType<LuaTable>(args[2])["pet_name"].Read<string>());
    }

    [Fact]
    public void AnIdButton_CallsOnButton()
    {
        Run("gump.open(7, 'release_pet')");

        Answer(4);

        var (_, function, args) = Assert.Single(_scripts.Calls);
        Assert.Equal(("on_button", 7L, 4), (function, args[0], args[1]));
    }

    [Fact]
    public void ClosingIt_CallsOnCloseForThePlayer()
    {
        Run("gump.open(7, 'release_pet')");

        Answer(0);

        var (_, function, args) = Assert.Single(_scripts.Calls);
        Assert.Equal(("on_close", 7L, "player"), (function, args[0], args[2]));
    }

    [Theory,
     InlineData(GumpCloseReasonType.Replaced, "replaced"),
     InlineData(GumpCloseReasonType.Server, "server"),
     InlineData(GumpCloseReasonType.Disconnect, "disconnect")]
    public void AServerClose_CallsOnCloseWithTheReason(GumpCloseReasonType reason, string expected)
    {
        Run("gump.open(7, 'release_pet')");

        _gumps.Opened[0].Gump.OnClosed!(_session, reason);

        var (_, function, args) = Assert.Single(_scripts.Calls);
        Assert.Equal(("on_close", expected), (function, args[2]));
    }

    [Fact]
    public void AnOpenButton_WritesTheBoundValuesIntoArgs_AndOpensTheNextGumpWithThem()
    {
        Run("gump.open(7, 'ask_name', { greeting = 'Hi' })");

        _gumps.Opened[0].Gump.OnResponse(
            _session,
            new GumpResponse { ButtonId = 1, Switches = new HashSet<int>(), Texts = new Dictionary<int, string> { [1] = "Aria" } }
        );

        Assert.Equal(2, _gumps.Opened.Count);
        Assert.Equal("Hi Aria", _gumps.Opened[1].Gump.Layout.Build().Strings[0]);
        Assert.Empty(_scripts.Calls);
        _gumps.Opened[1].Gump.OnClosed!(_session, GumpCloseReasonType.Disconnect);
        Assert.Equal("Aria", Assert.IsType<LuaTable>(Assert.Single(_scripts.Calls).Args[1])["name"].Read<string>());
    }

    public async Task DisposeAsync()
    {
        await _fixture.DisposeAsync();
    }

    private void Answer(int button, int[]? switches = null, Dictionary<int, string>? texts = null)
    {
        _gumps.Opened[0].Gump.OnResponse(
            _session,
            new GumpResponse { ButtonId = button, Switches = (switches ?? []).ToHashSet(), Texts = texts ?? [] }
        );
    }

    private LuaValue[] Run(string chunk)
    {
        var root = XElement.Parse(
            """
            <gump id="release_pet" x="100" y="50">
              <text x="1" y="1">Release ${pet_name} ${count} ${tame}</text>
              <button x="1" y="1" up="1" down="2" on_click="release" />
              <button x="1" y="1" up="1" down="2" id="4" />
              <checkbox x="1" y="1" off="1" on="2" switch="10" />
              <text_entry x="1" y="1" width="1" height="1" entry="5" />
            </gump>
            """
        );
        var askName = XElement.Parse(
            """
            <gump id="ask_name">
              <text_entry x="1" y="1" width="1" height="1" entry="1" bind="name" />
              <button x="1" y="1" up="1" down="2" open="greet" />
            </gump>
            """
        );
        var greet = XElement.Parse("""<gump id="greet"><text x="1" y="1">${greeting} ${name}</text></gump>""");
        var data = new StubDataLoaderService().With(
            new GumpTemplate { Id = "release_pet", File = "a.xml", Root = root },
            new GumpTemplate { Id = "ask_name", File = "b.xml", Root = askName },
            new GumpTemplate { Id = "greet", File = "c.xml", Root = greet }
        );
        var templates = new GumpTemplateService(_gumps, data, _fixture.Network.Loop, _fixture.Sessions);
        var module = new GumpModule(_fixture.Sessions, _gumps, templates, new Lazy<Moongate.Server.Ultima.Interfaces.IGumpScriptService>(_scripts));

        using var state = LuaState.Create();
        state.OpenBasicLibrary();
        new LuaModuleBinder(NoThreadGuard.Instance).Bind(state, module);

        return SyncValueTask.Run(state.DoStringAsync(chunk, "t"));
    }
}
