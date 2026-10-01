using System.Globalization;
using System.Xml;
using System.Xml.Linq;
using Moongate.Server.Core.Interfaces.Services;
using Moongate.Server.Ultima.Data.Gumps;
using Moongate.Server.Ultima.Data.Templates.Gumps;
using Moongate.Server.Ultima.Types.Gumps;

namespace Moongate.Server.Ultima.Services.Internal;

/// <summary>
///     Turns a gump of <c>templates/gumps</c> into a layout for one opening: <c>${name}</c> filled from the arguments
///     (empty when missing), texts from the element, a server message (<c>message</c>) or a client message
///     (<c>cliloc</c>), and an id for every <c>on_click</c> button that the <c>id</c> buttons do not use. The file was
///     checked against <c>gump.xsd</c>, so a number that is not one after filling is a placeholder gone wrong: it is 0.
/// </summary>
internal static class GumpXmlRenderer
{
    public static RenderedGump Render(
        GumpTemplate template,
        IReadOnlyDictionary<string, string> args,
        ILocalizationService? localization
    )
    {
        var root = template.Root;
        var layout = new GumpLayout();
        var clicks = new Dictionary<int, string>();
        var used = root.Descendants("button")
                       .Select(button => button.Attribute("id")?.Value)
                       .Select(id => int.TryParse(id, NumberStyles.Integer, CultureInfo.InvariantCulture, out var number) ? number : 0)
                       .Where(id => id > 0)
                       .ToHashSet();
        var context = new GumpXmlRenderContext(layout, args, localization, clicks, used);

        foreach (var (attribute, flag) in new[]
                 {
                     ("closable", GumpFlagType.NoClose), ("movable", GumpFlagType.NoMove),
                     ("disposable", GumpFlagType.NoDispose), ("resizable", GumpFlagType.NoResize)
                 })
        {
            if (root.Attribute(attribute) is { } value && !XmlConvert.ToBoolean(value.Value))
            {
                layout.Add(new GumpFlag { Flag = flag });
            }
        }

        layout.Add(new GumpPage { Page = 0 });
        var page = 0;

        foreach (var element in root.Elements())
        {
            if (element.Name.LocalName == "page")
            {
                layout.Add(new GumpPage { Page = ++page });

                foreach (var control in element.Elements())
                {
                    context.Add(control);
                }
            }
            else
            {
                context.Add(element);
            }
        }

        return new()
        {
            Layout = layout, X = context.Number(root, "x"), Y = context.Number(root, "y"), Clicks = clicks
        };
    }
}
