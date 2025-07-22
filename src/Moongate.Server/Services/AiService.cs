using Microsoft.Extensions.ObjectPool;
using Moongate.Core.Server.Interfaces.Services;
using Moongate.UO.Data.Contexts;
using Moongate.UO.Data.Interfaces.Ai;
using Moongate.UO.Data.Interfaces.Services;
using Moongate.UO.Data.Persistence.Entities;
using Moongate.UO.Data.Types;
using Serilog;

namespace Moongate.Server.Services;

public class AiService : IAiService
{
    private readonly ILogger _logger = Log.ForContext<AiService>();


    private readonly Dictionary<string, IAiBrainAction> _brains = new();


    private readonly IMobileService _mobileService;

    private readonly ObjectPool<AiContext> _aiContextPool =
        new DefaultObjectPool<AiContext>(new DefaultPooledObjectPolicy<AiContext>());


    private readonly ITimerService _timerService;

    public AiService(IMobileService mobileService, ITimerService timerService)
    {
        _mobileService = mobileService;
        _timerService = timerService;

        _mobileService.MobileAdded += MobileAdded;
    }

    private void MobileAdded(UOMobileEntity mobile)
    {
        if (mobile.IsPlayer)
        {
            return;
        }

        _logger.Debug("Mobile added: {Mobile} attaching brain {Brain}", mobile.Id, mobile.BrainId);

        if (string.IsNullOrEmpty(mobile.BrainId) || !_brains.TryGetValue(mobile.BrainId, out var brainAction))
        {
            _logger.Warning("No brain found for mobile {Mobile}.", mobile.Id);
            return;
        }

        mobile.ChatMessageReceived += MobileOnChatMessageReceived;

        _timerService.RegisterTimer(
            $"ai_brain_{mobile.BrainId}-{mobile.Name}",
            1000,
            () => { ProcessAiLogic(mobile, brainAction); },
            1000,
            true
        );
    }

    private void MobileOnChatMessageReceived(
        UOMobileEntity? self, UOMobileEntity? sender, ChatMessageType messageType, short hue, string text, int graphic,
        int font
    )
    {
        _logger.Debug("Processing text message for mobile {Mobile} with brain {Brain}", self.Id, sender.BrainId);

        var brainAction = _brains.GetValueOrDefault(self.BrainId);

        if (brainAction == null)
        {
            _logger.Warning("No brain action found for mobile {Mobile} with brain {Brain}", self.Id, sender.BrainId);
            return;
        }

        using var aiContext = _aiContextPool.Get();

        aiContext.InitializeContext(self);

        brainAction.ReceiveSpeech(aiContext, text, sender);
    }


    private void ProcessAiLogic(UOMobileEntity mobile, IAiBrainAction brainAction)
    {
        _logger.Debug("Processing AI for mobile {Mobile} with brain {Brain}", mobile.Id, mobile.BrainId);
        using var aiContext = _aiContextPool.Get();
        aiContext.InitializeContext(mobile);

        brainAction.Execute(aiContext);
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

        _brains[brainId] = brainAction;
        _logger.Information("Added brain with ID {BrainId}.", brainId);
    }

    public void Dispose()
    {
    }
}
