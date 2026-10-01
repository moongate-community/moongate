using System.Globalization;
using Lua;
using Moongate.Core.Primitives;
using Moongate.Scripting.Attributes.Scripts;
using Moongate.Server.Core.Data.Sessions;
using Moongate.Server.Core.Interfaces.Services;
using Moongate.Server.Ultima.Data.Gumps;
using Moongate.Server.Ultima.Data.Templates.Gumps;
using Moongate.Server.Ultima.Interfaces;
using Moongate.Server.Ultima.Interfaces.Loaders;
using Moongate.Server.Ultima.Services.Internal;
using Moongate.Server.Ultima.Types.Gumps;
using Serilog;

namespace Moongate.Server.Ultima.Modules;

/// <summary>
///     The <c>gump</c> Lua module: opens the gumps of <c>templates/gumps</c> on players and calls their script,
///     <c>scripts/gumps/&lt;id&gt;.lua</c>, with the answer: an <c>on_click</c> button calls the function it names, an
///     <c>id</c> button calls <c>on_button</c>, and <c>on_close</c> runs when the gump goes away without a button,
///     with the reason: <c>player</c>, <c>replaced</c>, <c>server</c> or <c>disconnect</c>.
/// </summary>
[ScriptModule("gump", "Opens the gumps of templates/gumps on players; their script gets the answer.")]
public sealed class GumpModule
{
    private readonly ILogger _logger = Log.ForContext<GumpModule>();
    private readonly ISessionService _sessions;
    private readonly IGumpService _gumps;
    private readonly IDataLoaderService _data;
    private readonly Lazy<IGumpScriptService> _scripts;
    private readonly ILocalizationService? _localization;

    public GumpModule(
        ISessionService sessions,
        IGumpService gumps,
        IDataLoaderService data,
        Lazy<IGumpScriptService> scripts,
        ILocalizationService? localization = null
    )
    {
        _sessions = sessions;
        _gumps = gumps;
        _data = data;
        _scripts = scripts;
        _localization = localization;
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

        var template = _data.GetEntities<GumpTemplate>().FirstOrDefault(gump => gump.Id == id);

        if (template is null)
        {
            _logger.Warning("No gump {Gump} in templates/gumps", id);

            return false;
        }

        var table = args ?? new LuaTable();
        var rendered = GumpXmlRenderer.Render(template, Strings(table), _localization);
        _gumps.Open(
            session,
            new()
            {
                Id = id, Layout = rendered.Layout, X = rendered.X, Y = rendered.Y,
                OnResponse = (answered, response) => Answered(id, answered, response, rendered.Clicks, table),
                OnClosed = (closed, reason) => _scripts.Value.Call(id, "on_close", PlayerOf(closed), table, ReasonOf(reason))
            }
        );

        return true;
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

    private void Answered(
        string id,
        GameSession session,
        GumpResponse response,
        IReadOnlyDictionary<int, string> clicks,
        LuaTable args
    )
    {
        var player = PlayerOf(session);

        if (response.ButtonId == 0)
        {
            _scripts.Value.Call(id, "on_close", player, args, "player");

            return;
        }

        var answer = ToLua(response);

        if (clicks.TryGetValue(response.ButtonId, out var function))
        {
            _scripts.Value.Call(id, function, player, answer, args);
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
