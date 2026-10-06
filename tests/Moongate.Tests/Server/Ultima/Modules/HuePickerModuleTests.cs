using Lua;
using Lua.Standard;
using Moongate.Scripting.Binding;
using Moongate.Scripting.Interfaces;
using Moongate.Scripting.Internal;
using Moongate.Scripting.Utils;
using Moongate.Server.Ultima.Modules;
using Moongate.Tests.TestSupport.Scripting;
using Moongate.Tests.TestSupport.Ultima.HuePicking;
using Moongate.Tests.TestSupport.Ultima.Speech;

namespace Moongate.Tests.Server.Ultima.Modules;

public sealed class HuePickerModuleTests : IAsyncLifetime
{
    private readonly StubHuePickerService _pickers = new();
    private readonly FakeScriptEngine _engine = new() { CurrentScript = "items/dyes.lua" };
    private readonly StubGameLoop _loop = new() { DeferTryPost = true };

    private BroadcastFixture _fixture = null!;

    public async Task InitializeAsync()
    {
        _fixture = await BroadcastFixture.CreateAsync();
        await _fixture.AddAsync(2);
    }

    [Fact]
    public void Open_ShowsTheGraphic_AndGivesTheHuePickedToTheFunction()
    {
        _pickers.Result = 0x0026;

        Assert.True(Run("return hue_picker.open(2, 0x0FAB, function(hue) end)")[0].Read<bool>());

        Assert.Equal([0x0FAB], _pickers.Graphics);
        var call = Assert.Single(_engine.FunctionCalls);
        Assert.Equal("items/dyes.lua", call.Owner);
        Assert.Equal(0x0026, Assert.IsType<int>(Assert.Single(call.Args)));
    }

    [Fact]
    public void Open_APickerThatEndsWithoutAnAnswer_GivesNilToTheFunction()
    {
        _pickers.Result = null;

        Run("hue_picker.open(2, 0x0FAB, function(hue) end)");

        Assert.Null(Assert.Single(Assert.Single(_engine.FunctionCalls).Args));
    }

    [Fact]
    public void AnAnswerThatComesWhileAScriptRuns_WaitsForTheNextTurnOfTheLoop()
    {
        // As when a script opens a second picker: the first one ends inside that script.
        _engine.IsRunningScript = true;

        Run("hue_picker.open(2, 0x0FAB, function(hue) end)");

        Assert.Empty(_engine.FunctionCalls);
        Assert.Equal(1, _loop.PostedWorkItems);
    }

    [Theory,
     InlineData("return hue_picker.open(999, 0x0FAB, function() end)"),
     InlineData("return hue_picker.open(-1, 0x0FAB, function() end)"),
     InlineData("return hue_picker.open(2, -1, function() end)"),
     InlineData("return hue_picker.open(2, 0x10000, function() end)")]
    public void APlayerNotInTheWorld_OrAGraphicOutOfRange_IsFalse_AndNothingIsShown(string chunk)
    {
        Assert.False(Run(chunk)[0].Read<bool>());

        Assert.Empty(_pickers.Graphics);
        Assert.Empty(_engine.FunctionCalls);
    }

    [Fact]
    public void Open_WithoutAFunction_IsAnArgumentError()
    {
        Assert.ThrowsAny<Exception>(() => Run("return hue_picker.open(2, 0x0FAB, 5)"));
    }

    private LuaValue[] Run(string chunk)
    {
        using var state = LuaState.Create();
        state.OpenBasicLibrary();
        new LuaModuleBinder(NoThreadGuard.Instance).Bind(
            state,
            new HuePickerModule(_pickers, _fixture.Sessions, new Lazy<IScriptEngine>(() => _engine), _loop)
        );

        return SyncValueTask.Run(state.DoStringAsync(chunk, "t"));
    }

    public async Task DisposeAsync()
    {
        await _fixture.DisposeAsync();
    }
}
