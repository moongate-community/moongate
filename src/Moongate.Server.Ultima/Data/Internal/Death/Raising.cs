using Moongate.Core.Geometry;
using Moongate.Ultima.Types;

namespace Moongate.Server.Ultima.Data.Internal.Death;

/// <summary>
///     What a corpse says of who died, read once on the game loop before the NPC is born off it: the mobile template,
///     where the corpse lies, the name and the facing to give back, and the props the NPC is born with.
/// </summary>
internal sealed record Raising(
    string Template,
    MapType Map,
    Point3D Location,
    string? Name,
    int? Direction,
    IReadOnlyDictionary<string, object?>? Props
);
