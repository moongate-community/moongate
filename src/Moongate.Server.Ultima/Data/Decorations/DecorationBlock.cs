using Moongate.Core.Geometry;

namespace Moongate.Server.Ultima.Data.Decorations;

/// <summary>
///     One <c>[[decoration]]</c> block of a decoration file: one kind of item placed at every location it lists.
/// </summary>
public sealed class DecorationBlock
{
    /// <summary>
    ///     The kind, kept as ModernUO names it, such as <c>Static</c>, <c>MetalDoor</c> or <c>Teleporter</c>.
    /// </summary>
    public required string Type { get; init; }

    /// <summary>
    ///     What the block places, when the data names it.
    /// </summary>
    public string? Comment { get; init; }

    /// <summary>
    ///     The graphic; null for an addon built from several graphics.
    /// </summary>
    public int? ItemId { get; init; }

    /// <summary>
    ///     The kind's settings that are a string, a whole number, a number or a bool, such as <c>hue</c>, <c>name</c> or
    ///     <c>facing</c>; settings that are lists are left out.
    /// </summary>
    public IReadOnlyDictionary<string, object> Props { get; init; } = new Dictionary<string, object>();

    /// <summary>
    ///     Where every item of the block goes.
    /// </summary>
    public IReadOnlyList<Point3D> Locations { get; init; } = [];
}
