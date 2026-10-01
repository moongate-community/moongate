using System.Globalization;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;
using Moongate.Server.Core.Interfaces.Services;
using Moongate.Server.Ultima.Data.Gumps;

namespace Moongate.Server.Ultima.Services.Internal;

/// <summary>
///     Adds the controls of one gump opening to its layout: fills the placeholders, picks the texts and numbers the
///     <c>on_click</c> buttons and radio groups.
/// </summary>
internal sealed partial class GumpXmlRenderContext
{
    private readonly GumpLayout _layout;
    private readonly IReadOnlyDictionary<string, string> _args;
    private readonly ILocalizationService? _localization;
    private readonly Dictionary<int, string> _clicks;
    private readonly HashSet<int> _used;

    private int _group;
    private int _nextClick = 1;

    public GumpXmlRenderContext(
        GumpLayout layout,
        IReadOnlyDictionary<string, string> args,
        ILocalizationService? localization,
        Dictionary<int, string> clicks,
        HashSet<int> used
    )
    {
        _layout = layout;
        _args = args;
        _localization = localization;
        _clicks = clicks;
        _used = used;
    }

    public void Add(XElement element)
    {
        int N(string name)
        {
            return Number(element, name);
        }

        switch (element.Name.LocalName)
        {
            case "background":
                _layout.Add(new GumpBackground { X = N("x"), Y = N("y"), GumpId = N("gump"), Width = N("width"), Height = N("height") });

                break;
            case "alpha_region":
                _layout.Add(new GumpAlphaRegion { X = N("x"), Y = N("y"), Width = N("width"), Height = N("height") });

                break;
            case "image":
                _layout.Add(new GumpImage { X = N("x"), Y = N("y"), GumpId = N("gump"), Hue = N("hue") });

                break;
            case "image_tiled":
                _layout.Add(new GumpImageTiled { X = N("x"), Y = N("y"), Width = N("width"), Height = N("height"), GumpId = N("gump") });

                break;
            case "item":
                _layout.Add(new GumpItem { X = N("x"), Y = N("y"), ItemId = N("item"), Hue = N("hue") });

                break;
            case "text":
                _layout.Add(new GumpText { X = N("x"), Y = N("y"), Hue = N("hue"), Text = Text(element) });

                break;
            case "label_cropped":
                _layout.Add(
                    new GumpLabelCropped
                    {
                        X = N("x"), Y = N("y"), Width = N("width"), Height = N("height"), Hue = N("hue"), Text = Text(element)
                    }
                );

                break;
            case "html":
                AddHtml(element);

                break;
            case "button":
                AddButton(element);

                break;
            case "checkbox":
                _layout.Add(
                    new GumpCheckbox
                    {
                        X = N("x"), Y = N("y"), Off = N("off"), On = N("on"), SwitchId = N("switch"), Checked = Flag(element, "checked")
                    }
                );

                break;
            case "group":
                _layout.Add(new GumpGroup { Group = ++_group });

                foreach (var radio in element.Elements())
                {
                    _layout.Add(
                        new GumpRadio
                        {
                            X = Number(radio, "x"), Y = Number(radio, "y"), Off = Number(radio, "off"), On = Number(radio, "on"),
                            SwitchId = Number(radio, "switch"), Checked = Flag(radio, "checked")
                        }
                    );
                }

                break;
            case "text_entry":
                _layout.Add(
                    new GumpTextEntry
                    {
                        X = N("x"), Y = N("y"), Width = N("width"), Height = N("height"), Hue = N("hue"), EntryId = N("entry"),
                        Text = Fill(Plain(element.Value)), MaxLength = N("max_length")
                    }
                );

                break;
            case "tooltip":
                _layout.Add(new GumpTooltip { Cliloc = N("cliloc"), Args = Optional(element, "args", true) });

                break;
            case "item_property":
                _layout.Add(
                    new GumpItemProperty
                    {
                        Serial = uint.TryParse(Fill(element.Attribute("serial")!.Value), CultureInfo.InvariantCulture, out var serial)
                            ? serial
                            : 0
                    }
                );

                break;
        }
    }

    public int Number(XElement element, string name)
    {
        return element.Attribute(name) is { } attribute &&
               int.TryParse(Fill(attribute.Value), NumberStyles.Integer, CultureInfo.InvariantCulture, out var number)
            ? number
            : 0;
    }

    private void AddHtml(XElement element)
    {
        int N(string name)
        {
            return Number(element, name);
        }

        if (element.Attribute("cliloc") is not null)
        {
            _layout.Add(
                new GumpHtmlLocalized
                {
                    X = N("x"), Y = N("y"), Width = N("width"), Height = N("height"), Cliloc = N("cliloc"),
                    Background = Flag(element, "background"), Scrollbar = Flag(element, "scrollbar"),
                    Color = element.Attribute("color") is null ? null : N("color"), Args = Optional(element, "args", true)
                }
            );

            return;
        }

        _layout.Add(
            new GumpHtml
            {
                X = N("x"), Y = N("y"), Width = N("width"), Height = N("height"), Text = Text(element, true),
                Background = Flag(element, "background"), Scrollbar = Flag(element, "scrollbar")
            }
        );
    }

    private void AddButton(XElement element)
    {
        var button = new GumpButton { X = Number(element, "x"), Y = Number(element, "y"), Up = Number(element, "up"), Down = Number(element, "down") };

        if (element.Attribute("page") is not null)
        {
            _layout.Add(new GumpButton { X = button.X, Y = button.Y, Up = button.Up, Down = button.Down, Page = Number(element, "page") });

            return;
        }

        var id = Number(element, "id");

        if (element.Attribute("on_click") is { } click)
        {
            while (_used.Contains(_nextClick))
            {
                _nextClick++;
            }

            id = _nextClick++;
            _clicks[id] = click.Value;
        }

        _layout.Add(new GumpButton { X = button.X, Y = button.Y, Up = button.Up, Down = button.Down, ButtonId = id });
    }

    // The element's text without the file's indentation, or the server message it names; with its placeholders
    // filled, escaped where the client reads HTML.
    private string Text(XElement element, bool html = false)
    {
        if (element.Attribute("message") is null)
        {
            return Fill(Plain(element.Value), html);
        }

        return _localization is not null && _localization.TryGetText(Number(element, "message"), out var text)
            ? Fill(text, html)
            : string.Empty;
    }

    private string? Optional(XElement element, string name, bool html = false)
    {
        return element.Attribute(name) is { } attribute ? Fill(attribute.Value, html) : null;
    }

    // The words of a text written over several indented lines, one space apart.
    private static string Plain(string text)
    {
        return string.Join(' ', text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
    }

    private static bool Flag(XElement element, string name)
    {
        return element.Attribute(name) is { } attribute && XmlConvert.ToBoolean(attribute.Value);
    }

    [GeneratedRegex(@"\$\{([a-z_][a-z0-9_]*)\}")]
    private static partial Regex Placeholder();

    // An argument shown as HTML is escaped, so a player's name cannot add links or fake controls.
    private string Fill(string text, bool html = false)
    {
        return Placeholder()
            .Replace(
                text,
                match =>
                {
                    var value = _args.GetValueOrDefault(match.Groups[1].Value, string.Empty);

                    return html ? value.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;") : value;
                }
            );
    }
}
