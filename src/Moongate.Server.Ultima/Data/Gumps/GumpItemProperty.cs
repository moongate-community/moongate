using System.Text;
using static System.FormattableString;

namespace Moongate.Server.Ultima.Data.Gumps;

/// <summary>
///     The tooltip of a real item, by serial: <c>{ itemproperty serial }</c>.
/// </summary>
public sealed class GumpItemProperty : GumpEntry
{
    public uint Serial { get; init; }

    public override void Write(StringBuilder layout, GumpStrings strings)
    {
        layout.Append(Invariant($"{{ itemproperty {Serial} }}"));
    }
}
