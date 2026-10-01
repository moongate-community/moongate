using Moongate.Core.Geometry;
using Moongate.Server.Ultima.Entities.World;
using Moongate.Ultima.Types;

namespace Moongate.Server.Ultima.Data.Internal.Bank;

/// <summary>
///     Where a player opened its bank box: it stays open while that character stands on that spot.
/// </summary>
public sealed record OpenBank(MobileEntity Player, MapType Map, Point3D Location);
