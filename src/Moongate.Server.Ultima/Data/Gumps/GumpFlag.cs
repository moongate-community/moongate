using System.Text;
using Moongate.Server.Ultima.Types.Gumps;

namespace Moongate.Server.Ultima.Data.Gumps;

/// <summary>
///     A flag of the whole gump: <c>{ nomove }</c>, <c>{ noclose }</c>, <c>{ nodispose }</c> or <c>{ noresize }</c>.
/// </summary>
public sealed class GumpFlag : GumpEntry
{
    public GumpFlagType Flag { get; init; }

    public override void Write(StringBuilder layout, GumpStrings strings)
    {
        layout.Append(
            Flag switch
            {
                GumpFlagType.NoMove => "{ nomove }",
                GumpFlagType.NoClose => "{ noclose }",
                GumpFlagType.NoDispose => "{ nodispose }",
                _ => "{ noresize }"
            }
        );
    }
}
