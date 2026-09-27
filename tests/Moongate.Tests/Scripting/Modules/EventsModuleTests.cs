using DryIoc;
using Moongate.Scripting.Data.Config;
using Moongate.Scripting.Data.Events;
using Moongate.Scripting.Extensions.Scripts;
using Moongate.Scripting.Interfaces;
using Moongate.Scripting.Services;
using Moongate.Scripting.Types.Scripts;
using Moongate.Server.Core.Extensions;
using Moongate.Server.Core.Interfaces.Events;
using Moongate.Server.Core.Interfaces.Services;
using Moongate.Tests.TestSupport.Scripting;

namespace Moongate.Tests.Scripting.Modules;

public sealed class EventsModuleTests : IDisposable
{
    private readonly TemporaryScriptsDirectory _scripts = new();
    private readonly Container _container = new();
    private readonly StubGameLoop _loop = new();
    private readonly RecordingTimerService _timers = new();
    private readonly List<ScriptErrorEvent> _errors = [];

    public EventsModuleTests()
    {
        _container.RegisterMoongateEventBus();
        _container.RegisterInstance<IGameLoopService>(_loop);
        _container.RegisterInstance<ITimerService>(_timers);
        _container.AddScriptEvent<ProbeEvent>(
            "probe_fired",
            e => new Dictionary<string, object?> { ["name"] = e.Name, ["value"] = e.Value }
        );
        _container.Resolve<IMoongateEventBus>()
            .Subscribe<ScriptErrorEvent>((evt, _) =>
                {
                    _errors.Add(evt);

                    return Task.CompletedTask;
                }
            );
    }

    private IMoongateEventBus Bus => _container.Resolve<IMoongateEventBus>();

    [Fact]
    public async Task On_ReturnsAHandle_AndOffRemovesItOnce()
    {
        _scripts.Write(
            "init.lua",
            "function sub() h = events.on('probe_fired', function(e) end) return type(h) end " +
            "function unsub() return events.off(h), events.off(h) end"
        );
        using var engine = NewEngine();
        await engine.StartAsync();

        Assert.Equal(["string"], engine.Call("sub").Values);
        Assert.Equal([true, false], engine.Call("unsub").Values);
    }

    [Fact]
    public async Task On_UnknownEventName_FailsWithAScriptError()
    {
        _scripts.Write("init.lua", "function sub() events.on('no_such_event', function(e) end) end");
        using var engine = NewEngine();
        await engine.StartAsync();

        var result = engine.Call("sub");

        Assert.Equal(ScriptResultKind.Failed, result.Kind);
        Assert.Contains("no_such_event", result.Error!.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task On_NonFunctionHandler_FailsWithAScriptError()
    {
        _scripts.Write("init.lua", "function sub() events.on('probe_fired', 42) end");
        using var engine = NewEngine();
        await engine.StartAsync();

        var result = engine.Call("sub");

        Assert.Equal(ScriptResultKind.Failed, result.Kind);
        Assert.Contains("function", result.Error!.Message, StringComparison.Ordinal);
    }

    private LuaScriptEngineService NewEngine()
    {
        var options = new ScriptEngineOptions
        {
            ScriptsDirectory = _scripts.Path,
            MaxInstructionsPerResume = 20_000,
            MaxInstructionsPerChunk = 100_000,
            HookInterval = 100,
            WriteDefinitions = false,
            MaxStringLength = 16 * 1024 * 1024
        };

        return new(
            options,
            _container.Resolve<IScriptModuleRegistry>(),
            _container,
            _loop,
            _timers,
            new EventBusAdapter(_container)
        );
    }

    public void Dispose()
    {
        _container.Dispose();
        _scripts.Dispose();
    }
}
