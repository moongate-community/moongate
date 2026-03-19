using Moongate.Abstractions.Interfaces.Services.Base;
using Moongate.Server.Attributes;
using Moongate.Server.Data.Events.Characters;
using Moongate.Server.Interfaces.Characters;
using Moongate.Server.Interfaces.Services.Characters;
using Moongate.Server.Interfaces.Services.Events;
using Moongate.Server.Interfaces.Services.Sessions;

namespace Moongate.Server.Handlers;

[RegisterGameEventListener]
public class PlayerLoginWorldSyncHandler
    : IGameEventListener<PlayerCharacterLoggedInEvent>,
      IMoongateService
{
    private readonly IGameNetworkSessionService _gameNetworkSessionService;
    private readonly ICharacterService _characterService;
    private readonly IPlayerLoginWorldSyncService _playerLoginWorldSyncService;

    public PlayerLoginWorldSyncHandler(
        IGameNetworkSessionService gameNetworkSessionService,
        ICharacterService characterService,
        IPlayerLoginWorldSyncService playerLoginWorldSyncService
    )
    {
        _gameNetworkSessionService = gameNetworkSessionService;
        _characterService = characterService;
        _playerLoginWorldSyncService = playerLoginWorldSyncService;
    }

    public async Task HandleAsync(PlayerCharacterLoggedInEvent gameEvent, CancellationToken cancellationToken = default)
    {
        if (!_gameNetworkSessionService.TryGet(gameEvent.SessionId, out var session))
        {
            return;
        }

        var mobileEntity = session.CharacterId == gameEvent.CharacterId && session.Character is not null
                               ? session.Character
                               : await _characterService.GetCharacterAsync(gameEvent.CharacterId);

        if (mobileEntity is null)
        {
            return;
        }

        await _playerLoginWorldSyncService.SyncAsync(session, mobileEntity, cancellationToken);
    }

    public Task StartAsync()
        => Task.CompletedTask;

    public Task StopAsync()
        => Task.CompletedTask;
}
