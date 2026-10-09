using DryIoc;
using Moongate.Scripting.Data.Config;
using Moongate.Scripting.Data.Events;
using Moongate.Scripting.Extensions.Scripts;
using Moongate.Scripting.Interfaces;
using Moongate.Scripting.Modules;
using Moongate.Scripting.Services;
using Moongate.Scripting.Types.Scripts;
using Moongate.Server.Core.Extensions;
using Moongate.Server.Core.Interfaces.Events;
using Moongate.Server.Core.Interfaces.Services;
using Moongate.Server.Ultima.Services;
using Moongate.Tests.TestSupport.Scripting;

namespace Moongate.Tests.Server.Ultima.Services;

public sealed class EventScriptServiceTests : IAsyncLifetime
{
    private readonly TemporaryScriptsDirectory _scripts = new();
    private readonly Container _container = new();
    private readonly StubGameLoop _loop = new();
    private readonly RecordingTimerService _timers = new();
    private readonly List<ScriptErrorEvent> _errors = [];

    private LuaScriptEngineService _engine = null!;
    private EventScriptService _service = null!;

    public async Task InitializeAsync()
    {
        _scripts.Write(
            "events/halloween.lua",
            """
            halloween = {}
            function halloween.on_start(id, name) return id .. ":" .. name end
            function halloween.boom() error("boom") end
            """
        );
        _scripts.Write(
            "events/cleanup.lua",
            """
            cleanup = {}
            function cleanup.run(id, scheduled) return id, scheduled end
            """
        );
        var options = new ScriptEngineOptions
        {
            ScriptsDirectory = _scripts.Path, MaxInstructionsPerResume = 20_000, MaxInstructionsPerChunk = 100_000,
            HookInterval = 100, WriteDefinitions = false
        };
        _container.RegisterMoongateEventBus();
        _container.RegisterInstance<IGameLoopService>(_loop);
        _container.RegisterInstance<ITimerService>(_timers);
        _container.AddScriptModule<LogModule>();
        _container.Resolve<IMoongateEventBus>()
            .Subscribe<ScriptErrorEvent>((evt, _) =>
                {
                    _errors.Add(evt);

                    return Task.CompletedTask;
                }
            );
        _engine = new(
            options,
            _container.Resolve<IScriptModuleRegistry>(),
            _container,
            _loop,
            _timers,
            new EventBusAdapter(_container)
        );
        await _engine.StartAsync();
        _service = new EventScriptService(_engine, _loop, options);
    }

    [Fact]
    public async Task Call_AFunctionOfTheScriptTable_GivesItsValue()
    {
        await _service.StartAsync();

        var result = _service.Call("halloween", "on_start", "halloween", "Halloween");

        Assert.Equal(ScriptResultKind.Completed, result.Kind);
        Assert.Equal("halloween:Halloween", Assert.Single(result.Values));
    }

    [Fact]
    public async Task Call_PassesTheArgumentsInOrder()
    {
        await _service.StartAsync();

        var result = _service.Call("cleanup", "run", "daily_cleanup", 1234L);

        Assert.Equal(ScriptResultKind.Completed, result.Kind);
        Assert.Equal(
            ["daily_cleanup", 1234.0],
            result.Values.Select(value => value is long number ? (object)(double)number : value)
        );
    }

    [Theory, InlineData("nothing", "run"), InlineData("halloween", "nope"), InlineData("cleanup", "on_start")]
    public async Task Call_AMissingScriptOrFunction_IsMissing_NotAnError(string script, string function)
    {
        await _service.StartAsync();

        var result = _service.Call(script, function, "x");

        Assert.Equal(ScriptResultKind.Missing, result.Kind);
        Assert.Empty(_errors);
    }

    [Fact]
    public async Task Call_AScriptThatThrows_IsFailed_AndRaisesTheScriptError()
    {
        await _service.StartAsync();

        var result = _service.Call("halloween", "boom");

        Assert.Equal(ScriptResultKind.Failed, result.Kind);
        Assert.NotNull(result.Error);
    }

    [Fact]
    public void Call_BeforeTheStart_IsMissing()
    {
        Assert.Equal(ScriptResultKind.Missing, _service.Call("halloween", "on_start", "x", "y").Kind);
    }

    [Fact]
    public async Task Call_AfterTheStop_IsMissing()
    {
        await _service.StartAsync();
        await _service.StopAsync();

        Assert.Equal(ScriptResultKind.Missing, _service.Call("halloween", "on_start", "x", "y").Kind);
    }

    public Task DisposeAsync()
    {
        _engine.Dispose();
        _container.Dispose();
        _scripts.Dispose();

        return Task.CompletedTask;
    }
}
