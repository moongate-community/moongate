using Moongate.Core.Geometry;
using Moongate.Server.Ultima.Data.Templates.Spawns;

namespace Moongate.Server.Ultima.Data.Internal.Spawns;

/// <summary>
///     One NPC or item a spawn check decided to spawn: its region, template, spot and the area the spot is in.
/// </summary>
internal sealed record PlannedSpawn(SpawnTemplate Region, string TemplateId, Point3D Location, SpawnArea Area, bool OfItems = false);
