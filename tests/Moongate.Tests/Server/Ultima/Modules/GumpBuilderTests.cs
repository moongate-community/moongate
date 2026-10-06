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
using Moongate.Tests.TestSupport.Ultima.Gumps;
using Moongate.Tests.TestSupport.Ultima.Loaders;
using Moongate.Tests.TestSupport.Ultima.Speech;

namespace Moongate.Tests.Server.Ultima.Modules;

public sealed class GumpBuilderTests : IAsyncLifetime
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
    public void Send_OpensTheBuiltGump_WithTheSameControlsAsTheXml()
    {
        var result = Run(
            """
            local g = gump.create("menu", 50, 60)
            g:background{ x = 0, y = 0, gump = 9200, width = 300, height = 200 }
             :text{ x = 20, y = 20, hue = 1152, text = "Hello ${name}" }
             :html{ x = 20, y = 40, width = 100, height = 20, cliloc = 1011011 }
             :text_entry{ x = 20, y = 60, width = 100, height = 20, entry = 1, bind = "nick", text = "nick" }
            g:page()
            g:checkbox{ x = 1, y = 2, off = 210, on = 211, switch = 5, checked = true }
            return gump.send(7, g, { name = "Aria" })
            """
        );

        Assert.True(result[0].Read<bool>());
        var gump = Assert.Single(_gumps.Opened).Gump;
        Assert.Equal(("menu", 50, 60), (gump.Id, gump.X, gump.Y));
        Assert.Equal(
            "{ page 0 }{ resizepic 0 0 9200 300 200 }{ text 20 20 1152 0 }{ xmfhtmlgump 20 40 100 20 1011011 0 0 }" +
            "{ textentry 20 60 100 20 0 1 1 }{ page 1 }{ checkbox 1 2 210 211 1 5 }",
            gump.Layout.Build().Layout
        );
        Assert.Equal(["Hello Aria", "nick"], gump.Layout.Build().Strings);
    }

    [Fact]
    public void AButtonWithAFunction_CallsIt_WithTheAnswerAndTheArgs()
    {
        _scripts.CurrentScript = "items/potion.lua";
        Run(
            """
            local g = gump.create("menu")
            g:button{ x = 1, y = 1, up = 4005, down = 4007, on_click = function(player, response, args) end }
            g:button{ x = 1, y = 1, up = 4005, down = 4007, on_click = "named" }
            gump.send(7, g, { name = "Aria" })
            """
        );

        Answer(1);
        Answer(2, gump: 0);

        var (owner, _, args) = Assert.Single(_scripts.FunctionCalls);
        Assert.Equal(("items/potion.lua", 7L), (owner, args[0]));
        Assert.Equal("Aria", Assert.IsType<LuaTable>(args[2])["name"].Read<string>());
        Assert.Equal("named", Assert.Single(_scripts.Calls).Function);
    }

    [Fact]
    public void Radios_FollowTheirGroup()
    {
        Run(
            """
            local g = gump.create("menu")
            g:group()
            g:radio{ x = 1, y = 1, off = 208, on = 209, switch = 1 }
            g:radio{ x = 1, y = 1, off = 208, on = 209, switch = 2 }
            g:radio{ x = 1, y = 1, off = 208, on = 209, switch = 3 }
            gump.send(7, g)
            """
        );

        Assert.Equal(
            "{ page 0 }{ group 1 }{ radio 1 1 208 209 0 1 }{ radio 1 1 208 209 0 2 }{ radio 1 1 208 209 0 3 }",
            _gumps.Opened[0].Gump.Layout.Build().Layout
        );
    }

    [Fact]
    public void Paginate_MakesPagesAndTheButtonsBetweenThem()
    {
        Run(
            """
            local g = gump.create("list")
            g:pager{ previous = { x = 10, y = 90 }, next = { x = 90, y = 90 } }
            for i = 1, 3 do
                local row = g:paginate(i, 2)
                g:text{ x = 20, y = 20 + row * 20, text = "item " .. i }
            end
            gump.send(7, g)
            """
        );

        Assert.Equal(
            "{ page 0 }{ page 1 }{ text 20 20 0 0 }{ text 20 40 0 1 }{ button 90 90 4005 4007 0 2 0 }" +
            "{ page 2 }{ button 10 90 4014 4015 0 1 0 }{ text 20 20 0 2 }",
            _gumps.Opened[0].Gump.Layout.Build().Layout
        );
    }

    [Fact]
    public void ASlot_IsFilledByItsScriptFunction_AtTheSlotsPlace()
    {
        _scripts.OnCall = (_, function, args) =>
        {
            if (function == "rows")
            {
                var builder = Assert.IsType<LuaTable>(args[0]);
                var entries = builder["__entries"].Read<LuaTable>();
                var row = new LuaTable();
                row["__kind"] = "text";
                row["x"] = 5;
                row["y"] = 10;
                row["text"] = "row";
                entries[entries.ArrayLength + 1] = row;
            }
        };

        Run("gump.open(7, 'with_slot', {})");

        Assert.Equal(
            ("with_slot", "rows", 7L),
            (_scripts.Calls[0].Gump, _scripts.Calls[0].Function, _scripts.Calls[0].Args[1])
        );
        Assert.Equal("{ page 0 }{ text 1 1 0 0 }{ text 105 210 0 1 }", _gumps.Opened[0].Gump.Layout.Build().Layout);
    }

    [Fact]
    public void TheBuildersMethods_CannotBeReachedOrChangedByScripts()
    {
        var result = Run("return getmetatable(gump.create('menu'))");

        Assert.Equal(LuaValueType.String, result[0].Type);
    }

    [Theory,
     InlineData("g:checkbox{ x = 1, y = 1, off = 1, on = 2, switch = 1, checked = 'yes' }", "checked"),
     InlineData("g:button{ x = 1, y = 1, up = 1, down = 2, page = 1, on_click = 'a' }", "exactly one"),
     InlineData("g:button{ x = 1, y = 1, up = 1, down = 2, open = 'nowhere' }", "nowhere"),
     InlineData("g:item_property{}", "serial"),
     InlineData("g:button{ x = 1, y = 1, up = 1, down = 2, on_click = '__reserved' }", "reserved")]
    public void Send_ABuiltGumpTheChecksRefuse_FailsWithTheReason(string control, string expected)
    {
        var exception =
            Assert.ThrowsAny<Exception>(() => Run($"local g = gump.create('menu') {control} return gump.send(7, g)"));

        Assert.Contains(expected, exception.Message);
        Assert.Empty(_gumps.Opened);
    }

    [Fact]
    public void Create_AnIdThatIsNotAName_Fails()
    {
        Assert.ThrowsAny<Exception>(() => Run("gump.create('Bad Id')"));
    }

    [Fact]
    public void ASlotFunctionThatFails_OpensNothing()
    {
        _scripts.CallResult =
            Moongate.Scripting.Data.Scripts.ScriptResult.Failed(new("gumps/with_slot.lua", 1, "boom", null));

        Assert.False(Run("return gump.open(7, 'with_slot', {})")[0].Read<bool>());
        Assert.Empty(_gumps.Opened);
    }

    public async Task DisposeAsync()
    {
        await _fixture.DisposeAsync();
    }

    private void Answer(int button, int gump = 0)
    {
        _gumps.Opened[gump]
            .Gump.OnResponse(
                _session,
                new GumpResponse { ButtonId = button, Switches = new HashSet<int>(), Texts = new Dictionary<int, string>() }
            );
    }

    private LuaValue[] Run(string chunk)
    {
        var withSlot = XElement.Parse(
            """
            <gump id="with_slot">
              <text x="1" y="1">top</text>
              <slot name="rows" x="100" y="200" />
            </gump>
            """
        );
        var data = new StubDataLoaderService().With(new GumpTemplate { Id = "with_slot", File = "a.xml", Root = withSlot });
        var templates = new GumpTemplateService(_gumps, data, _fixture.Network.Loop, _fixture.Sessions);
        var module = new GumpModule(
            _fixture.Sessions,
            _gumps,
            templates,
            new Lazy<Moongate.Server.Ultima.Interfaces.IGumpScriptService>(_scripts),
            _fixture.Network.Loop
        );

        using var state = LuaState.Create();
        state.OpenBasicLibrary();
        state.OpenStringLibrary();
        new LuaModuleBinder(NoThreadGuard.Instance).Bind(state, module);

        return SyncValueTask.Run(state.DoStringAsync(chunk, "t"));
    }
}
