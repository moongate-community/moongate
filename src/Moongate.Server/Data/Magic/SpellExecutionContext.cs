using Moongate.Server.Interfaces.Items;
using Moongate.Server.Interfaces.Services.Events;
using Moongate.Server.Interfaces.Services.Spatial;
using Moongate.Server.Interfaces.Services.Timing;
using Moongate.UO.Data.Persistence.Entities;

namespace Moongate.Server.Data.Magic;

/// <summary>
/// Provides resolved spell execution data and runtime services to concrete spell effects.
/// </summary>
public sealed record SpellExecutionContext(
    UOMobileEntity Caster,
    SpellTargetData Target,
    UOMobileEntity? TargetMobile,
    UOItemEntity? TargetItem,
    ISpatialWorldService SpatialWorldService,
    IGameEventBusService GameEventBusService,
    ITimerService TimerService,
    IItemService ItemService
);
