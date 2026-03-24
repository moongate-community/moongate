using Moongate.Abstractions.Interfaces.Services.Base;
using Moongate.Server.Attributes;
using Moongate.Server.Data.Events.Characters;
using Moongate.Server.Interfaces.Services.Entities;
using Moongate.Server.Interfaces.Services.Events;
using Moongate.Server.Interfaces.Services.Packets;
using Moongate.Server.Interfaces.Services.Sessions;
using Moongate.UO.Data.Ids;

namespace Moongate.Server.Handlers;

[RegisterGameEventListener]
public sealed class CharacterStatLockHandler
    : IGameEventListener<StatLockChangeRequestedEvent>,
      IMoongateService
{
    private readonly IMobileService _mobileService;
    private readonly IGameNetworkSessionService _gameNetworkSessionService;

    public CharacterStatLockHandler(
        IOutgoingPacketQueue outgoingPacketQueue,
        IMobileService mobileService,
        IGameNetworkSessionService gameNetworkSessionService
    )
    {
        _ = outgoingPacketQueue;
        _mobileService = mobileService;
        _gameNetworkSessionService = gameNetworkSessionService;
    }

    public async Task HandleAsync(StatLockChangeRequestedEvent gameEvent, CancellationToken cancellationToken = default)
    {
        if (!_gameNetworkSessionService.TryGet(gameEvent.SessionId, out var session))
        {
            return;
        }

        var mobile = session.Character;

        if (mobile is null && session.CharacterId != Serial.Zero)
        {
            mobile = await _mobileService.GetAsync(session.CharacterId, cancellationToken);
        }

        if (mobile is null)
        {
            return;
        }

        mobile.SetStatLock(gameEvent.Stat, gameEvent.LockState);
        await _mobileService.CreateOrUpdateAsync(mobile, cancellationToken);
        session.Character = mobile;
    }

    public Task StartAsync()
        => Task.CompletedTask;

    public Task StopAsync()
        => Task.CompletedTask;
}
