using System.Diagnostics;
using DryIoc;
using Moongate.Core.Persistence.Interfaces.Services;
using Moongate.Core.Server.Interfaces.Services;
using Moongate.UO.Data.Events.System;
using Moongate.UO.Data.Interfaces.Services;
using Moongate.UO.Interfaces.Services;
using Serilog;

namespace Moongate.Server.Services;

public class PersistenceService : IPersistenceService
{
    private readonly ILogger _logger = Log.ForContext<PersistenceService>();

    private DateTime _lastSaveTime = DateTime.MinValue;

    private readonly IEventBusService _eventBusService;

    private readonly ITimerService _timerService;

    private readonly IContainer _container;

    public PersistenceService(ITimerService timerService, IContainer container, IEventBusService eventBusService)
    {
        _timerService = timerService;
        _container = container;
        _eventBusService = eventBusService;

        _eventBusService.Subscribe<SavePersistenceRequestEvent>(async @event => RequestSave());
    }

    private async Task SaveRequestAsync()
    {
        _eventBusService.PublishAsync(new SavePersistenceStartingEvent());

        await Task.Delay(3000);


        var startSw = Stopwatch.GetTimestamp();

        var persistenceLoadSaves = new List<IPersistenceLoadSave>
        {
            _container.Resolve<IAccountService>(),
            _container.Resolve<IItemService>(),
            _container.Resolve<IMobileService>()
        };

        foreach (var persistenceLoadSave in persistenceLoadSaves)
        {
            await persistenceLoadSave.SaveAsync();
        }

        var elapsed = Stopwatch.GetElapsedTime(startSw);

        _logger.Information("Persistence files saved in {ElapsedMilliseconds} ms", elapsed.TotalMilliseconds);

        _eventBusService.PublishAsync(new SavePersistenceDoneEvent(elapsed.TotalMicroseconds));
    }

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        _timerService.RegisterTimer(
            "save_persistence_files",
            (int)TimeSpan.FromMinutes(5).TotalMicroseconds,
            async () => { await SaveRequestAsync(); },
            (int)TimeSpan.FromMinutes(5).TotalMicroseconds,
            true
        );
    }

    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        _lastSaveTime = DateTime.MinValue;
        await SaveRequestAsync();
    }

    public void RequestSave()
    {
        if (DateTime.UtcNow - _lastSaveTime < TimeSpan.FromMinutes(1))
        {
            _logger.Debug("Save request ignored, last save was less than 1 minutes ago");
            return;
        }

        _lastSaveTime = DateTime.UtcNow;

        _logger.Information("Save request received, starting save process...");

        _timerService.RegisterTimer(
            "request_save_persistence_files",
            1000,
            async () => { await SaveRequestAsync(); }, 30
        );

    }

    public void Dispose()
    {
    }
}
