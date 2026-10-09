using Moongate.Scripting.Data.Scripts;
using Moongate.Server.Ultima.Data.Schedule;
using Moongate.Server.Ultima.Services;
using Moongate.Tests.TestSupport.Localization;
using Moongate.Tests.TestSupport.Scripting;
using Moongate.Tests.TestSupport.Timing;
using Moongate.Tests.TestSupport.Ultima.Loaders;
using Moongate.Tests.TestSupport.Ultima.Schedule;

namespace Moongate.Tests.Server.Ultima.Services;

public sealed class ScheduleServiceTests
{
    private readonly SettableClock _clock = new() { Now = Utc(2026, 1, 10, 2) };
    private readonly RecordingTimerService _timers = new();
    private readonly RecordingBroadcastService _broadcast = new();
    private readonly RecordingShutdownService _shutdown = new();
    private readonly RecordingEventScriptService _scripts = new();

    [Fact]
    public async Task StartAsync_ArmsOneTimer_AtTheNearestStep()
    {
        // 03:00 local; the stop is at 04:00 local = 03:00Z, the first warning (600 s) at 02:50Z.
        await Start(Nightly(600, 60));

        var timer = Assert.Single(_timers.Timers);
        Assert.Equal(TimeSpan.FromMinutes(50), timer.Interval);
    }

    [Fact]
    public async Task TheWarnings_ThenTheStop_RunInOrder()
    {
        await Start(Nightly(600, 60));

        RunTimer(Utc(2026, 1, 10, 2, 50));
        Assert.Equal(["The server will shut down in 10 minutes."], _broadcast.Sent);

        RunTimer(Utc(2026, 1, 10, 2, 59));
        Assert.Equal("The server will shut down in 60 seconds.", _broadcast.Sent[^1]);
        Assert.Equal(0, _shutdown.Requests);

        RunTimer(Utc(2026, 1, 10, 3));
        Assert.Equal("The server is shutting down now.", _broadcast.Sent[^1]);
        Assert.Equal(1, _shutdown.Requests);
    }

    [Fact]
    public async Task StartingInsideTheWindow_RunsOnlyTheWarningsAhead_WithTheRealTimeLeft()
    {
        _clock.Now = Utc(2026, 1, 10, 2, 55, 30);
        await Start(Nightly(600, 300, 60, 10));

        // The 600 s and 300 s steps are past; the next is the 60 s one at 02:59.
        Assert.Equal(TimeSpan.FromSeconds(210), Assert.Single(_timers.Timers).Interval);

        RunTimer(Utc(2026, 1, 10, 2, 59));
        RunTimer(Utc(2026, 1, 10, 2, 59, 50));
        RunTimer(Utc(2026, 1, 10, 3));

        Assert.Equal(
            [
                "The server will shut down in 60 seconds.",
                "The server will shut down in 10 seconds.",
                "The server is shutting down now."
            ],
            _broadcast.Sent
        );
        Assert.Equal(1, _shutdown.Requests);
    }

    [Fact]
    public async Task ALateTimer_ReportsTheWarningAndNotTheJitter()
    {
        await Start(Nightly(600));

        RunTimer(Utc(2026, 1, 10, 2, 50, 2));

        Assert.Equal(["The server will shut down in 10 minutes."], _broadcast.Sent);
    }

    [Fact]
    public async Task StartingAfterTheStopOfTheDay_WaitsForTomorrow()
    {
        _clock.Now = Utc(2026, 1, 10, 3, 0, 1);
        await Start(Nightly(600));

        Assert.Equal(Utc(2026, 1, 11, 2, 50) - _clock.Now, Assert.Single(_timers.Timers).Interval);
    }

    [Fact]
    public async Task ABroadcastTask_SendsItsText_OrItsLocalizedMessage()
    {
        _clock.Now = Utc(2026, 1, 10, 4);
        await Start(
            Task("text_tip", "day", "06:00", "broadcast", text: "Visit the bank."),
            Task("message_tip", "day", "06:00", "broadcast", message: 30230)
        );

        RunTimer(Utc(2026, 1, 10, 5));

        Assert.Equal(["Visit the bank.", "Tip of the day"], _broadcast.Sent);
    }

    [Fact]
    public async Task ALuaTask_CallsRunOfTheScript_WithTheIdAndTheScheduledTime()
    {
        _clock.Now = Utc(2026, 1, 10, 4);
        await Start(Task("daily_cleanup", "day", "06:00", "lua", script: "cleanup"));

        RunTimer(Utc(2026, 1, 10, 5));

        var call = Assert.Single(_scripts.Calls);
        Assert.Equal(("cleanup", "run"), (call.Script, call.Function));
        Assert.Equal(["daily_cleanup", Utc(2026, 1, 10, 5).ToUnixTimeSeconds()], call.Args);
    }

    [Fact]
    public async Task ALuaTask_CanNameItsFunction()
    {
        _clock.Now = Utc(2026, 1, 10, 4);
        await Start(Task("daily_cleanup", "day", "06:00", "lua", script: "cleanup", function: "tick"));

        RunTimer(Utc(2026, 1, 10, 5));

        Assert.Equal("tick", Assert.Single(_scripts.Calls).Function);
    }

    [Fact]
    public async Task AMissingScript_IsHarmless_AndTheNextRunStillCallsIt()
    {
        _scripts.Result = ScriptResult.Missing;
        _clock.Now = Utc(2026, 1, 10, 4);
        await Start(Task("daily_cleanup", "day", "06:00", "lua", script: "cleanup"));

        RunTimer(Utc(2026, 1, 10, 5));
        RunTimer(Utc(2026, 1, 11, 5));

        Assert.Equal(2, _scripts.Calls.Count);
    }

    [Fact]
    public async Task AFailingBroadcast_DoesNotStopTheOtherTasksOfTheSameMoment()
    {
        _broadcast.Fail = true;
        _clock.Now = Utc(2026, 1, 10, 4);
        await Start(
            Task("tip", "day", "06:00", "broadcast", text: "Tip"),
            Task("daily_cleanup", "day", "06:00", "lua", script: "cleanup")
        );

        RunTimer(Utc(2026, 1, 10, 5));

        Assert.Single(_scripts.Calls);
        Assert.Single(_timers.Timers);
    }

    [Fact]
    public async Task AThrowingScript_DoesNotStopTheOtherTasksOfTheSameMoment()
    {
        _scripts.Throw = true;
        _clock.Now = Utc(2026, 1, 10, 4);
        await Start(
            Task("daily_cleanup", "day", "06:00", "lua", script: "cleanup"),
            Task("tip", "day", "06:00", "broadcast", text: "Tip")
        );

        RunTimer(Utc(2026, 1, 10, 5));

        Assert.Equal(["Tip"], _broadcast.Sent);
    }

    [Fact]
    public async Task TwoTasksAtTheSameMoment_BothRun()
    {
        _clock.Now = Utc(2026, 1, 10, 4);
        await Start(
            Task("a", "day", "06:00", "broadcast", text: "A"),
            Task("b", "day", "06:00", "broadcast", text: "B")
        );

        RunTimer(Utc(2026, 1, 10, 5));

        Assert.Equal(["A", "B"], _broadcast.Sent);
    }

    [Fact]
    public async Task AShutdownAskedByAnotherPath_IsNotRequestedAgain()
    {
        _shutdown.MarkRequested();
        await Start(Nightly());

        RunTimer(Utc(2026, 1, 10, 3));

        Assert.Equal(0, _shutdown.Requests);
        Assert.Empty(_broadcast.Sent);
    }

    [Fact]
    public async Task TwoShutdownTasksAtTheSameMoment_RequestOnce()
    {
        await Start(Task("one", "day", "04:00", "shutdown"), Task("two", "day", "04:00", "shutdown"));

        RunTimer(Utc(2026, 1, 10, 3));

        Assert.Equal(1, _shutdown.Requests);
    }

    [Fact]
    public async Task NextOccurrence_IsTheNextStopOfTheTask_NullForAnUnknownId()
    {
        var service = await Start(Nightly(600));

        Assert.Equal(Utc(2026, 1, 10, 3), service.NextOccurrence("nightly_restart"));
        Assert.Null(service.NextOccurrence("nothing"));
    }

    [Fact]
    public async Task AnEmptyCalendar_ArmsNoTimer()
    {
        var service = new ScheduleService(
            new StubDataLoaderService().With<ScheduleFile>(),
            _timers,
            _broadcast,
            _shutdown,
            _scripts,
            _clock,
            TestZones.Europe,
            Localization()
        );

        await service.StartAsync();

        Assert.Empty(_timers.Timers);
        Assert.Empty(service.Tasks);
    }

    [Fact]
    public async Task ATimerThatCannotBeArmed_DoesNotThrowFromTheCallback()
    {
        _clock.Now = Utc(2026, 1, 10, 4);
        await Start(Task("tip", "day", "06:00", "broadcast", text: "Tip"));
        _clock.Now = Utc(2026, 1, 10, 5);
        _timers.ThrowOnRegister = true;

        var exception = Record.Exception(() => _timers.Fire(Assert.Single(_timers.Timers).Id));

        Assert.Null(exception);
        Assert.Equal(["Tip"], _broadcast.Sent);
    }

    [Fact]
    public async Task StopAsync_UnregistersTheTimer()
    {
        var service = await Start(Nightly(600));
        var id = Assert.Single(_timers.Timers).Id;

        await service.StopAsync();

        Assert.Empty(_timers.Timers);
        Assert.Contains(id, _timers.Unregistered);
    }

    [Fact]
    public async Task DaylightSaving_TheStopStaysAt0400Local()
    {
        // The clocks go forward on 2026-03-29: 04:00 local is then 02:00Z, not 03:00Z.
        _clock.Now = Utc(2026, 3, 28, 12);
        await Start(Nightly());

        Assert.Equal(Utc(2026, 3, 29, 2) - _clock.Now, Assert.Single(_timers.Timers).Interval);
    }

    private static DateTimeOffset Utc(int year, int month, int day, int hour = 0, int minute = 0, int second = 0)
    {
        return new DateTimeOffset(year, month, day, hour, minute, second, TimeSpan.Zero);
    }

    private static ScheduleTask Nightly(params int[] warnings)
    {
        var task = Task("nightly_restart", "day", "04:00", "shutdown");
        task.Warnings = [.. warnings];

        return task;
    }

    private static ScheduleTask Task(
        string id,
        string every,
        string at,
        string action,
        string? text = null,
        int? message = null,
        string? script = null,
        string? function = null
    )
    {
        return new()
        {
            Id = id, When = new() { Every = every, At = at }, Action = action, Text = text, Message = message,
            Script = script,
            Function = function
        };
    }

    private static Moongate.Server.Core.Interfaces.Services.ILocalizationService Localization()
    {
        return TestLocalization.With(
            (30016, "The server is shutting down now."),
            (30017, "The server will shut down in {0} seconds."),
            (30224, "The server will shut down in {0} minutes."),
            (30230, "Tip of the day")
        );
    }

    private async Task<ScheduleService> Start(params ScheduleTask[] tasks)
    {
        var service = new ScheduleService(
            new StubDataLoaderService().With(new ScheduleFile { Task = [.. tasks] }),
            _timers,
            _broadcast,
            _shutdown,
            _scripts,
            _clock,
            TestZones.Europe,
            Localization()
        );
        await service.StartAsync();

        return service;
    }

    // Moves the clock to the moment of the timer and fires it.
    private void RunTimer(DateTimeOffset at)
    {
        _clock.Now = at;
        _timers.Fire(Assert.Single(_timers.Timers).Id);
    }
}
