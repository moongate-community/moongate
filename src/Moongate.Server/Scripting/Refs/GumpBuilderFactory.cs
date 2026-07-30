using Moongate.Server.Abstractions.Data.Gumps;
using Moongate.Server.Abstractions.Interfaces.Gumps;
using MoonSharp.Interpreter;

namespace Moongate.Server.Scripting.Refs;

/// <summary>
/// Builds the table Lua draws a gump through: one closure per element, each taking a named table.
/// <para>
/// Named fields rather than positional arguments, because a gump is a wall of numbers and an order
/// remembered wrongly produces a crooked gump rather than an error. They also dissolve the overloads
/// — the three localized-html forms differ only by which of <c>args</c> and <c>colour</c> they carry,
/// which as named fields is nothing to distinguish at all.
/// </para>
/// </summary>
public sealed class GumpBuilderFactory
{
    private readonly Script _script;

    public GumpBuilderFactory(Script script)
    {
        _script = script;
    }

    /// <summary>A table of closures writing into <paramref name="builder" />, valid for one build.</summary>
    public DynValue Create(IGumpBuilder builder)
    {
        var table = new Table(_script)
        {
            ["page"] = (Action<Table>)(t => builder.AddPage(Int(t, "index"))),
            ["group"] = (Action<Table>)(t => builder.AddGroup(Int(t, "index"))),
            ["background"] = (Action<Table>)(t =>
                builder.AddBackground(Int(t, "x"), Int(t, "y"), Int(t, "w"), Int(t, "h"), Int(t, "art"))),
            ["alpha"] = (Action<Table>)(t =>
                builder.AddAlphaRegion(Int(t, "x"), Int(t, "y"), Int(t, "w"), Int(t, "h"))),
            ["label"] = (Action<Table>)(t =>
                builder.AddLabel(Int(t, "x"), Int(t, "y"), Int(t, "hue"), Str(t, "text"))),
            ["label_cropped"] = (Action<Table>)(t =>
                builder.AddLabelCropped(Int(t, "x"), Int(t, "y"), Int(t, "w"), Int(t, "h"), Int(t, "hue"), Str(t, "text"))),
            ["label_html"] = (Action<Table>)(t =>
                builder.AddLabelHtml(
                    Int(t, "x"),
                    Int(t, "y"),
                    Int(t, "w"),
                    Int(t, "h"),
                    Str(t, "text"),
                    Str(t, "hue", "#FFFFFF"),
                    Int(t, "size", 4),
                    Bool(t, "center")
                )),
            ["html"] = (Action<Table>)(t =>
                builder.AddHtml(
                    Int(t, "x"),
                    Int(t, "y"),
                    Int(t, "w"),
                    Int(t, "h"),
                    Str(t, "text"),
                    Bool(t, "background"),
                    Bool(t, "scrollbar")
                )),
            ["html_localized"] = (Action<Table>)(t =>
                builder.AddHtmlLocalized(
                    Int(t, "x"),
                    Int(t, "y"),
                    Int(t, "w"),
                    Int(t, "h"),
                    Int(t, "cliloc"),
                    OptionalStr(t, "args"),
                    OptionalInt(t, "color"),
                    Bool(t, "background"),
                    Bool(t, "scrollbar")
                )),
            ["button"] = (Action<Table>)(t =>
                builder.AddButton(
                    Int(t, "x"),
                    Int(t, "y"),
                    Int(t, "art"),
                    Int(t, "pressed"),
                    Int(t, "id"),
                    Int(t, "type", 1),
                    Int(t, "page")
                )),
            ["button_art"] = (Action<Table>)(t =>
                builder.AddImageTiledButton(
                    Int(t, "x"),
                    Int(t, "y"),
                    Int(t, "art"),
                    Int(t, "pressed"),
                    Int(t, "id"),
                    Int(t, "type", 1),
                    Int(t, "page"),
                    Int(t, "item"),
                    Int(t, "hue"),
                    Int(t, "w"),
                    Int(t, "h")
                )),
            ["check"] = (Action<Table>)(t =>
                builder.AddCheck(Int(t, "x"), Int(t, "y"), Int(t, "art"), Int(t, "pressed"), Bool(t, "checked"), Int(t, "id"))),
            ["radio"] = (Action<Table>)(t =>
                builder.AddRadio(Int(t, "x"), Int(t, "y"), Int(t, "art"), Int(t, "pressed"), Bool(t, "checked"), Int(t, "id"))),
            ["entry"] = (Action<Table>)(t =>
                builder.AddTextEntry(
                    Int(t, "x"),
                    Int(t, "y"),
                    Int(t, "w"),
                    Int(t, "h"),
                    Int(t, "hue"),
                    Int(t, "id"),
                    Str(t, "text")
                )),
            ["image"] = (Action<Table>)(t =>
                builder.AddImage(Int(t, "x"), Int(t, "y"), Int(t, "art"), Int(t, "hue"))),
            ["image_tiled"] = (Action<Table>)(t =>
                builder.AddImageTiled(Int(t, "x"), Int(t, "y"), Int(t, "w"), Int(t, "h"), Int(t, "art"))),
            ["item"] = (Action<Table>)(t =>
                builder.AddItem(Int(t, "x"), Int(t, "y"), Int(t, "id"), Int(t, "hue"))),
            ["sprite"] = (Action<Table>)(t =>
                builder.AddSpriteImage(
                    Int(t, "x"),
                    Int(t, "y"),
                    Int(t, "art"),
                    Int(t, "w"),
                    Int(t, "h"),
                    Int(t, "sx"),
                    Int(t, "sy")
                )),
            ["tooltip"] = (Action<Table>)(t => builder.AddTooltip(Int(t, "cliloc"), OptionalStr(t, "args"))),
            ["item_property"] = (Action<Table>)(t => builder.AddItemProperty((uint)Int(t, "serial"))),
            ["master_gump"] = (Action<Table>)(t => builder.AddGumpIdOverride(Int(t, "art")))
        };

        return DynValue.NewTable(table);
    }

    /// <summary>
    /// Turns a response into the table the Lua callback receives: <c>button</c>, a one-based list of
    /// ticked <c>switches</c>, and <c>text</c> keyed by entry id.
    /// </summary>
    public DynValue ToResponseTable(GumpResponse response)
    {
        var switches = new Table(_script);

        for (var i = 0; i < response.Switches.Count; i++)
        {
            switches[i + 1] = response.Switches[i];
        }

        var text = new Table(_script);

        foreach (var (id, value) in response.TextEntries)
        {
            text[id] = value;
        }

        var table = new Table(_script)
        {
            ["button"] = response.Button,
            ["switches"] = switches,
            ["text"] = text
        };

        return DynValue.NewTable(table);
    }

    private static int Int(Table table, string key, int fallback = 0)
        => table.Get(key) is { Type: DataType.Number } value ? (int)value.Number : fallback;

    private static int? OptionalInt(Table table, string key)
        => table.Get(key) is { Type: DataType.Number } value ? (int)value.Number : null;

    private static string Str(Table table, string key, string fallback = "")
        => table.Get(key) is { Type: DataType.String } value ? value.String : fallback;

    private static string? OptionalStr(Table table, string key)
        => table.Get(key) is { Type: DataType.String } value ? value.String : null;

    private static bool Bool(Table table, string key, bool fallback = false)
        => table.Get(key) is { Type: DataType.Boolean } value ? value.Boolean : fallback;
}
