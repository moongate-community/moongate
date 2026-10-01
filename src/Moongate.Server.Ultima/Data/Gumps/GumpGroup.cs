using System.Text;

namespace Moongate.Server.Ultima.Data.Gumps;

/// <summary>
///     A radio group: <c>{ group n }</c>; the radios after it until the next group exclude each other.
/// </summary>
public sealed class GumpGroup : GumpEntry
{
    public int Group { get; init; }

    public override void Write(StringBuilder layout, GumpStrings strings)
    {
        layout.Append("{ group ").Append(Group).Append(" }");
    }
}
