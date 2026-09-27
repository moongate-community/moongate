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
        _container.AddScriptModule<CallbackModule>();
        _container.AddScriptEvent<UnsupportedValueEvent>(
            "unsupported_value",
            e => new Dictionary<string, object?> { ["when"] = e.When }
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

    [Fact]
    public async Task Publish_ReachesTheHandler_WithTheMappedFields()
    {
        _scripts.Write(
            "init.lua",
            "events.on('probe_fired', function(e) got = e.name .. ':' .. e.value end) function check() return got end"
        );
        using var engine = NewEngine();
        await engine.StartAsync();

        await Bus.PublishAsync(new ProbeEvent("orc", 3));

        Assert.Equal(["orc:3"], engine.Call("check").Values);
    }

    [Fact]
    public async Task Publish_HandlerRunsAsACoroutine_AndMayWait()
    {
        _scripts.Write(
            "init.lua",
            "events.on('probe_fired', function(e) wait(1) done = e.value end) function check() return done end"
        );
        using var engine = NewEngine();
        await engine.StartAsync();

        await Bus.PublishAsync(new ProbeEvent("orc", 5));
        Assert.Null(Assert.Single(engine.Call("check").Values));
        _timers.Fire(_timers.Timers.Single().Id);

        Assert.Equal([5d], engine.Call("check").Values);
    }

    [Fact]
    public async Task Publish_RunsHandlersInSubscriptionOrder()
    {
        _scripts.Write(
            "init.lua",
            "order = '' events.on('probe_fired', function(e) order = order .. 'a' end) " +
            "events.on('probe_fired', function(e) order = order .. 'b' end) function check() return order end"
        );
        using var engine = NewEngine();
        await engine.StartAsync();

        await Bus.PublishAsync(new ProbeEvent("orc", 1));

        Assert.Equal(["ab"], engine.Call("check").Values);
    }

    [Fact]
    public async Task Publish_ThrowingHandler_IsReported_AndTheNextStillRuns()
    {
        _scripts.Write(
            "init.lua",
            "events.on('probe_fired', function(e) error('boom') end) " +
            "events.on('probe_fired', function(e) second = true end) function check() return second end"
        );
        using var engine = NewEngine();
        await engine.StartAsync();

        await Bus.PublishAsync(new ProbeEvent("orc", 1));

        Assert.Equal([true], engine.Call("check").Values);
        Assert.Contains(_errors, e => e.Error.Message.Contains("boom", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Dispatch_HandlerUnsubscribingDuringDelivery_DoesNotSkipTheNextSubscriber()
    {
        _scripts.Write(
            "init.lua",
            "h = events.on('probe_fired', function(e) events.off(h) end) " +
            "events.on('probe_fired', function(e) count = (count or 0) + 1 end) function check() return count end"
        );
        using var engine = NewEngine();
        await engine.StartAsync();

        await Bus.PublishAsync(new ProbeEvent("orc", 1));
        await Bus.PublishAsync(new ProbeEvent("orc", 2));

        Assert.Equal([2d], engine.Call("check").Values);
    }

    [Fact]
    public async Task Dispatch_EachHandlerGetsItsOwnTable()
    {
        _scripts.Write(
            "init.lua",
            "events.on('probe_fired', function(e) e.name = 'changed' end) " +
            "events.on('probe_fired', function(e) seen = e.name end) function check() return seen end"
        );
        using var engine = NewEngine();
        await engine.StartAsync();

        await Bus.PublishAsync(new ProbeEvent("orc", 1));

        Assert.Equal(["orc"], engine.Call("check").Values);
    }

    [Fact]
    public async Task Publish_WithoutSubscribers_PostsNothing()
    {
        using var engine = NewEngine();
        await engine.StartAsync();
        var before = _loop.PostedWorkItems;

        await Bus.PublishAsync(new ProbeEvent("orc", 1));

        Assert.Equal(before, _loop.PostedWorkItems);
    }

    [Fact]
    public async Task Publish_WhenTheLoopRefusesWork_IsDroppedWithoutThrowing()
    {
        _scripts.Write("init.lua", "events.on('probe_fired', function(e) got = true end) function check() return got end");
        using var engine = NewEngine();
        await engine.StartAsync();
        _loop.RefuseTryPost = true;

        await Bus.PublishAsync(new ProbeEvent("orc", 1));

        _loop.RefuseTryPost = false;
        Assert.Null(Assert.Single(engine.Call("check").Values));
        Assert.Equal(1, engine.GetMetrics().EventsDropped);
    }

    [Fact]
    public async Task Publish_MappingWithUnsupportedValue_IsSkipped_AndLaterEventsStillArrive()
    {
        _scripts.Write(
            "init.lua",
            "events.on('unsupported_value', function(e) bad = true end) " +
            "events.on('probe_fired', function(e) good = true end) function check() return bad, good end"
        );
        using var engine = NewEngine();
        await engine.StartAsync();

        await Bus.PublishAsync(new UnsupportedValueEvent(DateTime.UnixEpoch));
        await Bus.PublishAsync(new ProbeEvent("orc", 1));

        Assert.Equal(new object?[] { null, true }, engine.Call("check").Values);
    }

    [Fact]
    public async Task Invalidate_RemovesTheFilesSubscriptions()
    {
        _scripts.Write("hooks.lua", "events.on('probe_fired', function(e) hits = (hits or 0) + 1 end)");
        _scripts.Write("init.lua", "function check() return hits end");
        using var engine = NewEngine();
        await engine.StartAsync();
        engine.LoadFile("hooks.lua");
        await Bus.PublishAsync(new ProbeEvent("orc", 1));

        engine.Invalidate("hooks.lua");
        await Bus.PublishAsync(new ProbeEvent("orc", 2));

        Assert.Equal([1d], engine.Call("check").Values);
    }

    [Fact]
    public async Task Publish_BeforeStartAndAfterStop_IsIgnored()
    {
        _scripts.Write("init.lua", "events.on('probe_fired', function(e) end)");
        var engine = NewEngine();

        await Bus.PublishAsync(new ProbeEvent("orc", 1));
        await engine.StartAsync();
        await engine.StopAsync();
        var before = _loop.PostedWorkItems;
        await Bus.PublishAsync(new ProbeEvent("orc", 2));

        Assert.Equal(before, _loop.PostedWorkItems);
        engine.Dispose();
    }

    [Fact]
    public async Task Dispatch_WhenTheSchedulerRefusesAHandler_ReportsItAndRunsTheRest()
    {
        _scripts.Write(
            "init.lua",
            "events.on('probe_fired', function(e) end) events.on('probe_fired', function(e) end) " +
            "function go() callback.invoke() end"
        );
        using var engine = NewEngine();
        await engine.StartAsync();
        Exception? thrown = null;

        // Starting a coroutine while another resumes is refused by the scheduler; Dispatch must survive it
        // rather than fault the game loop that runs it.
        _container.Resolve<CallbackModule>().OnInvoke = () =>
        {
            try
            {
                engine.Dispatch("probe_fired", []);
            }
            catch (Exception exception)
            {
                thrown = exception;
            }
        };
        engine.Call("go");

        Assert.Null(thrown);
        Assert.Equal(2, _errors.Count(e => e.Error.Message.Contains("probe_fired", StringComparison.Ordinal)));
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
