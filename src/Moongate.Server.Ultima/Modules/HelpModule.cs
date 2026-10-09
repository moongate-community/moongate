using Lua;
using Moongate.Core.Primitives;
using Moongate.Scripting.Attributes.Scripts;
using Moongate.Server.Ultima.Data.Cities;
using Moongate.Server.Ultima.Data.Config;
using Moongate.Server.Ultima.Data.Help;
using Moongate.Server.Ultima.Entities.World;
using Moongate.Server.Ultima.Interfaces;
using Moongate.Server.Ultima.Interfaces.Loaders;
using Moongate.Server.Ultima.Types.Help;

namespace Moongate.Server.Ultima.Modules;

/// <summary>
///     The <c>help</c> Lua module: the settings of the help gump, the starting city nearest to a character for the "I
///     am
///     stuck" button ( <c>help.nearest_city(player)</c>) and the queue of the requests for the game masters
///     ( <c>help.create_page(player, kind, text)</c>).
/// </summary>
[ScriptModule(
    "help",
    "What the help gump needs from the server: the wait and the pause of the I am stuck button, the starting city nearest to a character, and the queue of the requests for the game masters (create, list, take, answer, close). The module does not check who calls it: a script that is for the staff checks world.is_staff first."
)]
public sealed class HelpModule
{
    private readonly IMobileService _mobiles;
    private readonly IDataLoaderService _data;
    private readonly HelpConfig _config;
    private readonly IHelpPageService _pages;
    private readonly TimeProvider _time;

    public HelpModule(
        IMobileService mobiles,
        IDataLoaderService data,
        HelpConfig config,
        IHelpPageService pages,
        TimeProvider time
    )
    {
        _mobiles = mobiles;
        _data = data;
        _config = config;
        _pages = pages;
        _time = time;
    }

    /// <summary>
    ///     Says whether a player may ask for a game master now; <c>local c = help.can_page(player)</c>.
    /// </summary>
    [ScriptFunction(
        helpText:
        "Whether the player may ask for a game master now, as { ok } when it may, or { ok = false, reason, seconds } when it may not: reason is open (it has a request open or taken) or wait (it asked too soon; seconds is how long to wait), and reason is gone for a player not in the world."
    )]
    public LuaTable CanPage(long player)
    {
        var answer = new LuaTable();

        if (!TryGetMobile(player, out var mobile))
        {
            answer["ok"] = false;
            answer["reason"] = "gone";

            return answer;
        }

        return Describe(_pages.CanCreate(mobile.Id), answer);
    }

    /// <summary>
    ///     Makes a request for a game master in the name of a player;
    ///     <c>local made = help.create_page(player, HelpPageKindType.Bug, "The door is stuck")</c>.
    /// </summary>
    [ScriptFunction(
        helpText:
        "Makes a request for the game masters in the name of the player, of a HelpPageKindType and with a line of text (cut at 128 characters), and tells the game masters in the world. It gives { id } with the number of the request, or { reason, seconds } when none was made: reason is open (the player has a request open or taken), wait (it asked too soon; seconds is how long to wait), text (the text is empty or the kind is none of the four) or gone (the player is not in the world)."
    )]
    public LuaTable CreatePage(long player, double kind, string text)
    {
        var answer = new LuaTable();

        if (!TryGetMobile(player, out var mobile))
        {
            answer["reason"] = "gone";

            return answer;
        }

        if (kind is < 0 or > (int)HelpPageKindType.Harassment || kind != Math.Floor(kind))
        {
            answer["reason"] = "text";

            return answer;
        }

        var result = _pages.Create(mobile, (HelpPageKindType)(int)kind, text);

        if (result.Type == HelpPageCreateResultType.Ok)
        {
            answer["id"] = (long)result.Page!.Id.Value;

            return answer;
        }

        return Describe(result, answer, false);
    }

    /// <summary>
    ///     Gets the number of the requests that wait; <c>help.waiting()</c>.
    /// </summary>
    [ScriptFunction(helpText: "How many requests for the game masters are open or taken.")]
    public int Waiting()
    {
        return _pages.WaitingCount;
    }

    /// <summary>
    ///     Gets the open and taken requests, the oldest first; <c>for _, page in ipairs(help.pages()) do ... end</c>.
    /// </summary>
    [ScriptFunction(
        helpText:
        "The requests for the game masters that are open or taken, the oldest first, as an array of { id, player, name, account, kind, status, text, taken_by, answer, age_seconds, map, x, y, z, online }: kind is a HelpPageKindType, status a HelpPageStatusType, map the MapType number where the player asked, online whether the player is in the world."
    )]
    public LuaTable Pages()
    {
        var list = new LuaTable();
        var index = 1;

        foreach (var page in _pages.Active())
        {
            list[index++] = Describe(page);
        }

        return list;
    }

    /// <summary>
    ///     Gets one request, closed or not; <c>local page = help.page(id)</c>.
    /// </summary>
    [ScriptFunction(
        helpText:
        "One request for the game masters by its number, in the shape of help.pages, closed ones included; nil when there is none."
    )]
    public LuaTable? Page(long id)
    {
        return id is > 0 and <= uint.MaxValue && _pages.Get(new Serial((uint)id)) is { } page ? Describe(page) : null;
    }

    /// <summary>
    ///     Marks a request as taken by a game master; <c>help.take(id, staff)</c>.
    /// </summary>
    [ScriptFunction(
        helpText:
        "Marks the request as taken by the game master, whose name it keeps; a request taken by another can be taken over. False for an unknown or closed request, and for a staff not in the world."
    )]
    public bool Take(long id, long staff)
    {
        return TryGetRequest(id, out var serial) && TryGetMobile(staff, out var mobile) && _pages.Take(serial, mobile.Name);
    }

    /// <summary>
    ///     Answers a request and closes it; <c>help.answer(id, staff, "Go north")</c>.
    /// </summary>
    [ScriptFunction(
        helpText:
        "Answers the request in the name of the game master and closes it: the player reads the answer at once, or at its next login when it is offline (cut at 128 characters). False for an unknown or closed request, an empty answer, and a staff not in the world."
    )]
    public bool Answer(long id, long staff, string text)
    {
        return TryGetRequest(id, out var serial) && TryGetMobile(staff, out var mobile) &&
               _pages.Answer(serial, mobile.Name, text);
    }

    /// <summary>
    ///     Closes a request with no answer; <c>help.close(id, staff)</c>.
    /// </summary>
    [ScriptFunction(
        helpText:
        "Closes the request with no answer, in the name of the game master. False for an unknown or closed request and for a staff not in the world."
    )]
    public bool Close(long id, long staff)
    {
        return TryGetRequest(id, out var serial) && TryGetMobile(staff, out var mobile) && _pages.Close(serial, mobile.Name);
    }

    /// <summary>
    ///     Gets the starting city nearest to a character on its map; <c>local city = help.nearest_city(player)</c>.
    /// </summary>
    [ScriptFunction(
        helpText:
        "The starting city of data/starting_cities.toml nearest to the character, on the map it stands on, as { town, x, y, z, map } (map is the MapType number, for mobile.teleport). When no city is on that map it is the first city of the file. Nil when the mobile is not in the world or the file has no city."
    )]
    public LuaTable? NearestCity(long serial)
    {
        if (serial is <= 0 or > uint.MaxValue || !_mobiles.TryGet(new Serial((uint)serial), out var mobile))
        {
            return null;
        }

        var cities = _data.GetEntities<StartingCityContent>();
        var city = cities
                       .Where(candidate => candidate.Map == mobile.Map)
                       .OrderBy(candidate => Squared(candidate, mobile.Location.X, mobile.Location.Y))
                       .FirstOrDefault() ??
                   (cities.Count > 0 ? cities[0] : null);

        if (city is null)
        {
            return null;
        }

        var table = new LuaTable();
        table["town"] = city.Town;
        table["x"] = city.Location.X;
        table["y"] = city.Location.Y;
        table["z"] = city.Location.Z;
        table["map"] = (int)city.Map;

        return table;
    }

    /// <summary>
    ///     Gets the settings of the gump; <c>local s = help.settings()</c>.
    /// </summary>
    [ScriptFunction(
        helpText:
        "The settings of ultima.help as { wait_seconds, cooldown_minutes }: the seconds a character must stand still before I am stuck moves it, and the minutes before it can ask again (0 for no pause)."
    )]
    public LuaTable Settings()
    {
        var table = new LuaTable();
        table["wait_seconds"] = _config.StuckWaitSeconds;
        table["cooldown_minutes"] = _config.StuckCooldownMinutes;

        return table;
    }

    private static LuaTable Describe(HelpPageCreateResult result, LuaTable answer, bool withOk = true)
    {
        var ok = result.Type == HelpPageCreateResultType.Ok;

        if (withOk)
        {
            answer["ok"] = ok;
        }

        if (!ok)
        {
            answer["reason"] = result.Type switch
            {
                HelpPageCreateResultType.AlreadyOpen => "open",
                HelpPageCreateResultType.Wait        => "wait",
                _                                    => "text"
            };

            if (result.Type == HelpPageCreateResultType.Wait)
            {
                answer["seconds"] = result.WaitSeconds;
            }
        }

        return answer;
    }

    private LuaTable Describe(HelpPageEntity page)
    {
        var table = new LuaTable();
        table["id"] = (long)page.Id.Value;
        table["player"] = (long)page.Player.Value;
        table["name"] = page.PlayerName;
        table["account"] = (long)page.AccountId.Value;
        table["kind"] = (int)page.Kind;
        table["status"] = (int)page.Status;
        table["text"] = page.Text;
        table["taken_by"] = page.TakenBy;
        table["answer"] = page.Answer;
        table["age_seconds"] = Math.Max(0, (_time.GetUtcNow().ToUnixTimeMilliseconds() - page.CreatedAt) / 1000);
        table["map"] = (int)page.Map;
        table["x"] = page.X;
        table["y"] = page.Y;
        table["z"] = page.Z;
        table["online"] = _mobiles.TryGet(page.Player, out _);

        return table;
    }

    private bool TryGetMobile(long serial, out MobileEntity mobile)
    {
        // Safe: out parameter; callers read it only when the method returns true.
        mobile = null!;

        // Safe: the out value is only used when the lookup succeeds.
        return serial is > 0 and <= uint.MaxValue && _mobiles.TryGet(new Serial((uint)serial), out mobile!);
    }

    private static bool TryGetRequest(long id, out Serial serial)
    {
        serial = default;

        if (id is <= 0 or > uint.MaxValue)
        {
            return false;
        }

        serial = new Serial((uint)id);

        return true;
    }

    private static long Squared(StartingCityContent city, int x, int y)
    {
        long dx = city.Location.X - x;
        long dy = city.Location.Y - y;

        return dx * dx + dy * dy;
    }
}
