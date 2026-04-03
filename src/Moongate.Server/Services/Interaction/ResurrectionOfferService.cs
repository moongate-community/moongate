using System.Collections.Concurrent;
using Moongate.Scripting.Interfaces;
using Moongate.Server.Data.Internal.Interaction;
using Moongate.Server.Interfaces.Services.Interaction;
using Moongate.Server.Interfaces.Services.Sessions;
using Moongate.Server.Types.Interaction;
using Moongate.UO.Data.Geometry;
using Moongate.UO.Data.Ids;

namespace Moongate.Server.Services.Interaction;

/// <summary>
/// Stores in-memory resurrection offers and coordinates acceptance flow for a session.
/// </summary>
public sealed class ResurrectionOfferService : IResurrectionOfferService
{
    private const int AnkhRange = 2;
    private const int HealerRange = 4;
    private const int OfferDurationSeconds = 30;
    private const string ResurrectionOfferScriptFunction = "on_resurrection_offer";

    private readonly IGameNetworkSessionService _gameNetworkSessionService;
    private readonly IResurrectionService _resurrectionService;
    private readonly IScriptEngineService _scriptEngineService;
    private readonly TimeProvider _timeProvider;
    private readonly ConcurrentDictionary<long, PendingResurrectionOffer> _pendingOffers = new();

    public ResurrectionOfferService(
        IGameNetworkSessionService gameNetworkSessionService,
        IResurrectionService resurrectionService,
        IScriptEngineService scriptEngineService,
        TimeProvider? timeProvider = null
    )
    {
        _gameNetworkSessionService = gameNetworkSessionService;
        _resurrectionService = resurrectionService;
        _scriptEngineService = scriptEngineService;
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public void Decline(long sessionId)
        => _pendingOffers.TryRemove(sessionId, out _);

    public Task<bool> TryCreateOfferAsync(
        long sessionId,
        Serial characterId,
        ResurrectionOfferSourceType sourceType,
        CancellationToken cancellationToken = default
    )
    {
        _ = cancellationToken;

        if (
            !_gameNetworkSessionService.TryGet(sessionId, out var session) ||
            session.CharacterId != characterId ||
            session.Character is null
        )
        {
            return Task.FromResult(false);
        }

        return TryCreateOfferAsync(
            sessionId,
            characterId,
            sourceType,
            session.Character.Id,
            session.Character.MapId,
            session.Character.Location,
            cancellationToken
        );
    }

    public Task<bool> TryCreateOfferAsync(
        long sessionId,
        Serial characterId,
        ResurrectionOfferSourceType sourceType,
        Serial sourceSerial,
        int mapId,
        Point3D sourceLocation,
        CancellationToken cancellationToken = default
    )
    {
        _ = cancellationToken;

        if (
            sourceSerial == Serial.Zero ||
            !_gameNetworkSessionService.TryGet(sessionId, out var session) ||
            session.CharacterId != characterId ||
            session.Character is null ||
            session.Character.IsAlive ||
            session.Character.MapId != mapId ||
            !IsWithinSourceRange(session.Character.Location, sourceLocation, sourceType)
        )
        {
            return Task.FromResult(false);
        }

        var expiresAtUtc = _timeProvider.GetUtcNow().AddSeconds(OfferDurationSeconds);
        var offer = new PendingResurrectionOffer(
            sessionId,
            characterId,
            sourceType,
            sourceSerial,
            mapId,
            sourceLocation,
            expiresAtUtc
        );
        _pendingOffers[sessionId] = offer;

        _scriptEngineService.CallFunction(
            ResurrectionOfferScriptFunction,
            sessionId,
            (uint)characterId,
            ToSourceTypeString(sourceType)
        );

        return Task.FromResult(true);
    }

    public async Task<bool> TryAcceptAsync(long sessionId, CancellationToken cancellationToken = default)
    {
        if (!_pendingOffers.TryRemove(sessionId, out var pendingOffer))
        {
            return false;
        }

        if (pendingOffer.IsExpired(_timeProvider.GetUtcNow()))
        {
            return false;
        }

        return await _resurrectionService.TryResurrectAsync(
            pendingOffer.SessionId,
            pendingOffer.CharacterId,
            pendingOffer.SourceType,
            pendingOffer.SourceSerial,
            pendingOffer.MapId,
            pendingOffer.SourceLocation,
            cancellationToken
        );
    }

    private static string ToSourceTypeString(ResurrectionOfferSourceType sourceType)
        => sourceType switch
        {
            ResurrectionOfferSourceType.Healer => "healer",
            ResurrectionOfferSourceType.Ankh => "ankh",
            _ => sourceType.ToString().ToLowerInvariant()
        };

    private static bool IsWithinSourceRange(
        Point3D characterLocation,
        Point3D sourceLocation,
        ResurrectionOfferSourceType sourceType
    )
    {
        var allowedRange = sourceType switch
        {
            ResurrectionOfferSourceType.Healer => HealerRange,
            ResurrectionOfferSourceType.Ankh => AnkhRange,
            _ => 0
        };

        return characterLocation.GetDistance(sourceLocation) <= allowedRange;
    }
}
