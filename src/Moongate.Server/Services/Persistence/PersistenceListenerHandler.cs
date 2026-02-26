using Humanizer;
using Moongate.Abstractions.Interfaces.Services.Base;
using Moongate.Server.Attributes;
using Moongate.Server.Data.Events.Persistence;
using Moongate.Server.Interfaces.Services.Events;
using Moongate.Server.Interfaces.Services.Speech;

namespace Moongate.Server.Services.Persistence;

[RegisterGameEventListener]
public class PersistenceListenerHandler
    : IGameEventListener<DatabaseSavingStartEvent>, IGameEventListener<DatabaseSavedEvent>, IMoongateService
{
    private readonly ISpeechService _speechService;

    public PersistenceListenerHandler(ISpeechService speechService)
    {
        _speechService = speechService;
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
        => Task.CompletedTask;

    public Task StopAsync()
        => Task.CompletedTask;
}
