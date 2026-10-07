using System.Text;
using static System.FormattableString;

namespace Moongate.Server.Ultima.Data.Gumps;

/// <summary>
///     The tooltip of the entry before it: <c>{ tooltip cliloc [@args@] }</c>.
/// </summary>
public sealed class GumpTooltip : GumpEntry
{
    public int Cliloc { get; init; }

    public string? Args { get; init; }

    public override void Write(StringBuilder layout, GumpStrings strings)
    {
        layout.Append(
            Args is null ? Invariant($"{{ tooltip {Cliloc} }}") : Invariant($"{{ tooltip {Cliloc} @{Arguments(Args)}@ }}")
        );
    }
}
