using System.Text;
using static System.FormattableString;

namespace Moongate.Server.Ultima.Data.Gumps;

/// <summary>
///     HTML text in a box: <c>{ htmlgump x y width height string background scrollbar }</c>.
/// </summary>
public sealed class GumpHtml : GumpEntry
{
    public int X { get; init; }

    public int Y { get; init; }

    public int Width { get; init; }

    public int Height { get; init; }

    public string Text { get; init; } = string.Empty;

    public bool Background { get; init; }

    public bool Scrollbar { get; init; }

    public override void Write(StringBuilder layout, GumpStrings strings)
    {
        layout.Append(
            Invariant($"{{ htmlgump {X} {Y} {Width} {Height} {strings.Intern(Text)} {Flag(Background)} {Flag(Scrollbar)} }}")
        );
    }
}
