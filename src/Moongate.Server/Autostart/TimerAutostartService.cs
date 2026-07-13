using Serilog;
using SquidStd.Core.Interfaces.Timing;
using SquidStd.Persistence.Abstractions.Interfaces.Persistence;

namespace Moongate.Server.Autostart;

public class TimerAutostartService
{
    private readonly ILogger _logger = Log.ForContext<TimerAutostartService>();

    private readonly ITimerService _timerService;

    private readonly IPersistenceService _persistenceService;

    public TimerAutostartService(ITimerService timerService, IPersistenceService persistenceService)
    {
        _timerService = timerService;
        _persistenceService = persistenceService;
    }

    public void InitDefaultTimers()
    {
        _logger.Information("Initializing default timers...");

        // Example: Initialize a timer that runs every 5 minutes
        _timerService.RegisterTimer(
            "persistence_save",
            TimeSpan.FromMinutes(10),
            async () =>
            {
                await _persistenceService
                    .SaveSnapshotAsync();

            }
        );

        _logger.Information("Default timers initialized.");
    }
}
