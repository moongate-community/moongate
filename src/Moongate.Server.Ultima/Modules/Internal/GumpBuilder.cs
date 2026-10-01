using System.Globalization;
using System.Xml.Linq;
using Lua;

namespace Moongate.Server.Ultima.Modules.Internal;

/// <summary>
///     The gumps built from Lua: <c>gump.create(id)</c> gives a table whose methods (<c>g:text{ ... }</c>,
///     <c>g:button{ ... }</c>, ...) each add a control with the attributes of the XML elements of the same name;
///     <see cref="ToXml" /> turns them into those elements, so a built gump renders, binds and answers as an XML one.
/// </summary>
internal static class GumpBuilder
{
    public const string IdKey = "__id";
    public const string EntriesKey = "__entries";
    public const string KindKey = "__kind";
    private const string XKey = "__x";
    private const string YKey = "__y";
    private const string PagerKey = "__pager";
    private const string FunctionPrefix = "__function_";

    private static readonly string[] Controls =
    [
        "background", "alpha_region", "image", "image_tiled", "item", "text", "label_cropped", "html", "button",
        "checkbox", "radio", "text_entry", "tooltip", "item_property"
    ];

    // The elements whose text is their content in the XML.
    private static readonly HashSet<string> TextControls = ["text", "label_cropped", "html", "text_entry"];

    private static readonly LuaTable Metatable = CreateMetatable();

    public static LuaTable Create(string id, int x, int y)
    {
        var gump = new LuaTable();
        gump[IdKey] = id;
        gump[XKey] = x;
        gump[YKey] = y;
        gump[EntriesKey] = new LuaTable();
        gump.Metatable = Metatable;

        return gump;
    }

    public static string IdOf(LuaTable gump)
    {
        return gump[IdKey].TryRead<string>(out var id) ? id : string.Empty;
    }

    public static (int X, int Y) PositionOf(LuaTable gump)
    {
        return (Integer(gump[XKey]), Integer(gump[YKey]));
    }

    /// <summary>
    ///     Gets the controls of <paramref name="gump" /> as XML elements, moved by <paramref name="dx" />,
    ///     <paramref name="dy" />: those before any <c>g:page()</c>, and one <c>&lt;page&gt;</c> per page. A button whose
    ///     <c>on_click</c> is a function gets a name in <paramref name="functions" /> instead.
    /// </summary>
    public static (List<XElement> Controls, List<XElement> Pages) ToXml(
        LuaTable gump,
        int dx,
        int dy,
        Dictionary<string, LuaFunction> functions
    )
    {
        var controls = new List<XElement>();
        var pages = new List<XElement>();
        XElement? page = null;
        XElement? group = null;

        if (!gump[EntriesKey].TryRead<LuaTable>(out var entries))
        {
            return (controls, pages);
        }

        for (var index = 1; index <= entries.ArrayLength; index++)
        {
            if (!entries[index].TryRead<LuaTable>(out var entry) || !entry[KindKey].TryRead<string>(out var kind))
            {
                continue;
            }

            void Place(XElement element)
            {
                if (page is null)
                {
                    controls.Add(element);
                }
                else
                {
                    page.Add(element);
                }
            }

            switch (kind)
            {
                case "page":
                    page = new XElement("page");
                    pages.Add(page);
                    group = null;

                    break;
                case "group":
                    group = new XElement("group");
                    Place(group);

                    break;
                case "radio":
                    if (group is null)
                    {
                        group = new XElement("group");
                        Place(group);
                    }

                    group.Add(Element(kind, entry, dx, dy, functions));

                    break;
                default:
                    group = null;
                    Place(Element(kind, entry, dx, dy, functions));

                    break;
            }
        }

        return (controls, pages);
    }

    private static XElement Element(
        string kind,
        LuaTable entry,
        int dx,
        int dy,
        Dictionary<string, LuaFunction> functions
    )
    {
        var element = new XElement(kind);

        foreach (var (key, value) in entry)
        {
            if (!key.TryRead<string>(out var name) || name == KindKey)
            {
                continue;
            }

            if (name == "text" && TextControls.Contains(kind))
            {
                element.Value = Text(value);
            }
            else if (name == "on_click" && value.TryRead<LuaFunction>(out var function))
            {
                var functionName = $"{FunctionPrefix}{functions.Count}";
                functions[functionName] = function;
                element.SetAttributeValue(name, functionName);
            }
            else if (name is "x" or "y" && value.TryRead<double>(out var number))
            {
                element.SetAttributeValue(name, (long)number + (name == "x" ? dx : dy));
            }
            else
            {
                element.SetAttributeValue(name, Text(value));
            }
        }

        return element;
    }

    private static string Text(LuaValue value)
    {
        if (value.TryRead<string>(out var text))
        {
            return text;
        }

        if (value.TryRead<bool>(out var flag))
        {
            return flag ? "true" : "false";
        }

        return value.TryRead<double>(out var number)
            ? number % 1 == 0 ? ((long)number).ToString(CultureInfo.InvariantCulture) : number.ToString(CultureInfo.InvariantCulture)
            : string.Empty;
    }

    private static int Integer(LuaValue value)
    {
        return value.TryRead<double>(out var number) ? (int)number : 0;
    }

    private static void Add(LuaTable gump, string kind, LuaTable? spec)
    {
        var entry = new LuaTable();

        if (spec is not null)
        {
            foreach (var (key, value) in spec)
            {
                entry[key] = value;
            }
        }

        entry[KindKey] = kind;
        var entries = gump[EntriesKey].Read<LuaTable>();
        entries[entries.ArrayLength + 1] = entry;
    }

    private static LuaTable CreateMetatable()
    {
        var methods = new LuaTable();

        foreach (var control in Controls)
        {
            methods[control] = new LuaFunction(
                control,
                (context, _) =>
                {
                    var gump = context.GetArgument<LuaTable>(0);
                    Add(gump, control, Spec(context));

                    return new(context.Return(gump));
                }
            );
        }

        methods["page"] = new LuaFunction(
            "page",
            (context, _) =>
            {
                var gump = context.GetArgument<LuaTable>(0);
                Add(gump, "page", null);

                return new(context.Return(gump));
            }
        );
        methods["group"] = new LuaFunction(
            "group",
            (context, _) =>
            {
                var gump = context.GetArgument<LuaTable>(0);
                Add(gump, "group", null);

                return new(context.Return(gump));
            }
        );

        // g:pager{ previous = { x, y, up, down }, next = { ... } }: where g:paginate puts its buttons.
        methods["pager"] = new LuaFunction(
            "pager",
            (context, _) =>
            {
                var gump = context.GetArgument<LuaTable>(0);
                gump[PagerKey] = Spec(context) ?? new LuaTable();

                return new(context.Return(gump));
            }
        );

        // g:paginate(index, per_page): starts a new page every per_page items, with the buttons between the pages, and
        // returns the item's row on its page, from 0.
        methods["paginate"] = new LuaFunction(
            "paginate",
            (context, _) =>
            {
                var gump = context.GetArgument<LuaTable>(0);
                var index = Math.Max(1, (long)context.GetArgument<double>(1));
                var perPage = Math.Max(1, (long)context.GetArgument<double>(2));
                var page = (index - 1) / perPage + 1;
                var row = (index - 1) % perPage;

                if (row == 0)
                {
                    if (page > 1)
                    {
                        Add(gump, "button", PageButton(gump, "next", page, 260, 340, 4005, 4007));
                    }

                    Add(gump, "page", null);

                    if (page > 1)
                    {
                        Add(gump, "button", PageButton(gump, "previous", page - 1, 20, 340, 4014, 4015));
                    }
                }

                return new(context.Return(row));
            }
        );

        var metatable = new LuaTable();
        metatable["__index"] = methods;

        return metatable;
    }

    private static LuaTable PageButton(LuaTable gump, string which, long page, int x, int y, int up, int down)
    {
        var button = new LuaTable();
        button["x"] = x;
        button["y"] = y;
        button["up"] = up;
        button["down"] = down;

        if (gump[PagerKey].TryRead<LuaTable>(out var pager) && pager[which].TryRead<LuaTable>(out var settings))
        {
            foreach (var (key, value) in settings)
            {
                button[key] = value;
            }
        }

        button["page"] = page;

        return button;
    }

    private static LuaTable? Spec(LuaFunctionExecutionContext context)
    {
        return context.ArgumentCount > 1 && context.GetArgument(1).TryRead<LuaTable>(out var spec) ? spec : null;
    }
}
