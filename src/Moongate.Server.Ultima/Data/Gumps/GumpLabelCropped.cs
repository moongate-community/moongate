using System.Text;
using static System.FormattableString;

namespace Moongate.Server.Ultima.Data.Gumps;

/// <summary>
///     Text cut to a box: <c>{ croppedtext x y width height hue string }</c>.
/// </summary>
public sealed class GumpLabelCropped : GumpEntry
{
    public int X { get; init; }

    public int Y { get; init; }

    public int Width { get; init; }

    public int Height { get; init; }

    public int Hue { get; init; }

    public string Text { get; init; } = string.Empty;

    public override void Write(StringBuilder layout, GumpStrings strings)
    {
        layout.Append(Invariant($"{{ croppedtext {X} {Y} {Width} {Height} {Hue} {strings.Intern(Text)} }}"));
    }
}
