using Moongate.Core.Data.Directories;
using Moongate.Core.Types;
using Moongate.Scripting.Data.Scripts;
using Moongate.Scripting.Interfaces;
using Moongate.Server.Data.Events.Base;
using Moongate.Server.Data.Events.Scheduling;
using Moongate.Server.Interfaces.Services.Events;
using Moongate.Server.Interfaces.Services.Scripting;
using Moongate.Server.Interfaces.Services.Timing;
using Moongate.Server.Services.Scripting;
using Moongate.Tests.TestSupport;
using MoonSharp.Interpreter;

namespace Moongate.Tests.Server.Services.Scripting;

public sealed class ScheduledEventServiceTests
{
    private sealed class ScheduledEventServiceTestTimerService : ITimerService
    {
        public List<(string Name, TimeSpan Interval, TimeSpan? Delay, Action Callback)> Registrations { get; } = [];

        public void FireSingle(string name)
        {
            var timer = Registrations.Single(registration => registration.Name == name);
            timer.Callback();
        }

        public void ProcessTick() { }

        public string RegisterTimer(
            string name,
            TimeSpan interval,
            Action callback,
            TimeSpan? delay = null,
            bool repeat = false
        )
        {
            Registrations.Add((name, interval, delay, callback));

            return $"{name}:{Registrations.Count}";
        }

        public void UnregisterAllTimers()
            => Registrations.Clear();

        public bool UnregisterTimer(string timerId)
            => true;

        public int UnregisterTimersByName(string name)
            => 0;

        public int UpdateTicksDelta(long timestampMilliseconds)
            => 0;
    }

    private sealed class ScheduledEventServiceTestScriptEngineService : IScriptEngineService
    {
        private readonly IScheduledEventDefinitionService _definitionService;

        public ScheduledEventServiceTestScriptEngineService(IScheduledEventDefinitionService definitionService)
        {
            _definitionService = definitionService;
        }

        public void AddCallback(string name, Action<object[]> callback) { }
        public void AddConstant(string name, object value) { }
        public void AddInitScript(string script) { }
        public void AddManualModuleFunction(string moduleName, string functionName, Action<object[]> callback) { }

        public void AddManualModuleFunction<TInput, TOutput>(
            string moduleName,
            string functionName,
            Func<TInput?, TOutput> callback
        ) { }

        public void AddScriptModule(Type type) { }
        public void CallFunction(string functionName, params object[] args) { }
        public void ClearScriptCache() { }
        public void InvalidateScript(string filePath)
            => _ = filePath;
        public void ExecuteCallback(string name, params object[] args) { }
        public void ExecuteEngineReady() { }

        public ScriptResult ExecuteFunction(string command)
            => new();

        public Task<ScriptResult> ExecuteFunctionAsync(string command)
            => Task.FromResult(new ScriptResult());

        public void ExecuteFunctionFromBootstrap(string name) { }
        public void ExecuteScript(string script) { }

        public void ExecuteScriptFile(string scriptFile)
        {
            var script = new Script();
            var definition = script.DoString(File.ReadAllText(scriptFile)).Table!;
            _ = _definitionService.Register(Path.GetFileNameWithoutExtension(scriptFile), definition, scriptFile);
        }

        public ScriptExecutionMetrics GetExecutionMetrics()
            => new();

        public void RegisterGlobal(string name, object value) { }
        public void RegisterGlobalFunction(string name, Delegate func) { }

        public Task StartAsync()
            => Task.CompletedTask;

        public Task StopAsync()
            => Task.CompletedTask;

        public string ToScriptEngineFunctionName(string name)
            => name;

        public bool UnregisterGlobal(string name)
            => true;

#pragma warning disable CS0067
        public event EventHandler<ScriptErrorInfo>? OnScriptError;
#pragma warning restore CS0067
    }

    private sealed class ScheduledEventServiceTestGameEventBusService : IGameEventBusService
    {
        public List<IGameEvent> PublishedEvents { get; } = [];

        public ValueTask PublishAsync<TEvent>(TEvent gameEvent, CancellationToken cancellationToken = default)
            where TEvent : IGameEvent
        {
            PublishedEvents.Add(gameEvent);

            return ValueTask.CompletedTask;
        }

        public void RegisterListener<TEvent>(IGameEventListener<TEvent> listener) where TEvent : IGameEvent { }
    }

    [Test]
    public async Task StartAsync_WhenDailyEventUsesTimeZone_ShouldScheduleUsingZone()
    {
        using var tempDirectory = new TempDirectory();
        var scriptsRoot = Path.Combine(tempDirectory.Path, "scripts");
        var eventsDirectory = Path.Combine(scriptsRoot, "events");
        Directory.CreateDirectory(eventsDirectory);
        await File.WriteAllTextAsync(
            Path.Combine(eventsDirectory, "rome_daily.lua"),
            """
            return {
                trigger_name = "rome_daily_trigger",
                recurrence = "daily",
                time = "09:00",
                time_zone = "Europe/Rome"
            }
            """
        );

        var definitions = new ScheduledEventDefinitionService();
        var timer = new ScheduledEventServiceTestTimerService();
        var bus = new ScheduledEventServiceTestGameEventBusService();
        var utcNow = new DateTime(2026, 03, 19, 7, 30, 0, DateTimeKind.Utc);
        var service = CreateService(tempDirectory.Path, definitions, timer, bus, utcNow);

        await service.StartAsync();

        Assert.That(timer.Registrations, Has.Count.EqualTo(1));
        Assert.That(timer.Registrations[0].Delay, Is.EqualTo(TimeSpan.FromMinutes(30)));
    }

    [Test]
    public async Task StartAsync_WhenDailyScriptExists_ShouldLoadDefinitionAndRegisterTimer()
    {
        using var tempDirectory = new TempDirectory();
        var scriptsRoot = Path.Combine(tempDirectory.Path, "scripts");
        var eventsDirectory = Path.Combine(scriptsRoot, "events");
        Directory.CreateDirectory(eventsDirectory);
        await File.WriteAllTextAsync(
            Path.Combine(eventsDirectory, "town_crier_morning.lua"),
            """
            return {
                trigger_name = "town_crier_announcement",
                recurrence = "daily",
                time = "09:00"
            }
            """
        );

        var definitions = new ScheduledEventDefinitionService();
        var timer = new ScheduledEventServiceTestTimerService();
        var bus = new ScheduledEventServiceTestGameEventBusService();
        var service = CreateService(
            tempDirectory.Path,
            definitions,
            timer,
            bus,
            new(2026, 03, 19, 8, 0, 0, DateTimeKind.Utc)
        );

        await service.StartAsync();

        Assert.Multiple(
            () =>
            {
                Assert.That(service.GetScheduledEventCount(), Is.EqualTo(1));
                Assert.That(definitions.TryGet("town_crier_morning", out var definition), Is.True);
                Assert.That(definition!.TriggerName, Is.EqualTo("town_crier_announcement"));
                Assert.That(timer.Registrations, Has.Count.EqualTo(1));
                Assert.That(timer.Registrations[0].Name, Is.EqualTo("scheduled_event:town_crier_morning"));
            }
        );
    }

    [Test]
    public async Task StartAsync_WhenDefinitionIsDisabled_ShouldNotRegisterTimer()
    {
        using var tempDirectory = new TempDirectory();
        var scriptsRoot = Path.Combine(tempDirectory.Path, "scripts");
        var eventsDirectory = Path.Combine(scriptsRoot, "events");
        Directory.CreateDirectory(eventsDirectory);
        await File.WriteAllTextAsync(
            Path.Combine(eventsDirectory, "disabled_event.lua"),
            """
            return {
                enabled = false,
                trigger_name = "disabled_trigger",
                recurrence = "daily",
                time = "09:00"
            }
            """
        );

        var definitions = new ScheduledEventDefinitionService();
        var timer = new ScheduledEventServiceTestTimerService();
        var bus = new ScheduledEventServiceTestGameEventBusService();
        var service = CreateService(
            tempDirectory.Path,
            definitions,
            timer,
            bus,
            new(2026, 03, 19, 8, 0, 0, DateTimeKind.Utc)
        );

        await service.StartAsync();

        Assert.Multiple(
            () =>
            {
                Assert.That(service.GetScheduledEventCount(), Is.EqualTo(0));
                Assert.That(definitions.TryGet("disabled_event", out var definition), Is.True);
                Assert.That(definition!.Enabled, Is.False);
                Assert.That(timer.Registrations, Is.Empty);
            }
        );
    }

    [Test]
    public async Task TimerCallback_WhenFired_ShouldPublishScheduledEvent()
    {
        using var tempDirectory = new TempDirectory();
        var scriptsRoot = Path.Combine(tempDirectory.Path, "scripts");
        var eventsDirectory = Path.Combine(scriptsRoot, "events");
        Directory.CreateDirectory(eventsDirectory);
        await File.WriteAllTextAsync(
            Path.Combine(eventsDirectory, "town_crier_morning.lua"),
            """
            return {
                trigger_name = "town_crier_announcement",
                recurrence = "daily",
                time = "09:00"
            }
            """
        );

        var definitions = new ScheduledEventDefinitionService();
        var timer = new ScheduledEventServiceTestTimerService();
        var bus = new ScheduledEventServiceTestGameEventBusService();
        var service = CreateService(
            tempDirectory.Path,
            definitions,
            timer,
            bus,
            new(2026, 03, 19, 8, 0, 0, DateTimeKind.Utc)
        );

        await service.StartAsync();
        timer.FireSingle("scheduled_event:town_crier_morning");

        Assert.Multiple(
            () =>
            {
                Assert.That(bus.PublishedEvents, Has.Count.EqualTo(1));
                Assert.That(bus.PublishedEvents[0], Is.TypeOf<ScheduledEventTriggeredEvent>());
                var gameEvent = (ScheduledEventTriggeredEvent)bus.PublishedEvents[0];
                Assert.That(gameEvent.EventId, Is.EqualTo("town_crier_morning"));
                Assert.That(gameEvent.TriggerName, Is.EqualTo("town_crier_announcement"));
            }
        );
    }

    private static ScheduledEventService CreateService(
        string root,
        IScheduledEventDefinitionService definitions,
        ITimerService timer,
        IGameEventBusService bus,
        DateTime utcNow
    )
    {
        var directories = new DirectoriesConfig(root, Enum.GetNames<DirectoryType>());
        Directory.CreateDirectory(directories[DirectoryType.Scripts]);

        return new(
            directories,
            new ScheduledEventServiceTestScriptEngineService(definitions),
            definitions,
            timer,
            bus,
            () => utcNow
        );
    }
}
