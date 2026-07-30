using Moongate.Core.Extensions;
using Moongate.Core.Interfaces;
using Moongate.Core.Primitives;
using Moongate.Persistence.Entities;
using Moongate.Server.Abstractions.Data.Events;
using Moongate.Server.Abstractions.Interfaces.Mobiles;
using Moongate.Server.Abstractions.Interfaces.World;
using SquidStd.Core.Interfaces.Events;
using SquidStd.Persistence.Abstractions.Interfaces.Persistence;

namespace Moongate.Server.Services.Mobiles;

/// <summary>
/// Default <see cref="IMobileService" />. Carries the loop-affinity guard on every mutation, the way
/// <c>ItemService</c> does — which is what <c>MobileModule</c>'s own comment asked for and could not
/// have while it wrote the store directly.
/// </summary>
public sealed class MobileService : IMobileService
{
    private readonly IEntityStore<MobileEntity, Serial> _mobiles;
    private readonly ISpatialIndexService _spatial;
    private readonly IEventBus _eventBus;
    private readonly ILoopAffinity? _loopAffinity;

    public MobileService(
        IPersistenceService persistenceService,
        ISpatialIndexService spatial,
        IEventBus eventBus,
        ILoopAffinity? loopAffinity = null
    )
    {
        _mobiles = persistenceService.GetStore<MobileEntity, Serial>();
        _spatial = spatial;
        _eventBus = eventBus;
        _loopAffinity = loopAffinity;
    }

    public bool Teleport(Serial mobile, int x, int y, int z)
    {
        _loopAffinity?.AssertOnLoop("mobile.teleport");

        if (_mobiles.GetById(mobile) is not { } entity)
        {
            return false;
        }

        var fromMapId = entity.MapId;
        var fromPosition = entity.Position;

        entity.Position = new(x, y, z);
        _mobiles.UpsertAsync(entity).WaitSync();
        _spatial.AddOrUpdate(entity);

        // Only a real move is worth telling the world about: the brain router and every other
        // subscriber would otherwise wake for nothing.
        if (entity.MapId != fromMapId || entity.Position != fromPosition)
        {
            _eventBus.Publish(new MobileMovedEvent(entity.Id, fromMapId, fromPosition, entity.MapId, entity.Position));
        }

        return true;
    }
}
