using Moongate.Core.Server.Interfaces.Services;
using Moongate.UO.Data.Interfaces.Ai;
using Moongate.UO.Data.Interfaces.Services;
using Moongate.UO.Data.Persistence.Entities;
using Serilog;

namespace Moongate.Server.Services;

public class AiService : IAiService
{
    private readonly ILogger _logger = Log.ForContext<AiService>();

    private double _accumulatedTickDurationMs = 0;

    private readonly Dictionary<string, Type> _brains = new();

    private readonly IEventLoopService _eventLoopService;

    private readonly IMobileService _mobileService;

    public AiService(IEventLoopService eventLoopService, IMobileService mobileService)
    {
        _eventLoopService = eventLoopService;
        _mobileService = mobileService;

        _mobileService.MobileAdded += MobileAdded;

        _eventLoopService.OnTick += OnEventLoopTick;
    }

    private void MobileAdded(UOMobileEntity mobile)
    {
        _logger.Debug("Mobile added: {Mobile} attaching brain {Brain}", mobile.Id, mobile.BrainId);
    }

    private void OnEventLoopTick(double tickDurationMs)
    {
        _accumulatedTickDurationMs += tickDurationMs;

        if (_accumulatedTickDurationMs >= 1000) // Process every second
        {
            _logger.Information("Processing AI logic...");

            _accumulatedTickDurationMs = 0;
        }
    }


    public Task StartAsync(CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }

    public void AddBrain(string brainId, IAiBrainAction brainAction)
    {
        if (_brains.ContainsKey(brainId))
        {
            _logger.Warning("Brain with ID {BrainId} already exists. Overwriting.", brainId);
        }

        _brains[brainId] = brainAction.GetType();
        _logger.Information("Added brain with ID {BrainId}.", brainId);
    }

    public void Dispose()
    {
    }
}
