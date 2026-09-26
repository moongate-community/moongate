using Moongate.Core.Geometry;

namespace Moongate.Server.Ultima.Data.Multis;

/// <summary>
///     The layout of one multi from the client files: a house, a boat, a tent or any other object made of several tiles.
/// </summary>
public sealed class MultiDefinition
{
    /// <summary>
    ///     Gets the multi id; the multi's item graphic is <c>0x4000</c> plus this id.
    /// </summary>
    public int Id { get; init; }

    /// <summary>
    ///     Gets the smallest X and Y offset of the components.
    /// </summary>
    public Point2D Min { get; init; }

    /// <summary>
    ///     Gets the largest X and Y offset of the components.
    /// </summary>
    public Point2D Max { get; init; }

    /// <summary>
    ///     Gets the highest Z offset of the components.
    /// </summary>
    public int Height { get; init; }

    /// <summary>
    ///     Gets the tiles of the multi, in file order.
    /// </summary>
    public IReadOnlyList<MultiComponent> Components { get; init; } = [];
}
