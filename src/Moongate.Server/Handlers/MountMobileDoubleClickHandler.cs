using Moongate.Abstractions.Interfaces.Services.Base;
using Moongate.Server.Attributes;
using Moongate.Server.Data.Events.Characters;
using Moongate.Server.Data.Internal.Interaction;
using Moongate.Server.Interfaces.Services.Entities;
using Moongate.Server.Interfaces.Services.Events;
using Moongate.Server.Interfaces.Services.Packets;
using Moongate.Server.Interfaces.Services.Sessions;
using Moongate.UO.Data.Persistence.Entities;

namespace Moongate.Server.Handlers;

[RegisterGameEventListener]
public sealed class MountMobileDoubleClickHandler : IGameEventListener<MobileDoubleClickEvent>, IMoongateService
{
    private const string IsMountKey = "is_mount";
    private const int MountInteractionRange = 2;

    private readonly IMobileService _mobileService;
    private readonly IGameNetworkSessionService _gameNetworkSessionService;
    private readonly IOutgoingPacketQueue _outgoingPacketQueue;

    public MountMobileDoubleClickHandler(
        IMobileService mobileService,
        IGameNetworkSessionService gameNetworkSessionService,
        IOutgoingPacketQueue outgoingPacketQueue
    )
    {
        _mobileService = mobileService;
        _gameNetworkSessionService = gameNetworkSessionService;
        _outgoingPacketQueue = outgoingPacketQueue;
    }

    public async Task HandleAsync(MobileDoubleClickEvent gameEvent, CancellationToken cancellationToken = default)
    {
        if (!_gameNetworkSessionService.TryGet(gameEvent.SessionId, out var session) || session.Character is null)
        {
            return;
        }

        var rider = session.Character;
        var mount = await _mobileService.GetAsync(gameEvent.MobileSerial, cancellationToken);

        if (mount is null || !IsMount(mount))
        {
            return;
        }

        if (rider.MapId != mount.MapId ||
            !rider.Location.InRange(mount.Location, MountInteractionRange) ||
            rider.MountedMobileId != 0 ||
            mount.RiderMobileId != 0)
        {
            return;
        }

        if (!await _mobileService.TryMountAsync(rider.Id, mount.Id, cancellationToken))
        {
            return;
        }

        MountedSelfRefreshHelper.Refresh(session, _outgoingPacketQueue, rider, mount, true);
    }

    public Task StartAsync()
        => Task.CompletedTask;

    public Task StopAsync()
        => Task.CompletedTask;

    private static bool IsMount(UOMobileEntity mobile)
        => mobile.TryGetCustomBoolean(IsMountKey, out var isMount)
               ? isMount
               : mobile.TryGetCustomString(IsMountKey, out var stringValue) &&
                 bool.TryParse(stringValue, out var parsed) &&
                 parsed;
}
