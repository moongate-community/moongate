using Moongate.Core.Geometry;

namespace Moongate.UO.Data.World;

/// <summary>
/// One object the world should hold, resolved from whichever corpus asked for it — decoration, a sign
/// or a door — and reduced to what placing it needs.
/// </summary>
/// <param name="MapId">Which facet.</param>
/// <param name="Point">Where it stands.</param>
/// <param name="ItemId">The graphic, which the instance carries rather than the template.</param>
/// <param name="Hue">0 for the raw art.</param>
/// <param name="NameCliloc">A cliloc that names it, or 0.</param>
/// <param name="Name">Literal text that names it, or empty.</param>
/// <param name="TemplateId">The template to build it from, which is what decides whether it behaves.</param>
public readonly record struct WorldPlacement(
    int MapId,
    Point3D Point,
    int ItemId,
    int Hue,
    int NameCliloc,
    string Name,
    string TemplateId
);
