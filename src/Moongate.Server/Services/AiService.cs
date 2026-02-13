using Microsoft.Extensions.ObjectPool;
using Moongate.Core.Server.Interfaces.Services;
using Moongate.UO.Data.Contexts;
using Moongate.UO.Data.Ids;
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

    private const double _aiTickInterval = 500;

    private readonly IMobileService _mobileService;

    private readonly ObjectPool<AiContext> _aiContextPool =
        new DefaultObjectPool<AiContext>(new DefaultPooledObjectPolicy<AiContext>());

    private readonly ITimerService _timerService;

    private readonly Dictionary<Serial, AiContext> _contexts = new();

    public AiService(IMobileService mobileService, ITimerService timerService)
    {
        _mobileService = mobileService;
        _timerService = timerService;

        _mobileService.MobileAdded += MobileAdded;
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

    public void Dispose() { }

    public Task StartAsync(CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public Task StopAsync(CancellationToken cancellationToken = default)
        => Task.CompletedTask;

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

        var aiContext = _aiContextPool.Get();

        aiContext.InitializeContext(mobile);

        _contexts[mobile.Id] = aiContext;

        _timerService.RegisterTimer(
            $"ai_brain_{mobile.BrainId}-{mobile.Name}",
            _aiTickInterval,
            () => { ProcessAiLogic(mobile, brainAction, _aiTickInterval); },
            _aiTickInterval,
            true
        );
    }

    private void MobileOnChatMessageReceived(
        UOMobileEntity? self,
        UOMobileEntity? sender,
        ChatMessageType messageType,
        short hue,
        string text,
        int graphic,
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

        try
        {
            brainAction.ReceiveSpeech(aiContext, text, sender);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error processing speech for mobile {Mobile} with brain {Brain}", self.Id, sender.BrainId);
        }
    }

    private void ProcessAiLogic(UOMobileEntity mobile, IAiBrainAction brainAction, double elapsedTime = 0)
    {
        _logger.Debug("Processing AI for mobile {Mobile} with brain {Brain}", mobile.Id, mobile.BrainId);

        var aiContext = _contexts.GetValueOrDefault(mobile.Id) ?? _aiContextPool.Get();

        aiContext.InitializeContext(mobile);

        aiContext.IncrementElapsedTime(elapsedTime);

        brainAction.Execute(aiContext);
    }
}
