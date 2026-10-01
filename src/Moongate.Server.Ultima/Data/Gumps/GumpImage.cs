using System.Text;

namespace Moongate.Server.Ultima.Data.Gumps;

/// <summary>
///     A gump image: <c>{ gumppic x y gump [hue=h] }</c>.
/// </summary>
public sealed class GumpImage : GumpEntry
{
    public int X { get; init; }

    public int Y { get; init; }

    public int GumpId { get; init; }

    public int Hue { get; init; }

    public override void Write(StringBuilder layout, GumpStrings strings)
    {
        layout.Append($"{{ gumppic {X} {Y} {GumpId}");

        if (Hue != 0)
        {
            layout.Append($" hue={Hue}");
        }

        layout.Append(" }");
    }
}
