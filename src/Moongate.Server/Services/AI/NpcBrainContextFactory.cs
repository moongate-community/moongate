using Moongate.Core.Geometry;
using Moongate.Persistence.Entities;
using Moongate.Server.Abstractions.Data.AI;
using Moongate.Server.Abstractions.Interfaces.Accounts;
using Moongate.Server.Abstractions.Interfaces.World;

namespace Moongate.Server.Services.AI;

public sealed class NpcBrainContextFactory
{
    private readonly ISpatialIndexService _spatial;
    private readonly ISessionManager _sessions;
    private readonly TimeProvider _timeProvider;

    public NpcBrainContextFactory(
        ISpatialIndexService spatial,
        ISessionManager sessions,
        TimeProvider timeProvider
    )
    {
        _spatial = spatial;
        _sessions = sessions;
        _timeProvider = timeProvider;
    }

    public BrainContext Create(
        MobileEntity owner,
        int homeMapId,
        Point3D homePosition,
        BrainDescriptor descriptor
    )
    {
        var nearby = _spatial
                     .GetMobilesInRange(owner.MapId, owner.Position, descriptor.PerceptionRange)
                     .Where(mobile => mobile.Id != owner.Id)
                     .Select(ToSnapshot)
                     .OrderBy(snapshot => snapshot.Id)
                     .ToArray();

        return new(
            _timeProvider.GetUtcNow(),
            ToSnapshot(owner),
            homeMapId,
            homePosition,
            nearby
        );
    }

    private BrainMobileSnapshot ToSnapshot(MobileEntity mobile)
        => new(
            mobile.Id,
            mobile.Name,
            _sessions.IsCharacterPlayed(mobile.Id),
            mobile.MapId,
            mobile.Position,
            mobile.Hits,
            mobile.HitsMax,
            mobile.Warmode,
            mobile.CombatantId,
            mobile.Criminal,
            mobile.Kills
        );
}
