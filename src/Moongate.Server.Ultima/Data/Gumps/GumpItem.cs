using System.Text;

namespace Moongate.Server.Ultima.Data.Gumps;

/// <summary>
///     An item graphic: <c>{ tilepic x y item }</c>, or <c>{ tilepichue x y item hue }</c> with a hue.
/// </summary>
public sealed class GumpItem : GumpEntry
{
    public int X { get; init; }

    public int Y { get; init; }

    public int ItemId { get; init; }

    public int Hue { get; init; }

    public override void Write(StringBuilder layout, GumpStrings strings)
    {
        layout.Append(Hue == 0 ? $"{{ tilepic {X} {Y} {ItemId} }}" : $"{{ tilepichue {X} {Y} {ItemId} {Hue} }}");
    }
}
