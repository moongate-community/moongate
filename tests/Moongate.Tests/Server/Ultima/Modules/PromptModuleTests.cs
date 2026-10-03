using Lua;
using Lua.Standard;
using Moongate.Scripting.Binding;
using Moongate.Scripting.Interfaces;
using Moongate.Scripting.Internal;
using Moongate.Scripting.Utils;
using Moongate.Server.Ultima.Modules;
using Moongate.Tests.TestSupport.Scripting;
using Moongate.Tests.TestSupport.Ultima.Prompts;
using Moongate.Tests.TestSupport.Ultima.Speech;

namespace Moongate.Tests.Server.Ultima.Modules;

public sealed class PromptModuleTests : IAsyncLifetime
{
    private readonly StubPromptService _prompts = new();
    private readonly FakeScriptEngine _engine = new() { CurrentScript = "items/rune.lua" };
    private readonly StubGameLoop _loop = new() { DeferTryPost = true };

    private BroadcastFixture _fixture = null!;

    public async Task InitializeAsync()
    {
        _fixture = await BroadcastFixture.CreateAsync();
        await _fixture.AddAsync(2);
    }

    public async Task DisposeAsync()
    {
        await _fixture.DisposeAsync();
    }

    [Fact]
    public void Ask_GivesTheTypedTextToTheFunction_OwnedByTheScriptThatAsked()
    {
        _prompts.Answer = "Vesper";

        Assert.True(Run("return prompt.ask(2, function(text) end)")[0].Read<bool>());

        var call = Assert.Single(_engine.FunctionCalls);
        Assert.Equal("items/rune.lua", call.Owner);
        Assert.Equal("Vesper", Assert.IsType<string>(Assert.Single(call.Args)));
    }

    [Fact]
    public void Ask_Canceled_GivesNilToTheFunction()
    {
        Run("prompt.ask(2, function(text) end)");

        Assert.Null(Assert.Single(Assert.Single(_engine.FunctionCalls).Args));
    }

    [Fact]
    public void AnAnswerThatComesWhileAScriptRuns_WaitsForTheNextTurnOfTheLoop()
    {
        _engine.IsRunningScript = true;

        Run("prompt.ask(2, function(text) end)");

        Assert.Empty(_engine.FunctionCalls);
        Assert.Equal(1, _loop.PostedWorkItems);
    }

    [Fact]
    public void AnAnswerThatComesOffTheGameLoop_AsWhenASessionClosesAtShutdown_IsPostedToIt()
    {
        _loop.IsOnLoopThread = false;

        Run("prompt.ask(2, function(text) end)");

        Assert.Empty(_engine.FunctionCalls);
        Assert.Equal(1, _loop.PostedWorkItems);
    }

    [Theory, InlineData("return prompt.ask(999, function() end)"), InlineData("return prompt.cancel(-1)")]
    public void APlayerNotInTheWorld_IsFalse_AndNothingIsAsked(string chunk)
    {
        Assert.False(Run(chunk)[0].Read<bool>());

        Assert.Empty(_engine.FunctionCalls);
        Assert.Equal(0, _prompts.Cancels);
    }

    [Fact]
    public void Ask_WithoutAFunction_IsAnArgumentError()
    {
        Assert.ThrowsAny<Exception>(() => Run("return prompt.ask(2, 5)"));
    }

    [Fact]
    public void Cancel_APlayerInTheWorld_IsTrue()
    {
        Assert.True(Run("return prompt.cancel(2)")[0].Read<bool>());
        Assert.Equal(1, _prompts.Cancels);
    }

    private LuaValue[] Run(string chunk)
    {
        using var state = LuaState.Create();
        state.OpenBasicLibrary();
        new LuaModuleBinder(NoThreadGuard.Instance).Bind(
            state,
            new PromptModule(_prompts, _fixture.Sessions, new Lazy<IScriptEngine>(() => _engine), _loop)
        );

        return SyncValueTask.Run(state.DoStringAsync(chunk, "t"));
    }
}
