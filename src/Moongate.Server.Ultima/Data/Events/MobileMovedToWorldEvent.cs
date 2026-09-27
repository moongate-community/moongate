using Moongate.Core.Geometry;
using Moongate.Server.Core.Interfaces.Events;
using Moongate.Server.Ultima.Entities.World;
using Moongate.Ultima.Types;

namespace Moongate.Server.Ultima.Data.Events;

/// <summary>
///     Published once a mobile is saved at a place in the world, after the transaction commits; also meant for later
///     moves and teleports.
/// </summary>
public sealed record MobileMovedToWorldEvent(MobileEntity Mobile, MapType Map, Point3D Location) : IMoongateEvent;
