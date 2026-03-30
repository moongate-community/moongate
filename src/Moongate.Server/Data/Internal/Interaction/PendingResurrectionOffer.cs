using Moongate.Server.Types.Interaction;
using Moongate.UO.Data.Geometry;
using Moongate.UO.Data.Ids;

namespace Moongate.Server.Data.Internal.Interaction;

/// <summary>
/// Internal in-memory state for a pending resurrection confirmation.
/// </summary>
internal sealed class PendingResurrectionOffer
{
    public PendingResurrectionOffer(
        long sessionId,
        Serial characterId,
        ResurrectionOfferSourceType sourceType,
        Serial sourceSerial,
        int mapId,
        Point3D sourceLocation,
        DateTimeOffset expiresAtUtc
    )
    {
        SessionId = sessionId;
        CharacterId = characterId;
        SourceType = sourceType;
        SourceSerial = sourceSerial;
        MapId = mapId;
        SourceLocation = sourceLocation;
        ExpiresAtUtc = expiresAtUtc;
    }

    public long SessionId { get; }

    public Serial CharacterId { get; }

    public ResurrectionOfferSourceType SourceType { get; }

    public Serial SourceSerial { get; }

    public int MapId { get; }

    public Point3D SourceLocation { get; }

    public DateTimeOffset ExpiresAtUtc { get; }

    public bool IsExpired(DateTimeOffset utcNow)
        => utcNow >= ExpiresAtUtc;
}
