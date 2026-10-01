using System.Text;
using static System.FormattableString;

namespace Moongate.Server.Ultima.Data.Gumps;

/// <summary>
///     A see-through region: <c>{ checkertrans x y width height }</c>.
/// </summary>
public sealed class GumpAlphaRegion : GumpEntry
{
    public int X { get; init; }

    public int Y { get; init; }

    public int Width { get; init; }

    public int Height { get; init; }

    public override void Write(StringBuilder layout, GumpStrings strings)
    {
        layout.Append(Invariant($"{{ checkertrans {X} {Y} {Width} {Height} }}"));
    }
}
