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
[ScriptModule("jail", "The jail of data/jail.toml: its cells and who is in them, sending a player or an NPC to a cell for some days, and releasing it early. The module does not check who calls it: a script that is for the staff checks world.is_staff first.")]
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
    ///     <c>{ number, x, y, z, map }</c>, and an occupied one also has <c>prisoner</c>, <c>name</c> and
    ///     <c>seconds_left</c> and <c>reason</c>.
    /// </summary>
    [ScriptFunction(helpText: "The cells of the jail in file order, as an array of { number, x, y, z, map } (map is the MapType number of the jail, for mobile.teleport); a cell that holds a prisoner also has prisoner (its serial), name, seconds_left and reason (empty when none was given). An empty array when there is no data/jail.toml. A cell whose sentence is over is free, even while its prisoner is offline.")]
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
            }

            cells[index++] = entry;
        }

        return cells;
    }

    /// <summary>
    ///     Gets the sentence of a mobile; <c>local sentence = jail.sentence(who)</c>. It is
    ///     <c>{ cell, days, seconds_left, by, reason }</c>.
    /// </summary>
    [ScriptFunction(helpText: "The sentence of the mobile as { cell, days, seconds_left, by, reason }, by being the name of who jailed it and reason what it gave, empty for none; nil when it is not in jail. seconds_left is 0 for a sentence that is over and waits for its prisoner to come back.")]
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

        return table;
    }

    /// <summary>
    ///     Sends a mobile to a cell for a number of days; <c>jail.send(target, 2, 3, who, "Stole a horse") == JailResultType.Ok</c>.
    /// </summary>
    [ScriptFunction(helpText: "Sends the mobile, a player or an NPC, to the cell for that many real days, in the name of by, and gives a JailResultType: Ok, Disabled (no data/jail.toml), NoSuchCell, CellOccupied, BadDays (not a whole number from 1 to jail.max_days()), NotInWorld (the mobile or by), Refused (by itself, or a player of by's rank or above) or MapNotLoaded. The optional reason is kept as one line of at most 100 characters, told to the mobile and written on its release note. The mobile is told the days, goes back where it was when they are over, pays the fine of ultima.jail.fine_gold and gets a release note. A mobile already in jail moves to the cell with a sentence that starts now.")]
    public JailResultType Send(long serial, double cell, double days, long by, string? reason = null)
    {
        if (!TryGetMobile(serial, out var prisoner) || !TryGetMobile(by, out var jailer))
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

        return _jail.Jail(prisoner, (int)cell, (int)days, jailer, reason);
    }

    /// <summary>
    ///     Ends the sentence of a mobile now, with no fine and no note; <c>jail.release(prisoner)</c>.
    /// </summary>
    [ScriptFunction(helpText: "Ends the sentence of the mobile now, with no fine and no release note: a prisoner in the world goes back at once where it was arrested, a player who is offline at its next login. False when the mobile is not in jail.")]
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

    private static long SecondsLeft(JailSentenceEntity sentence, long now)
    {
        return Math.Max(0, (sentence.ReleaseAt - now) / 1000);
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
