using System.Text;

namespace Moongate.Server.Ultima.Data.Gumps;

/// <summary>
///     A client message (cliloc) in a box: <c>xmfhtmlgump</c>; with <see cref="Color" /> <c>xmfhtmlgumpcolor</c>; with <see cref="Args" /> (tab separated) <c>xmfhtmltok</c>.
/// </summary>
public sealed class GumpHtmlLocalized : GumpEntry
{
    public int X { get; init; }

    public int Y { get; init; }

    public int Width { get; init; }

    public int Height { get; init; }

    public int Cliloc { get; init; }

    public bool Background { get; init; }

    public bool Scrollbar { get; init; }

    public int? Color { get; init; }

    public string? Args { get; init; }

    public override void Write(StringBuilder layout, GumpStrings strings)
    {
        var box = $"{X} {Y} {Width} {Height}";

        if (Args is not null)
        {
            layout.Append(
                $"{{ xmfhtmltok {box} {Flag(Background)} {Flag(Scrollbar)} {Color ?? 0} {Cliloc} @{Args}@ }}"
            );
        }
        else if (Color is { } color)
        {
            layout.Append($"{{ xmfhtmlgumpcolor {box} {Cliloc} {Flag(Background)} {Flag(Scrollbar)} {color} }}");
        }
        else
        {
            layout.Append($"{{ xmfhtmlgump {box} {Cliloc} {Flag(Background)} {Flag(Scrollbar)} }}");
        }
    }
}
