using System.Text;

namespace Moongate.Server.Ultima.Data.Gumps;

/// <summary>
///     A radio button: <c>{ radio x y off on checked switch }</c>; the chosen one is in the answer's switches.
/// </summary>
public sealed class GumpRadio : GumpEntry
{
    public int X { get; init; }

    public int Y { get; init; }

    public int Off { get; init; }

    public int On { get; init; }

    public bool Checked { get; init; }

    public int SwitchId { get; init; }

    public override void Write(StringBuilder layout, GumpStrings strings)
    {
        layout.Append($"{{ radio {X} {Y} {Off} {On} {Flag(Checked)} {SwitchId} }}");
    }
}
