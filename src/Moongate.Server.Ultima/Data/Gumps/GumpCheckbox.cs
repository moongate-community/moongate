using System.Text;
using static System.FormattableString;

namespace Moongate.Server.Ultima.Data.Gumps;

/// <summary>
///     A checkbox: <c>{ checkbox x y off on checked switch }</c>; a checked one is in the answer's switches.
/// </summary>
public sealed class GumpCheckbox : GumpEntry
{
    public int X { get; init; }

    public int Y { get; init; }

    public int Off { get; init; }

    public int On { get; init; }

    public bool Checked { get; init; }

    public int SwitchId { get; init; }

    public override void Write(StringBuilder layout, GumpStrings strings)
    {
        layout.Append(Invariant($"{{ checkbox {X} {Y} {Off} {On} {Flag(Checked)} {SwitchId} }}"));
    }
}
