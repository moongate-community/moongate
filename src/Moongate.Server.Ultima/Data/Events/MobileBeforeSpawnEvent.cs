using Moongate.Core.Geometry;
using Moongate.Server.Core.Interfaces.Events;
using Moongate.Server.Ultima.Entities.World;
using Moongate.Ultima.Types;

namespace Moongate.Server.Ultima.Data.Events;

/// <summary>
///     Published by <see cref="Interfaces.IMobileFactoryService.SpawnAsync" /> before the mobile is saved; handlers may
///     change it. The event bus logs a handler's exception and the spawn goes on.
/// </summary>
public sealed record MobileBeforeSpawnEvent(MobileEntity Mobile, MapType Map, Point3D Location) : IMoongateEvent;
