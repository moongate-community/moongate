using Moongate.Server.Ultima.Data.Schedule;
using Moongate.Server.Ultima.Entities.World;
using Moongate.Server.Ultima.Services;
using Moongate.Tests.TestSupport.Persistence;
using Moongate.Tests.TestSupport.Scripting;
using Moongate.Tests.TestSupport.Timing;
using Moongate.Tests.TestSupport.Ultima.Loaders;

namespace Moongate.Tests.TestSupport.Ultima.Schedule;

/// <summary>
///     The real schedule and seasonal event services over recording doubles, with one nightly shutdown task and the
///     events halloween (10-20 to 11-02) and winter (12-20 to 01-06); the clock starts on 2026-10-25 12:00Z.
/// </summary>
public sealed class ScheduleServices
{
    private ScheduleServices(ScheduleService schedule, SeasonalEventService events, RecordingEventScriptService scripts)
    {
        Schedule = schedule;
        Events = events;
        Scripts = scripts;
    }

    public ScheduleService Schedule { get; }

    public SeasonalEventService Events { get; }

    public RecordingEventScriptService Scripts { get; }

    public static async Task<ScheduleServices> CreateAsync()
    {
        var clock = new SettableClock { Now = new(2026, 10, 25, 12, 0, 0, TimeSpan.Zero) };
        var scripts = new RecordingEventScriptService();
        var props = new WorldPropsService(new RecordingDataAccess<WorldStateEntity>());
        await props.StartAsync();
        var file = new ScheduleFile
        {
            Task =
            [
                new()
                {
                    Id = "nightly_restart", When = new() { Every = "day", At = "04:00" }, Action = "shutdown"
                }
            ],
            Event =
            [
                new() { Id = "halloween", Name = "Halloween", From = "10-20", To = "11-02" },
                new() { Id = "winter", Name = "Winter", From = "12-20", To = "01-06" }
            ]
        };
        var data = new StubDataLoaderService().With(file);
        var timers = new RecordingTimerService();
        var schedule = new ScheduleService(
            data,
            timers,
            new RecordingBroadcastService(),
            new RecordingShutdownService(),
            scripts,
            clock,
            TimeZoneInfo.Utc
        );
        var events = new SeasonalEventService(data, props, timers, new StubGameLoop(), scripts, clock, TimeZoneInfo.Utc);
        await schedule.StartAsync();
        await events.StartAsync();

        return new(schedule, events, scripts);
    }
}
