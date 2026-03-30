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
using Moongate.UO.Data.Ids;
using Moongate.UO.Data.Persistence.Entities;

namespace Moongate.Server.Services.Interaction;

[RegisterGameEventListener]
public sealed class HealerResurrectionOfferListener
    : IMoongateService,
      IGameEventListener<MobileAddedInWorldEvent>,
      IGameEventListener<MobileAppearanceChangedEvent>,
      IGameEventListener<MobilePositionChangedEvent>
{
    private const int OfferCooldownSeconds = 5;
    private const int HealerOfferRange = 4;
    private const string HealerResurrectionSource = "healer";

    private readonly ISpatialWorldService _spatialWorldService;
    private readonly IGameNetworkSessionService _gameNetworkSessionService;
    private readonly IResurrectionOfferService _resurrectionOfferService;
    private readonly ConcurrentDictionary<Serial, (Serial SourceId, DateTimeOffset OfferedAtUtc)> _activeHealerByPlayerId = new();

    public HealerResurrectionOfferListener(
        ISpatialWorldService spatialWorldService,
        IGameNetworkSessionService gameNetworkSessionService,
        IResurrectionOfferService resurrectionOfferService
    )
    {
        _spatialWorldService = spatialWorldService;
        _gameNetworkSessionService = gameNetworkSessionService;
        _resurrectionOfferService = resurrectionOfferService;
    }

    public async Task HandleAsync(MobileAddedInWorldEvent gameEvent, CancellationToken cancellationToken = default)
    {
        if (IsGhostPlayer(gameEvent.Mobile))
        {
            await EvaluatePlayerAsync(gameEvent.Mobile, cancellationToken);

            return;
        }

        if (IsHealerSource(gameEvent.Mobile))
        {
            await EvaluateNearbyGhostPlayersAsync(gameEvent.Mobile, cancellationToken);
        }
    }

    public Task HandleAsync(MobileAppearanceChangedEvent gameEvent, CancellationToken cancellationToken = default)
        => EvaluatePlayerAsync(gameEvent.Mobile, cancellationToken);

    public async Task HandleAsync(MobilePositionChangedEvent gameEvent, CancellationToken cancellationToken = default)
    {
        var mobile = ResolveMobile(gameEvent);

        if (mobile is null)
        {
            return;
        }

        if (IsGhostPlayer(mobile))
        {
            await EvaluatePlayerAsync(mobile, cancellationToken);

            return;
        }

        if (IsHealerSource(mobile))
        {
            await EvaluateNearbyGhostPlayersAsync(mobile, cancellationToken);
        }
    }

    public Task StartAsync()
        => Task.CompletedTask;

    public Task StopAsync()
        => Task.CompletedTask;

    private async Task EvaluateNearbyGhostPlayersAsync(UOMobileEntity healer, CancellationToken cancellationToken)
    {
        foreach (var candidate in _spatialWorldService.GetNearbyMobiles(healer.Location, HealerOfferRange, healer.MapId))
        {
            if (!IsGhostPlayer(candidate))
            {
                continue;
            }

            await EvaluatePlayerAsync(candidate, cancellationToken);
        }
    }

    private async Task EvaluatePlayerAsync(UOMobileEntity player, CancellationToken cancellationToken)
    {
        if (!IsGhostPlayer(player))
        {
            _activeHealerByPlayerId.TryRemove(player.Id, out _);

            return;
        }

        if (!_gameNetworkSessionService.TryGetByCharacterId(player.Id, out var session) || session.Character is null)
        {
            _activeHealerByPlayerId.TryRemove(player.Id, out _);

            return;
        }

        var healer = FindNearbyHealer(player);

        if (healer is null)
        {
            _activeHealerByPlayerId.TryRemove(player.Id, out _);

            return;
        }

        var utcNow = DateTimeOffset.UtcNow;

        if (
            _activeHealerByPlayerId.TryGetValue(player.Id, out var activeHealer) &&
            activeHealer.SourceId == healer.Id &&
            utcNow < activeHealer.OfferedAtUtc.AddSeconds(OfferCooldownSeconds)
        )
        {
            return;
        }

        var created = await _resurrectionOfferService.TryCreateOfferAsync(
            session.SessionId,
            player.Id,
            ResurrectionOfferSourceType.Healer,
            healer.Id,
            healer.MapId,
            healer.Location,
            cancellationToken
        );

        if (created)
        {
            _activeHealerByPlayerId[player.Id] = (healer.Id, utcNow);
        }
    }

    private UOMobileEntity? FindNearbyHealer(UOMobileEntity player)
        => _spatialWorldService
           .GetNearbyMobiles(player.Location, HealerOfferRange, player.MapId)
           .Where(IsHealerSource)
           .OrderBy(candidate => GetDistanceSquared(player, candidate))
           .FirstOrDefault();

    private UOMobileEntity? ResolveMobile(MobilePositionChangedEvent gameEvent)
    {
        if (_gameNetworkSessionService.TryGetByCharacterId(gameEvent.MobileId, out var session) &&
            session.Character?.Id == gameEvent.MobileId)
        {
            return session.Character;
        }

        return _spatialWorldService
               .GetNearbyMobiles(gameEvent.NewLocation, 0, gameEvent.MapId)
               .FirstOrDefault(candidate => candidate.Id == gameEvent.MobileId);
    }

    private static long GetDistanceSquared(UOMobileEntity player, UOMobileEntity healer)
    {
        var deltaX = player.Location.X - healer.Location.X;
        var deltaY = player.Location.Y - healer.Location.Y;

        return (long)deltaX * deltaX + (long)deltaY * deltaY;
    }

    private static bool IsGhostPlayer(UOMobileEntity mobile)
        => mobile.IsPlayer && !mobile.IsAlive;

    private static bool IsHealerSource(UOMobileEntity mobile)
        => mobile.TryGetCustomString(MobileCustomParamKeys.Interaction.ResurrectionSource, out var source) &&
           string.Equals(source, HealerResurrectionSource, StringComparison.OrdinalIgnoreCase);
}
