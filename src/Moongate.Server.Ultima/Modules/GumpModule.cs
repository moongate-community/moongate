using System.Globalization;
using System.Xml.Linq;
using Lua;
using Moongate.Core.Primitives;
using Moongate.Scripting.Attributes.Scripts;
using Moongate.Scripting.Types.Scripts;
using Moongate.Server.Core.Data.Sessions;
using Moongate.Server.Core.Interfaces.Services;
using Moongate.Server.Ultima.Data.Gumps;
using Moongate.Server.Ultima.Data.Templates.Gumps;
using Moongate.Server.Ultima.Interfaces;
using Moongate.Server.Ultima.Modules.Internal;
using Moongate.Server.Ultima.Types.Gumps;
using Serilog;

namespace Moongate.Server.Ultima.Modules;

/// <summary>
///     The <c>gump</c> Lua module: opens the gumps of <c>templates/gumps</c> on players and calls their script,
///     <c>scripts/gumps/&lt;id&gt;.lua</c>, with the answer: the controls with <c>bind</c> write into the arguments, an
///     <c>open</c> button opens its gump with them, an <c>on_click</c> button calls the function it names, an <c>id</c>
///     button calls <c>on_button</c>, and <c>on_close</c> runs when the gump goes away without a button, with the
///     reason: <c>player</c>, <c>replaced</c>, <c>server</c> or <c>disconnect</c>.
/// </summary>
[ScriptModule("gump", "Opens the gumps of templates/gumps on players; their script gets the answer.")]
public sealed class GumpModule
{
    private readonly ILogger _logger = Log.ForContext<GumpModule>();
    private readonly ISessionService _sessions;
    private readonly IGumpService _gumps;
    private readonly IGumpTemplateService _templates;
    private readonly Lazy<IGumpScriptService> _scripts;

    public GumpModule(
        ISessionService sessions,
        IGumpService gumps,
        IGumpTemplateService templates,
        Lazy<IGumpScriptService> scripts
    )
    {
        _sessions = sessions;
        _gumps = gumps;
        _templates = templates;
        _scripts = scripts;
    }

    /// <summary>
    ///     Opens gump <paramref name="id" /> on the player, its <c>${name}</c> filled from <paramref name="args" />, which
    ///     its script gets back with every answer; <c>gump.open(player, "release_pet", { pet_name = "Fido" })</c>.
    /// </summary>
    [ScriptFunction(helpText: "Opens a gump of templates/gumps on the player, filled from args; false for an unknown player or gump.")]
    public bool Open(long player, string id, LuaTable? args = null)
    {
        if (!TryGetSession(player, out var session))
        {
            return false;
        }

        if (!_templates.TryGet(id, out var template))
        {
            _logger.Warning("No gump {Gump} in templates/gumps", id);

            return false;
        }

        var table = args ?? new LuaTable();
        var functions = new Dictionary<string, LuaFunction>(StringComparer.Ordinal);

        if (template.Root.Descendants("slot").Any())
        {
            template = new() { Id = template.Id, File = template.File, Root = FillSlots(template, player, table, functions) };
        }

        return OpenTemplate(session, template, table, functions);
    }

    /// <summary>
    ///     Starts a gump built from Lua; <c>local g = gump.create("pet_list")</c>, then <c>g:text{ x = 20, y = 20,
    ///     text = "Pets" }</c> and the other controls, with the attributes of the XML elements of the same name.
    /// </summary>
    [ScriptFunction(helpText: "Starts a gump built from Lua: add controls with g:text{...}, g:button{...}, ..., then gump.send.")]
    public LuaTable Create(string id, int x = 0, int y = 0)
    {
        return GumpBuilder.Create(id, x, y);
    }

    /// <summary>
    ///     Opens a gump built with <see cref="Create" /> on the player; its buttons call their <c>on_click</c> function, or
    ///     the function of that name of <c>scripts/gumps/&lt;id&gt;.lua</c>; <c>gump.send(player, g, args)</c>.
    /// </summary>
    [ScriptFunction(helpText: "Opens a gump built with gump.create on the player; false for an unknown player.")]
    public bool Send(long player, LuaTable gump, LuaTable? args = null)
    {
        if (!TryGetSession(player, out var session))
        {
            return false;
        }

        var id = GumpBuilder.IdOf(gump);
        var (x, y) = GumpBuilder.PositionOf(gump);
        var functions = new Dictionary<string, LuaFunction>(StringComparer.Ordinal);
        var (controls, pages) = GumpBuilder.ToXml(gump, 0, 0, functions);
        var root = new XElement("gump", new XAttribute("id", id), new XAttribute("x", x), new XAttribute("y", y), controls, pages);

        return OpenTemplate(session, new() { Id = id, File = "(built from Lua)", Root = root }, args ?? new LuaTable(), functions);
    }

    /// <summary>
    ///     Closes the player's gump <paramref name="id" />; its script gets <c>on_close</c> with <c>server</c>;
    ///     <c>gump.close(player, "release_pet")</c>.
    /// </summary>
    [ScriptFunction(helpText: "Closes the player's gump; false for an unknown player or a gump that is not open.")]
    public bool Close(long player, string id)
    {
        return TryGetSession(player, out var session) && _gumps.Close(session, id);
    }

    private bool OpenTemplate(
        GameSession session,
        GumpTemplate template,
        LuaTable table,
        Dictionary<string, LuaFunction> functions
    )
    {
        var id = template.Id;

        return _templates.Open(
            session,
            template,
            Strings(table),
            (answered, answer) => Answered(id, answered, answer, table, functions),
            (closed, reason) => _scripts.Value.Call(id, "on_close", PlayerOf(closed), table, ReasonOf(reason))
        );
    }

    // Each <slot> becomes what <id>.<name>(g, player, args) adds to g, moved to the slot; its pages follow the gump's.
    private XElement FillSlots(GumpTemplate template, long player, LuaTable table, Dictionary<string, LuaFunction> functions)
    {
        var root = new XElement(template.Root);

        foreach (var slot in root.Descendants("slot").ToList())
        {
            var builder = GumpBuilder.Create(template.Id, 0, 0);
            _scripts.Value.Call(template.Id, (string)slot.Attribute("name")!, builder, player, table);
            var (controls, pages) = GumpBuilder.ToXml(builder, Coordinate(slot, "x"), Coordinate(slot, "y"), functions);
            slot.ReplaceWith(controls);
            root.Add(pages);
        }

        return root;
    }

    private static int Coordinate(XElement element, string name)
    {
        return int.TryParse((string?)element.Attribute(name), NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)
            ? value
            : 0;
    }

    private void Answered(
        string id,
        GameSession session,
        GumpTemplateAnswer answered,
        LuaTable args,
        IReadOnlyDictionary<string, LuaFunction> functions
    )
    {
        var player = PlayerOf(session);
        var response = answered.Response;

        if (response.ButtonId == 0)
        {
            _scripts.Value.Call(id, "on_close", player, args, "player");

            return;
        }

        foreach (var (name, value) in answered.Bound)
        {
            args[name] = value switch
            {
                bool flag => flag,
                long number => number,
                _ => (string)value
            };
        }

        // An open button goes on to its gump with the same arguments, the bound values in them.
        if (answered.Open is { } next)
        {
            Open(player, next, args);

            return;
        }

        var answer = ToLua(response);

        if (answered.Click is { } click && functions.TryGetValue(click, out var callback))
        {
            _scripts.Value.CallFunction(id, callback, player, answer, args);
        }
        else if (answered.Click is { } function)
        {
            if (_scripts.Value.Call(id, function, player, answer, args).Kind == ScriptResultKind.Missing)
            {
                _logger.Warning("Gump {Gump}: on_click {Function} is not a function of scripts/gumps/{Gump}.lua", id, function, id);
            }
        }
        else
        {
            _scripts.Value.Call(id, "on_button", player, response.ButtonId, answer, args);
        }
    }

    private static LuaTable ToLua(GumpResponse response)
    {
        var switches = new LuaTable();

        foreach (var id in response.Switches)
        {
            switches[id] = true;
        }

        var texts = new LuaTable();

        foreach (var (id, text) in response.Texts)
        {
            texts[id] = text;
        }

        var answer = new LuaTable();
        answer["button"] = response.ButtonId;
        answer["switches"] = switches;
        answer["text"] = texts;

        return answer;
    }

    // The placeholders take strings, numbers (whole ones without a decimal point) and bools.
    private static Dictionary<string, string> Strings(LuaTable table)
    {
        var strings = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var (key, value) in table)
        {
            if (!key.TryRead<string>(out var name))
            {
                continue;
            }

            if (value.TryRead<string>(out var text))
            {
                strings[name] = text;
            }
            else if (value.TryRead<double>(out var number))
            {
                strings[name] = number % 1 == 0 && Math.Abs(number) < 1e15
                    ? ((long)number).ToString(CultureInfo.InvariantCulture)
                    : number.ToString(CultureInfo.InvariantCulture);
            }
            else if (value.TryRead<bool>(out var flag))
            {
                strings[name] = flag ? "true" : "false";
            }
        }

        return strings;
    }

    private static string ReasonOf(GumpCloseReasonType reason)
    {
        return reason switch
        {
            GumpCloseReasonType.Replaced => "replaced",
            GumpCloseReasonType.Disconnect => "disconnect",
            _ => "server"
        };
    }

    private static long PlayerOf(GameSession session)
    {
        return session.CharacterId.Value;
    }

    private bool TryGetSession(long player, out GameSession session)
    {
        session = null!;

        return player is > 0 and <= uint.MaxValue &&
               _sessions.TryGetByCharacterId(new Serial((uint)player), out session!);
    }
}
