using System.Text;
using static System.FormattableString;

namespace Moongate.Server.Ultima.Data.Gumps;

/// <summary>
///     A resizable background: <c>{ resizepic x y gump width height }</c>.
/// </summary>
public sealed class GumpBackground : GumpEntry
{
    public int X { get; init; }

    public int Y { get; init; }

    public int GumpId { get; init; }

    public int Width { get; init; }

    public int Height { get; init; }

    public override void Write(StringBuilder layout, GumpStrings strings)
    {
        layout.Append(Invariant($"{{ resizepic {X} {Y} {GumpId} {Width} {Height} }}"));
    }
}
