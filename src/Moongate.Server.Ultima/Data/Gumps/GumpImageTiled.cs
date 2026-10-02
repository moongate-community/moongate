using System.Text;
using static System.FormattableString;

namespace Moongate.Server.Ultima.Data.Gumps;

/// <summary>
///     A tiled gump image: <c>{ gumppictiled x y width height gump }</c>.
/// </summary>
public sealed class GumpImageTiled : GumpEntry
{
    public int X { get; init; }

    public int Y { get; init; }

    public int Width { get; init; }

    public int Height { get; init; }

    public int GumpId { get; init; }

    public override void Write(StringBuilder layout, GumpStrings strings)
    {
        layout.Append(Invariant($"{{ gumppictiled {X} {Y} {Width} {Height} {GumpId} }}"));
    }
}
