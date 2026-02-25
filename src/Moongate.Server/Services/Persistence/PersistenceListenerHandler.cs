using Humanizer;
using Moongate.Abstractions.Interfaces.Services.Base;
using Moongate.Server.Data.Events.Persistence;
using Moongate.Server.Interfaces.Services.Events;
using Moongate.Server.Interfaces.Services.Speech;

namespace Moongate.Server.Services.Persistence;

public class PersistenceListenerHandler
    : IGameEventListener<DatabaseSavingStartEvent>, IGameEventListener<DatabaseSavedEvent>, IMoongateService
{
    private readonly ISpeechService _speechService;
    private readonly IGameEventBusService _gameEventBusService;

    public PersistenceListenerHandler(ISpeechService speechService, IGameEventBusService gameEventBusService)
    {
        _speechService = speechService;
        _gameEventBusService = gameEventBusService;
    }

    public async Task HandleAsync(DatabaseSavingStartEvent gameEvent, CancellationToken cancellationToken = default)
    {
        await _speechService.BroadcastFromServerAsync("Saving world...");
    }

    public async Task HandleAsync(DatabaseSavedEvent gameEvent, CancellationToken cancellationToken = default)
    {
        await _speechService.BroadcastFromServerAsync(
            $"World saved in {gameEvent.ElapsedMilliseconds.Milliseconds()} seconds."
        );
    }

    public Task StartAsync()
    {
        _gameEventBusService.RegisterListener<DatabaseSavedEvent>(this);
        _gameEventBusService.RegisterListener<DatabaseSavingStartEvent>(this);

        return Task.CompletedTask;
    }

    public Task StopAsync()
    {
        return Task.CompletedTask;
    }
}
