using System.Text;
using static System.FormattableString;

namespace Moongate.Server.Ultima.Data.Gumps;

/// <summary>
///     A button: with a <see cref="Page" /> it turns to that page, else it answers the server with <see cref="ButtonId" />.
/// </summary>
public sealed class GumpButton : GumpEntry
{
    public int X { get; init; }

    public int Y { get; init; }

    public int Up { get; init; }

    public int Down { get; init; }

    public int ButtonId { get; init; }

    public int Page { get; init; }

    public override void Write(StringBuilder layout, GumpStrings strings)
    {
        layout.Append(
            Page == 0
                ? Invariant($"{{ button {X} {Y} {Up} {Down} 1 0 {ButtonId} }}")
                : Invariant($"{{ button {X} {Y} {Up} {Down} 0 {Page} 0 }}")
        );
    }
}
