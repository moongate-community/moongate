using System.Text;
using static System.FormattableString;

namespace Moongate.Server.Ultima.Data.Gumps;

/// <summary>
///     A line of text: <c>{ text x y hue string }</c>.
/// </summary>
public sealed class GumpText : GumpEntry
{
    public int X { get; init; }

    public int Y { get; init; }

    public int Hue { get; init; }

    public string Text { get; init; } = string.Empty;

    public override void Write(StringBuilder layout, GumpStrings strings)
    {
        layout.Append(Invariant($"{{ text {X} {Y} {Hue} {strings.Intern(Text)} }}"));
    }
}
