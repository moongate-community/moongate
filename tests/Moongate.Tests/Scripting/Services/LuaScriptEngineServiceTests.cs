using DryIoc;
using Moongate.Scripting.Data.Config;
using Moongate.Scripting.Data.Events;
using Moongate.Scripting.Extensions.Scripts;
using Moongate.Scripting.Interfaces;
using Moongate.Scripting.Modules;
using Moongate.Scripting.Services;
using Moongate.Scripting.Types.Scripts;
using Moongate.Server.Core.Extensions;
using Moongate.Server.Core.Interfaces.Services;
using Moongate.Tests.TestSupport.Scripting;

namespace Moongate.Tests.Scripting.Services;

public sealed class LuaScriptEngineServiceTests : IDisposable
{
    private readonly TemporaryScriptsDirectory _scripts = new();
    private readonly Container _container = new();
    private readonly StubGameLoop _loop = new();
    private readonly RecordingTimerService _timers = new();
    private readonly List<ScriptErrorEvent> _events = [];

    public LuaScriptEngineServiceTests()
    {
        _container.RegisterMoongateEventBus();
        _container.RegisterInstance<IGameLoopService>(_loop);
        _container.RegisterInstance<ITimerService>(_timers);
        _container.RegisterScriptModule<ProbeModule>();
        _container.Resolve<Moongate.Server.Core.Interfaces.Events.IMoongateEventBus>()
            .Subscribe<ScriptErrorEvent>((evt, _) => { _events.Add(evt); return Task.CompletedTask; });
    }

    private LuaScriptEngineService NewEngine(bool writeDefinitions = false)
    {
        var options = new ScriptEngineOptions
        {
            ScriptsDirectory = _scripts.Path,
            MaxInstructionsPerResume = 20_000,
            HookInterval = 100,
            WriteDefinitions = writeDefinitions
        };

        return new LuaScriptEngineService(
            options,
            _container.Resolve<IScriptModuleRegistry>(),
            _container,
            _loop,
            _timers,
            new EventBusAdapter(_container)
        );
    }

    [Fact]
    public async Task StartAsync_RunsThePreludeAndInitLua_WithModulesBound()
    {
        _scripts.Write("init.lua",
            "result = probe.add(1, 2) has_wait = type(wait) == 'function' has_engine = engine.name\n" +
            "function inspect() return result, has_wait, has_engine end");
        using var engine = NewEngine();

        await engine.StartAsync();

        Assert.Equal([3d, true, "Moongate"], engine.Call("inspect").Values);
        Assert.Equal(1, engine.GetMetrics().FilesLoaded);
    }

    [Fact]
    public async Task StartAsync_WithoutInitLua_StillStarts()
    {
        using var engine = NewEngine();

        await engine.StartAsync();

        Assert.Equal(0, engine.GetMetrics().FilesLoaded);
    }

    [Fact]
    public async Task StartAsync_InitLuaError_AbortsStartup()
    {
        _scripts.Write("init.lua", "error('broken boot')");
        using var engine = NewEngine();

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(engine.StartAsync);

        Assert.Contains("init.lua", exception.Message, StringComparison.Ordinal);
        Assert.Contains("broken boot", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Call_MissingFunction_FailsWithoutThrowing()
    {
        using var engine = NewEngine();
        await engine.StartAsync();

        var result = engine.Call("nothing_here");

        Assert.Equal(ScriptResultKind.Failed, result.Kind);
        Assert.Contains("nothing_here", result.Error!.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Call_FunctionThatWaits_ReportsSuspended_AndResumesFromTheTimer()
    {
        _scripts.Write("init.lua", "function slow() wait(1) slow_done = true end function check() return slow_done == true end");
        using var engine = NewEngine();
        await engine.StartAsync();

        var result = engine.Call("slow");

        Assert.Equal(ScriptResultKind.Suspended, result.Kind);
        Assert.Equal([false], engine.Call("check").Values);
        _timers.Fire(_timers.Timers.Single().Id);
        Assert.Equal([true], engine.Call("check").Values);
    }

    [Fact]
    public async Task ScriptError_IsPublishedOnTheBus_AndTheEngineKeepsWorking()
    {
        _scripts.Write("init.lua", "function boom() error('kaboom') end function fine() return 1 end");
        using var engine = NewEngine();
        await engine.StartAsync();

        engine.Call("boom");

        var evt = Assert.Single(_events);
        Assert.Equal("init.lua", evt.Error.File);
        Assert.Contains("kaboom", evt.Error.Message, StringComparison.Ordinal);
        Assert.Equal([1d], engine.Call("fine").Values);
        Assert.Equal(1, engine.GetMetrics().Errors);
    }

    [Fact]
    public async Task Invalidate_ThenLoadFile_RunsTheNewVersion_AndCancelsWhatTheOldOneScheduled()
    {
        _scripts.Write("ai/guard.lua", "timer.every(1, function() ticks = (ticks or 0) + 1 end) version = 1");
        using var engine = NewEngine();
        await engine.StartAsync();
        engine.LoadFile("ai/guard.lua");
        Assert.Single(_timers.Timers);
        _scripts.Write("ai/guard.lua", "version = 2");

        engine.Invalidate("ai/guard.lua");
        engine.LoadFile("ai/guard.lua");

        Assert.Empty(_timers.Timers);
        Assert.Equal(2, engine.GetMetrics().FilesLoaded);
    }

    [Fact]
    public async Task EveryPublicMember_OffTheLoopThread_Throws()
    {
        using var engine = NewEngine();
        await engine.StartAsync();
        _loop.IsOnLoopThread = false;

        Assert.Throws<InvalidOperationException>(() => engine.LoadFile("init.lua"));
        Assert.Throws<InvalidOperationException>(() => engine.Call("x"));
        Assert.Throws<InvalidOperationException>(() => engine.Invalidate("init.lua"));
    }

    [Fact]
    public async Task Io_And_Os_AreNotAvailableToScripts()
    {
        _scripts.Write("init.lua", "function libs() return io == nil, os == nil, require ~= nil end");
        using var engine = NewEngine();
        await engine.StartAsync();

        Assert.Equal([true, true, true], engine.Call("libs").Values);
    }

    [Fact]
    public async Task StopAsync_ThenDispose_IsSafeTwice()
    {
        var engine = NewEngine();
        await engine.StartAsync();

        await engine.StopAsync();
        engine.Dispose();
        engine.Dispose();
    }

    public void Dispose()
    {
        _container.Dispose();
        _scripts.Dispose();
    }
}
