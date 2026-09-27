using Moongate.Core.Geometry;
using Moongate.Server.Core.Interfaces.Events;
using Moongate.Server.Ultima.Entities.World;
using Moongate.Ultima.Types;

namespace Moongate.Server.Ultima.Data.Events;

/// <summary>
///     Published by <see cref="Interfaces.IMobileFactoryService.SpawnAsync" /> before the mobile is saved; handlers may
///     change it, and an exception stops the spawn with nothing saved.
/// </summary>
public sealed record MobileBeforeSpawnEvent(MobileEntity Mobile, MapType Map, Point3D Location) : IMoongateEvent;
