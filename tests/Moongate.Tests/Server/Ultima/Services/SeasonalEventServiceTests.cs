using DryIoc;
using Moongate.Core.Primitives;
using Moongate.Scripting.Data.Scripts;
using Moongate.Server.Core.Extensions;
using Moongate.Server.Core.Interfaces.Events;
using Moongate.Server.Ultima.Data.Events;
using Moongate.Server.Ultima.Data.Schedule;
using Moongate.Server.Ultima.Entities.World;
using Moongate.Server.Ultima.Services;
using Moongate.Tests.TestSupport.Persistence;
using Moongate.Tests.TestSupport.Scripting;
using Moongate.Tests.TestSupport.Timing;
using Moongate.Tests.TestSupport.Ultima.Loaders;
using Moongate.Tests.TestSupport.Ultima.Schedule;

namespace Moongate.Tests.Server.Ultima.Services;

public sealed class SeasonalEventServiceTests
{
    private readonly SettableClock _clock = new() { Now = Local(2026, 9, 1, 12) };
    private readonly RecordingTimerService _timers = new();
    private readonly RecordingEventScriptService _scripts = new();
    private readonly RecordingDataAccess<WorldStateEntity> _data = new();
    private readonly StubGameLoop _loop = new();
    private readonly Container _container = new();
    private WorldPropsService _props = null!;

    [Fact]
    public async Task AnEventOutsideItsWindow_IsInactive_AndNothingIsCalled()
    {
        var service = await Start();

        Assert.False(service.IsActive("halloween"));
        Assert.Empty(_scripts.Calls);
    }

    [Fact]
    public async Task AtStartupInsideTheWindow_OnStartIsCalledOnce()
    {
        _clock.Now = Local(2026, 10, 25, 12);
        var service = await Start();

        Assert.True(service.IsActive("halloween"));
        var call = Assert.Single(_scripts.Calls);
        Assert.Equal(("halloween", "on_start"), (call.Script, call.Function));
        Assert.Equal(["halloween", "Halloween"], call.Args);
        Assert.Equal(true, _props.Get("schedule.event.halloween.active"));

        // The same props at the next start: already told.
        await Restart();
        Assert.Single(_scripts.Calls);
    }

    [Fact]
    public async Task TheMidnightTimer_StartsAndEndsTheEvent()
    {
        _clock.Now = Local(2026, 10, 19, 12);
        await Start();
        Assert.Empty(_scripts.Calls);

        FireTimer(Local(2026, 10, 20));
        Assert.Equal(["on_start"], _scripts.Calls.Select(call => call.Function));

        FireTimer(Local(2026, 11, 3));
        Assert.Equal(["on_start", "on_end"], _scripts.Calls.Select(call => call.Function));

        // The timer is armed again for the next local midnight.
        Assert.Equal(Local(2026, 11, 4) - _clock.Now, Assert.Single(_timers.Timers).Interval);
    }

    [Fact]
    public async Task TheLastDay_IsStillInside_AndTheDayAfterEnds()
    {
        _clock.Now = Local(2026, 11, 2, 23, 59);
        var service = await Start();
        Assert.True(service.IsActive("halloween"));

        FireTimer(Local(2026, 11, 3));

        Assert.False(service.IsActive("halloween"));
    }

    [Theory]
    [InlineData(2026, 12, 31, true)]
    [InlineData(2027, 1, 1, true)]
    [InlineData(2027, 1, 6, true)]
    [InlineData(2027, 1, 7, false)]
    [InlineData(2026, 12, 19, false)]
    public async Task AWindowCrossingTheNewYear_IsActiveOnBothSides(int year, int month, int day, bool active)
    {
        _clock.Now = Local(year, month, day, 12);
        var service = await Start();

        Assert.Equal(active, service.IsActive("winter"));
    }

    [Fact]
    public async Task AnEventThatStartedWhileTheServerWasOff_StartsAtTheNextStartup()
    {
        _clock.Now = Local(2026, 10, 25, 12);
        _data.Upserted.Add(new() { Props = new() { ["schedule.event.halloween.active"] = false } });

        await Start();

        Assert.Equal(["on_start"], _scripts.Calls.Select(call => call.Function));
    }

    [Fact]
    public async Task AnEventThatEndedWhileTheServerWasOff_EndsAtTheNextStartup()
    {
        _clock.Now = Local(2026, 11, 10, 12);
        _data.Upserted.Add(new() { Props = new() { ["schedule.event.halloween.active"] = true } });

        await Start();

        Assert.Equal(["on_end"], _scripts.Calls.Select(call => call.Function));
        Assert.Equal(false, _props.Get("schedule.event.halloween.active"));
    }

    [Fact]
    public async Task SetModeOn_ForcesAnInactiveEventOn_AndModeOffForcesItOffOnce()
    {
        var service = await Start();

        Assert.True(service.SetMode("halloween", "on"));
        Assert.True(service.IsActive("halloween"));
        Assert.Equal(["on_start"], _scripts.Calls.Select(call => call.Function));

        Assert.True(service.SetMode("halloween", "off"));
        Assert.True(service.SetMode("halloween", "off"));
        Assert.False(service.IsActive("halloween"));
        Assert.Equal(["on_start", "on_end"], _scripts.Calls.Select(call => call.Function));
    }

    [Fact]
    public async Task SetModeAuto_ReturnsToTheCalendar()
    {
        _clock.Now = Local(2026, 10, 25, 12);
        var service = await Start();
        service.SetMode("halloween", "off");
        _scripts.Calls.Clear();

        service.SetMode("halloween", "auto");

        Assert.True(service.IsActive("halloween"));
        Assert.Equal(["on_start"], _scripts.Calls.Select(call => call.Function));
        Assert.Null(_props.Get("schedule.event.halloween.mode"));
    }

    [Fact]
    public async Task TheMode_SurvivesARestart()
    {
        _clock.Now = Local(2026, 10, 25, 12);
        var service = await Start();
        service.SetMode("halloween", "off");

        service = await Restart();

        Assert.False(service.IsActive("halloween"));
        Assert.Equal("off", service.Get("halloween")!.Mode);
    }

    [Fact]
    public async Task SetMode_Unknown_IsFalse()
    {
        var service = await Start();

        Assert.False(service.SetMode("nothing", "on"));
        Assert.False(service.SetMode("halloween", "maybe"));
    }

    [Fact]
    public async Task AMissingHookScript_IsHarmless_AndAThrowingOneIsLogged()
    {
        _clock.Now = Local(2026, 10, 25, 12);
        _scripts.Result = ScriptResult.Missing;
        var service = await Start();
        Assert.True(service.IsActive("halloween"));

        _scripts.Throw = true;
        service.SetMode("halloween", "off");

        Assert.False(service.IsActive("halloween"));
    }

    [Fact]
    public async Task Events_ListsEveryEventWithItsModeAndState()
    {
        _clock.Now = Local(2026, 10, 25, 12);
        var service = await Start();
        service.SetMode("winter", "on");

        Assert.Equal(
            [
                new SeasonalEventState("halloween", "Halloween", "10-20", "11-02", "auto", true),
                new SeasonalEventState("winter", "Winter", "12-20", "01-06", "on", true)
            ],
            service.Events
        );
    }

    [Fact]
    public async Task TheTimeZone_DecidesTheDay()
    {
        // 23:30Z on October 19 is already October 20 in the test zone (UTC+2 then), not in UTC.
        _clock.Now = new(2026, 10, 19, 23, 30, 0, TimeSpan.Zero);

        var local = await Start();
        var utc = await Start(TimeZoneInfo.Utc);

        Assert.True(local.IsActive("halloween"));
        Assert.False(utc.IsActive("halloween"));
    }

    [Fact]
    public async Task TheHooks_AreQueuedOnTheLoop_NotRunInsideTheCaller()
    {
        // A hook asked from inside a running Lua script must wait for the next work item of the loop.
        _clock.Now = Local(2026, 10, 25, 12);
        _loop.DeferTryPost = true;

        await Start();

        Assert.Empty(_scripts.Calls);
        _loop.RunDeferred();
        Assert.Equal(["on_start"], _scripts.Calls.Select(call => call.Function));
    }

    [Fact]
    public async Task StartAsync_WorksOnTheLoop()
    {
        await Start();

        Assert.True(_loop.PostedWorkItems > 0);
    }

    [Fact]
    public async Task ATimerThatCannotBeArmed_DoesNotThrowFromTheMidnightCallback()
    {
        _clock.Now = Local(2026, 10, 19, 12);
        await Start();
        _clock.Now = Local(2026, 10, 20);
        _timers.ThrowOnRegister = true;

        var exception = Record.Exception(() => _timers.Fire(Assert.Single(_timers.Timers).Id));

        Assert.Null(exception);
    }

    [Fact]
    public async Task ALogin_CallsOnLoginOfTheActiveEventsOnly()
    {
        _clock.Now = Local(2026, 10, 25, 12);
        await Start();
        _scripts.Calls.Clear();

        await Bus().PublishAsync(new CharacterEnteredWorldEvent(new() { Id = new Serial(2) }));

        var call = Assert.Single(_scripts.Calls);
        Assert.Equal(("halloween", "on_login"), (call.Script, call.Function));
        Assert.Equal(["halloween", "Halloween", 2L], call.Args);
    }

    [Fact]
    public async Task AfterStopAsync_ALoginCallsNothing()
    {
        _clock.Now = Local(2026, 10, 25, 12);
        var service = await Start();
        _scripts.Calls.Clear();
        await service.StopAsync();

        await Bus().PublishAsync(new CharacterEnteredWorldEvent(new() { Id = new Serial(2) }));

        Assert.Empty(_scripts.Calls);
    }

    private IMoongateEventBus Bus()
    {
        if (!_container.IsRegistered<IMoongateEventBus>())
        {
            _container.RegisterMoongateEventBus();
        }

        return _container.Resolve<IMoongateEventBus>();
    }

    private static DateTimeOffset Local(int year, int month, int day, int hour = 0, int minute = 0)
    {
        var local = new DateTime(year, month, day, hour, minute, 0, DateTimeKind.Unspecified);

        return new(local, TestZones.Europe.GetUtcOffset(local));
    }

    private static ScheduleFile Calendar()
    {
        return new()
        {
            Event =
            [
                new() { Id = "halloween", Name = "Halloween", From = "10-20", To = "11-02" },
                new() { Id = "winter", Name = "Winter", From = "12-20", To = "01-06" }
            ]
        };
    }

    private async Task<SeasonalEventService> Start(TimeZoneInfo? zone = null)
    {
        _props = new(_data);
        await _props.StartAsync();
        _timers.Timers.Clear();

        var service = new SeasonalEventService(
            new StubDataLoaderService().With(Calendar()),
            _props,
            _timers,
            _loop,
            Bus(),
            _scripts,
            _clock,
            zone ?? TestZones.Europe
        );
        await service.StartAsync();

        return service;
    }

    private async Task<SeasonalEventService> Restart()
    {
        _data.Upserted.Clear();
        _data.Upserted.Add(new() { Props = new(_props.State.Props) });

        return await Start();
    }

    // Moves the clock to the moment of the timer and fires it.
    private void FireTimer(DateTimeOffset at)
    {
        _clock.Now = at;
        _timers.Fire(Assert.Single(_timers.Timers).Id);
    }
}
