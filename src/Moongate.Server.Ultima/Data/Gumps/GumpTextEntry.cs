using System.Text;

namespace Moongate.Server.Ultima.Data.Gumps;

/// <summary>
///     A text field: <c>{ textentry x y width height hue entry string }</c>, or <c>textentrylimited</c> with a <see cref="MaxLength" />; its text comes back in the answer.
/// </summary>
public sealed class GumpTextEntry : GumpEntry
{
    public int X { get; init; }

    public int Y { get; init; }

    public int Width { get; init; }

    public int Height { get; init; }

    public int Hue { get; init; }

    public int EntryId { get; init; }

    public string Text { get; init; } = string.Empty;

    public int MaxLength { get; init; }

    public override void Write(StringBuilder layout, GumpStrings strings)
    {
        var text = strings.Intern(Text);

        layout.Append(
            MaxLength == 0
                ? $"{{ textentry {X} {Y} {Width} {Height} {Hue} {EntryId} {text} }}"
                : $"{{ textentrylimited {X} {Y} {Width} {Height} {Hue} {EntryId} {text} {MaxLength} }}"
        );
    }
}
