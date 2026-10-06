using System.Globalization;
using System.Text.RegularExpressions;
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
using Moongate.Server.Ultima.Loaders;
using Moongate.Server.Ultima.Modules.Internal;
using Moongate.Server.Ultima.Services.Internal;
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
public sealed partial class GumpModule
{
    private readonly ILogger _logger = Log.ForContext<GumpModule>();
    private readonly ISessionService _sessions;
    private readonly IGumpService _gumps;
    private readonly IGumpTemplateService _templates;
    private readonly Lazy<IGumpScriptService> _scripts;
    private readonly IGameLoopService _loop;

    public GumpModule(
        ISessionService sessions,
        IGumpService gumps,
        IGumpTemplateService templates,
        Lazy<IGumpScriptService> scripts,
        IGameLoopService loop
    )
    {
        _sessions = sessions;
        _gumps = gumps;
        _templates = templates;
        _scripts = scripts;
        _loop = loop;
    }

    /// <summary>
    ///     Opens gump <paramref name="id" /> on the player, its <c>${name}</c> filled from <paramref name="args" />,
    ///     which
    ///     its script gets back with every answer; <c>gump.open(player, "release_pet", { pet_name = "Fido" })</c>.
    /// </summary>
    [ScriptFunction(
        helpText:
        "Opens the gump templates/gumps/<id>.xml on the player, its ${name} filled from args; scripts/gumps/<id>.lua gets the answer. From a script it opens on the next turn of the game loop. False for an unknown player or gump."
    )]
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

        return RunOrPost(() => OpenNow(session, template, player, table));
    }

    /// <summary>
    ///     Starts a gump built from Lua; <c>local g = gump.create("pet_list")</c>, then
    ///     <c>g:text{ x = 20, y = 20, text = "Pets" }</c> and the other controls, with the attributes of the XML
    ///     elements of the same name.
    /// </summary>
    [ScriptFunction(
        helpText:
        "Starts a gump built from Lua: add controls with g:text{...}, g:button{...}, g:paginate(...), ..., then gump.send. A button's on_click may be a function."
    )]
    public LuaTable Create(string id, int x = 0, int y = 0)
    {
        if (!GumpId().IsMatch(id))
        {
            throw new ArgumentException(
                $"A gump id is lower case letters, digits and _, starting with a letter or _: '{id}'."
            );
        }

        return GumpBuilder.Create(id, x, y);
    }

    /// <summary>
    ///     Opens a gump built with <see cref="Create" /> on the player; its buttons call their <c>on_click</c>
    ///     function, or
    ///     the function of that name of <c>scripts/gumps/&lt;id&gt;.lua</c>; <c>gump.send(player, g, args)</c>.
    /// </summary>
    [ScriptFunction(
        helpText:
        "Opens a gump built with gump.create on the player, from a script on the next turn of the game loop; false for an unknown player."
    )]
    public bool Send(long player, LuaTable gump, LuaTable? args = null)
    {
        if (!TryGetSession(player, out var session))
        {
            return false;
        }

        var id = GumpBuilder.IdOf(gump);
        var (x, y) = GumpBuilder.PositionOf(gump);
        var built = new Dictionary<string, LuaFunction>(StringComparer.Ordinal);
        var (controls, pages) = GumpBuilder.ToXml(gump, 0, 0, built);
        var root = new XElement(
            "gump",
            new XAttribute("id", id),
            new XAttribute("x", x),
            new XAttribute("y", y),
            controls,
            pages
        );

        // Checked as a file is, so a mistake fails here with its reason instead of drawing a broken gump.
        GumpsLoader.Validate($"gump.send('{id}')", root, _templates.Exists);

        // The callbacks belong with the script that built the gump: reloading it ends what they left waiting.
        var owner = _scripts.Value.CurrentScript ?? $"gumps/{id}.lua";
        var functions = built.ToDictionary(pair => pair.Key, pair => (pair.Value, owner), StringComparer.Ordinal);
        var table = args ?? new LuaTable();

        return RunOrPost(() => OpenTemplate(
                session,
                new() { Id = id, File = "(built from Lua)", Root = root },
                table,
                functions
            )
        );
    }

    /// <summary>
    ///     Closes the player's gump <paramref name="id" />; its script gets <c>on_close</c> with <c>server</c>;
    ///     <c>gump.close(player, "release_pet")</c>. Called from a script, the gump closes on the next turn of the game
    ///     loop and the answer is true whether it is open or not.
    /// </summary>
    [ScriptFunction(
        helpText:
        "Closes the player's gump, from a script on the next turn of the game loop, so it gives true even for a gump that is not open; false for an unknown player."
    )]
    public bool Close(long player, string id)
    {
        return TryGetSession(player, out var session) && RunOrPost(() => _gumps.Close(session, id));
    }

    // From inside a running script, opening or closing a gump would run other script functions (slots, on_close) as
    // nested coroutines, which the engine refuses: the work goes to the next turn of the game loop instead.
    private bool RunOrPost(Func<bool> work)
    {
        return _scripts.Value.IsRunningScript ? _loop.TryPost(new LoopActionWorkItem(() => work())) : work();
    }

    private bool OpenNow(GameSession session, GumpTemplate template, long player, LuaTable table)
    {
        var functions = new Dictionary<string, (LuaFunction Function, string Owner)>(StringComparer.Ordinal);

        if (template.Root.Descendants("slot").Any())
        {
            if (FillSlots(template, player, table, functions) is not { } root)
            {
                return false;
            }

            try
            {
                GumpsLoader.Validate($"{template.File} (slots filled)", root, _templates.Exists);
            }
            catch (InvalidDataException exception)
            {
                _logger.Error(exception, "Gump {Gump}: its slots made a gump that cannot open", template.Id);

                return false;
            }

            template = new() { Id = template.Id, File = template.File, Root = root };
        }

        return OpenTemplate(session, template, table, functions);
    }

    private bool OpenTemplate(
        GameSession session,
        GumpTemplate template,
        LuaTable table,
        Dictionary<string, (LuaFunction Function, string Owner)> functions
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
    // A slot function that fails or waits leaves the gump unopened; a missing one leaves the slot empty.
    private XElement? FillSlots(
        GumpTemplate template,
        long player,
        LuaTable table,
        Dictionary<string, (LuaFunction Function, string Owner)> functions
    )
    {
        var root = new XElement(template.Root);
        var owner = $"gumps/{template.Id}.lua";

        foreach (var slot in root.Descendants("slot").ToList())
        {
            // Safe: the XSD requires this attribute.
            var name = (string)slot.Attribute("name")!;
            var builder = GumpBuilder.Create(template.Id, 0, 0);
            var result = _scripts.Value.Call(template.Id, name, builder, player, table);

            switch (result.Kind)
            {
                case ScriptResultKind.Missing:
                    _logger.Warning("Gump {Gump}: slot {Slot} has no function in {Owner}", template.Id, name, owner);

                    break;
                case ScriptResultKind.Failed or ScriptResultKind.Suspended:
                    _logger.Error(
                        "Gump {Gump}: slot function {Slot} {Outcome}, so the gump does not open",
                        template.Id,
                        name,
                        result.Kind == ScriptResultKind.Failed ? "failed" : "called wait()"
                    );

                    return null;
            }

            var built = new Dictionary<string, LuaFunction>(StringComparer.Ordinal);
            var (controls, pages) = GumpBuilder.ToXml(builder, Coordinate(slot, "x"), Coordinate(slot, "y"), built);

            foreach (var (key, function) in built)
            {
                // Numbered per slot, so two slots never share a name.
                functions[$"{key}_{name}"] = (function, owner);
                foreach (var button in controls.Concat(pages).SelectMany(element => element.DescendantsAndSelf("button")))
                {
                    if ((string?)button.Attribute("on_click") == key)
                    {
                        button.SetAttributeValue("on_click", $"{key}_{name}");
                    }
                }
            }

            slot.ReplaceWith(controls);
            root.Add(pages);
        }

        return root;
    }

    [GeneratedRegex("^[a-z_][a-z0-9_]*$")]
    private static partial Regex GumpId();

    private static int Coordinate(XElement element, string name)
    {
        return int.TryParse(
            (string?)element.Attribute(name),
            NumberStyles.Integer,
            CultureInfo.InvariantCulture,
            out var value
        )
            ? value
            : 0;
    }

    private void Answered(
        string id,
        GameSession session,
        GumpTemplateAnswer answered,
        LuaTable args,
        IReadOnlyDictionary<string, (LuaFunction Function, string Owner)> functions
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
                bool flag   => flag,
                long number => number,
                _           => (string)value
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
            _scripts.Value.CallFunction(callback.Owner, callback.Function, player, answer, args);
        }
        else if (answered.Click is { } function)
        {
            if (_scripts.Value.Call(id, function, player, answer, args).Kind == ScriptResultKind.Missing)
            {
                _logger.Warning(
                    "Gump {Gump}: on_click {Function} is not a function of scripts/gumps/{Gump}.lua",
                    id,
                    function,
                    id
                );
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
            GumpCloseReasonType.Replaced   => "replaced",
            GumpCloseReasonType.Disconnect => "disconnect",
            _                              => "server"
        };
    }

    private static long PlayerOf(GameSession session)
    {
        return session.CharacterId.Value;
    }

    private bool TryGetSession(long player, out GameSession session)
    {
        // Safe: out parameter; callers read it only when the method returns true.
        session = null!;

        return player is > 0 and <= uint.MaxValue &&
               // Safe: the out value is only used when the lookup succeeds.
               _sessions.TryGetByCharacterId(new Serial((uint)player), out session!);
    }
}
