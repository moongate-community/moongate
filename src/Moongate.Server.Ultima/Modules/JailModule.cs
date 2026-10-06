using Lua;
using Moongate.Core.Primitives;
using Moongate.Scripting.Attributes.Scripts;
using Moongate.Server.Ultima.Entities.World;
using Moongate.Server.Ultima.Interfaces;
using Moongate.Server.Ultima.Types.Jail;

namespace Moongate.Server.Ultima.Modules;

/// <summary>
///     The <c>jail</c> Lua module: the cells of <c>data/jail.toml</c>, who is in them, and sending a mobile to one or
///     releasing it, for the gump of the <c>.jail</c> command; <c>jail.send(target, 2, 3, who)</c>.
/// </summary>
[ScriptModule(
    "jail",
    "The jail of data/jail.toml: its cells and who is in them, sending a player or an NPC to a cell for some days, and releasing it early. The module does not check who calls it: a script that is for the staff checks world.is_staff first."
)]
public sealed class JailModule
{
    private readonly IJailService _jail;
    private readonly IMobileService _mobiles;
    private readonly TimeProvider _time;

    public JailModule(IJailService jail, IMobileService mobiles, TimeProvider time)
    {
        _jail = jail;
        _mobiles = mobiles;
        _time = time;
    }

    /// <summary>
    ///     Gets the cells in the order of the file; <c>for _, cell in ipairs(jail.cells()) do ... end</c>. Each is
    ///     <c>{ number, x, y, z, map }</c>, and an occupied one also has <c>prisoner</c>, <c>name</c>,
    ///     <c>seconds_left</c>, <c>reason</c> and <c>pending</c>.
    /// </summary>
    [ScriptFunction(
        helpText:
        "The cells of the jail in file order, as an array of { number, x, y, z, map } (map is the MapType number of the jail, for mobile.teleport); a cell that holds a prisoner also has prisoner (its serial), name, seconds_left, reason (empty when none was given) and pending. pending is true for a cell kept for a player who was offline when it was jailed and has not logged in since: nobody is inside yet, and seconds_left is the whole sentence. An empty array when there is no data/jail.toml. A cell whose sentence is over is free, even while its prisoner is offline."
    )]
    public LuaTable Cells()
    {
        var cells = new LuaTable();
        var now = Now();
        var index = 1;

        foreach (var cell in _jail.Cells)
        {
            var entry = new LuaTable();
            entry["number"] = cell.Number;
            entry["x"] = cell.Location.X;
            entry["y"] = cell.Location.Y;
            entry["z"] = cell.Location.Z;

            if (_jail.Map is { } map)
            {
                entry["map"] = (int)map;
            }

            if (_jail.GetOccupant(cell.Number) is { } occupant)
            {
                entry["prisoner"] = (long)occupant.Id.Value;
                entry["name"] = occupant.Name;
                entry["seconds_left"] = SecondsLeft(occupant, now);
                entry["reason"] = occupant.Reason;
                entry["pending"] = occupant.Pending;
            }

            cells[index++] = entry;
        }

        return cells;
    }

    /// <summary>
    ///     Gets the sentence of a mobile; <c>local sentence = jail.sentence(who)</c>. It is
    ///     <c>{ cell, days, seconds_left, by, reason, pending }</c>.
    /// </summary>
    [ScriptFunction(
        helpText:
        "The sentence of the mobile as { cell, days, seconds_left, by, reason, pending }, by being the name of who jailed it and reason what it gave, empty for none; nil when it is not in jail. seconds_left is 0 for a sentence that is over and waits for its prisoner to come back. pending is true for a sentence given to a player who was offline and has not logged in since: its days have not started, and seconds_left is its whole length."
    )]
    public LuaTable? Sentence(long serial)
    {
        if (serial is <= 0 or > uint.MaxValue || _jail.GetSentence(new Serial((uint)serial)) is not { } sentence)
        {
            return null;
        }

        var table = new LuaTable();
        table["cell"] = sentence.Cell;
        table["days"] = sentence.Days;
        table["seconds_left"] = SecondsLeft(sentence, Now());
        table["by"] = sentence.JailedBy;
        table["reason"] = sentence.Reason;
        table["pending"] = sentence.Pending;

        return table;
    }

    /// <summary>
    ///     Sends a mobile to a cell for a number of days;
    ///     <c>jail.send(target, 2, 3, who, "Stole a horse") == JailResultType.Ok</c>.
    /// </summary>
    [ScriptFunction(
        helpText:
        "Sends the mobile, a player or an NPC, to the cell for that many real days, in the name of by, and gives a JailResultType: Ok, Pending, Disabled (no data/jail.toml), NoSuchCell, CellOccupied, BadDays (not a whole number from 1 to jail.max_days()), NotInWorld (the mobile or by), Refused (by itself, or a player of by's rank or above) or MapNotLoaded. The optional reason is kept as one line of at most 100 characters, told to the mobile and written on its release note. The mobile is told the days, goes back where it was when they are over, pays the fine of ultima.jail.fine_gold and gets a release note. A mobile already in jail moves to the cell with a sentence that starts now. A player who is offline can be sent only after .jail <name> found it: the answer is Pending, the cell is kept for it, and at its next login it is taken there and its days start; any other serial that is not in the world is NotInWorld."
    )]
    public JailResultType Send(long serial, double cell, double days, long by, string? reason = null)
    {
        if (serial is <= 0 or > uint.MaxValue || !TryGetMobile(by, out var jailer))
        {
            return JailResultType.NotInWorld;
        }

        // Lua numbers: a cell or a sentence with a fraction, or beyond an int, is none.
        if (!IsWhole(cell))
        {
            return JailResultType.NoSuchCell;
        }

        if (!IsWhole(days))
        {
            return JailResultType.BadDays;
        }

        // Not in the world: the jail knows whether it is a player it found by name.
        return TryGetMobile(serial, out var prisoner)
            ? _jail.Jail(prisoner, (int)cell, (int)days, jailer, reason)
            : _jail.JailOffline(new Serial((uint)serial), (int)cell, (int)days, jailer, reason);
    }

    /// <summary>
    ///     Ends the sentence of a mobile now, with no fine and no note; <c>jail.release(prisoner)</c>.
    /// </summary>
    [ScriptFunction(
        helpText:
        "Ends the sentence of the mobile now, with no fine and no release note: a prisoner in the world goes back at once where it was arrested, a player who is offline at its next login. False when the mobile is not in jail."
    )]
    public bool Release(long serial)
    {
        return serial is > 0 and <= uint.MaxValue && _jail.Pardon(new Serial((uint)serial));
    }

    /// <summary>
    ///     Gets the longest sentence in days; <c>jail.max_days()</c>.
    /// </summary>
    [ScriptFunction(helpText: "The longest sentence in days, from the setting ultima.jail.max_days.")]
    public int MaxDays()
    {
        return _jail.MaxDays;
    }

    private static bool IsWhole(double value)
    {
        return value is >= int.MinValue and <= int.MaxValue && Math.Floor(value) == value;
    }

    // A sentence that waits has all its days left.
    private static long SecondsLeft(JailSentenceEntity sentence, long now)
    {
        return sentence.Pending ? sentence.Days * 86_400L : Math.Max(0, (sentence.ReleaseAt - now) / 1000);
    }

    private long Now()
    {
        return _time.GetUtcNow().ToUnixTimeMilliseconds();
    }

    private bool TryGetMobile(long serial, out MobileEntity mobile)
    {
        mobile = null!;

        return serial is > 0 and <= uint.MaxValue && _mobiles.TryGet(new Serial((uint)serial), out mobile!);
    }
}
