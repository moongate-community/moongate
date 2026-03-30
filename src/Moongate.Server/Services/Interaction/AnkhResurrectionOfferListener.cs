using System.Collections.Concurrent;
using Moongate.Abstractions.Interfaces.Services.Base;
using Moongate.Server.Attributes;
using Moongate.Server.Data.Events.Characters;
using Moongate.Server.Data.Events.Spatial;
using Moongate.Server.Data.Internal.Scripting;
using Moongate.Server.Interfaces.Services.Events;
using Moongate.Server.Interfaces.Services.Interaction;
using Moongate.Server.Interfaces.Services.Sessions;
using Moongate.Server.Interfaces.Services.Spatial;
using Moongate.Server.Types.Interaction;
using Moongate.UO.Data.Ids;
using Moongate.UO.Data.Persistence.Entities;

namespace Moongate.Server.Services.Interaction;

[RegisterGameEventListener]
public sealed class AnkhResurrectionOfferListener
    : IMoongateService,
      IGameEventListener<MobileAddedInWorldEvent>,
      IGameEventListener<MobileAppearanceChangedEvent>,
      IGameEventListener<MobilePositionChangedEvent>
{
    private const int AnkhOfferRange = 2;
    private const int OfferCooldownSeconds = 5;
    private const string AnkhResurrectionSource = "ankh";

    private readonly ISpatialWorldService _spatialWorldService;
    private readonly IGameNetworkSessionService _gameNetworkSessionService;
    private readonly IResurrectionOfferService _resurrectionOfferService;
    private readonly ConcurrentDictionary<Serial, (Serial SourceId, DateTimeOffset OfferedAtUtc)> _activeAnkhByPlayerId = new();

    public AnkhResurrectionOfferListener(
        ISpatialWorldService spatialWorldService,
        IGameNetworkSessionService gameNetworkSessionService,
        IResurrectionOfferService resurrectionOfferService
    )
    {
        _spatialWorldService = spatialWorldService;
        _gameNetworkSessionService = gameNetworkSessionService;
        _resurrectionOfferService = resurrectionOfferService;
    }

    public Task StartAsync()
        => Task.CompletedTask;

    public Task StopAsync()
        => Task.CompletedTask;

    public Task HandleAsync(MobileAppearanceChangedEvent gameEvent, CancellationToken cancellationToken = default)
        => EvaluatePlayerAsync(gameEvent.Mobile, cancellationToken);

    public Task HandleAsync(MobileAddedInWorldEvent gameEvent, CancellationToken cancellationToken = default)
        => EvaluatePlayerAsync(gameEvent.Mobile, cancellationToken);

    public async Task HandleAsync(MobilePositionChangedEvent gameEvent, CancellationToken cancellationToken = default)
    {
        if (!_gameNetworkSessionService.TryGetByCharacterId(gameEvent.MobileId, out var session) || session.Character is null)
        {
            return;
        }

        await EvaluatePlayerAsync(session.Character, cancellationToken);
    }

    private async Task EvaluatePlayerAsync(UOMobileEntity player, CancellationToken cancellationToken)
    {
        if (!IsGhostPlayer(player))
        {
            _activeAnkhByPlayerId.TryRemove(player.Id, out _);

            return;
        }

        if (!_gameNetworkSessionService.TryGetByCharacterId(player.Id, out var session) || session.Character is null)
        {
            _activeAnkhByPlayerId.TryRemove(player.Id, out _);

            return;
        }

        var ankh = FindNearbyAnkh(player);

        if (ankh is null)
        {
            _activeAnkhByPlayerId.TryRemove(player.Id, out _);

            return;
        }

        var utcNow = DateTimeOffset.UtcNow;

        if (
            _activeAnkhByPlayerId.TryGetValue(player.Id, out var activeAnkh) &&
            activeAnkh.SourceId == ankh.Id &&
            utcNow < activeAnkh.OfferedAtUtc.AddSeconds(OfferCooldownSeconds)
        )
        {
            return;
        }

        var created = await _resurrectionOfferService.TryCreateOfferAsync(
            session.SessionId,
            player.Id,
            ResurrectionOfferSourceType.Ankh,
            ankh.Id,
            ankh.MapId,
            ankh.Location,
            cancellationToken
        );

        if (created)
        {
            _activeAnkhByPlayerId[player.Id] = (ankh.Id, utcNow);
        }
    }

    private UOItemEntity? FindNearbyAnkh(UOMobileEntity player)
        => _spatialWorldService
           .GetNearbyItems(player.Location, AnkhOfferRange, player.MapId)
           .Where(IsAnkhSource)
           .OrderBy(candidate => player.Location.GetDistance(candidate.Location))
           .FirstOrDefault();

    private static bool IsGhostPlayer(UOMobileEntity mobile)
        => mobile.IsPlayer && !mobile.IsAlive;

    private static bool IsAnkhSource(UOItemEntity item)
        => item.TryGetCustomString(ItemCustomParamKeys.Interaction.ResurrectionSource, out var source) &&
           string.Equals(source, AnkhResurrectionSource, StringComparison.OrdinalIgnoreCase);
}
