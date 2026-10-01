using System.Text;

namespace Moongate.Server.Ultima.Data.Gumps;

/// <summary>
///     A page: <c>{ page n }</c>. Page 0 shows on every page.
/// </summary>
public sealed class GumpPage : GumpEntry
{
    public int Page { get; init; }

    public override void Write(StringBuilder layout, GumpStrings strings)
    {
        layout.Append("{ page ").Append(Page).Append(" }");
    }
}
