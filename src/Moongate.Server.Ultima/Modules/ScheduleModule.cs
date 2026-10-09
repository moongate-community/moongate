using Lua;
using Moongate.Scripting.Attributes.Scripts;
using Moongate.Server.Ultima.Data.Schedule;
using Moongate.Server.Ultima.Interfaces;

namespace Moongate.Server.Ultima.Modules;

/// <summary>
///     The
///     <c>
///         schedule
///     </c>
///     Lua module: the seasonal events of
///     <c>
///         data/schedule.toml
///     </c>
///     and the next run of its tasks;
///     <c>
///         if schedule.is_active("halloween") then ... end
///     </c>
///     .
/// </summary>
[ScriptModule(
    "schedule",
    "The calendar of data/schedule.toml: whether a seasonal event is on, the list of events, switching one on or off like the .event command, and when a timed task runs next. A script cannot add entries to the calendar: a custom action is a task with action = \"lua\"."
)]
public sealed class ScheduleModule
{
    private readonly ISeasonalEventService _events;
    private readonly IScheduleService _schedule;

    public ScheduleModule(ISeasonalEventService events, IScheduleService schedule)
    {
        _events = events;
        _schedule = schedule;
    }

    /// <summary>
    ///     Tells whether an event is on;
    ///     <c>
    ///         schedule.is_active("halloween")
    ///     </c>
    ///     .
    /// </summary>
    [ScriptFunction(
        helpText:
        "True when the seasonal event is on now, by its dates or because the staff forced it on; false when it is off and for an id that is not in data/schedule.toml."
    )]
    public bool IsActive(string id)
    {
        return _events.IsActive(id);
    }

    /// <summary>
    ///     Gets the events that are on;
    ///     <c>
    ///         for _, e in ipairs(schedule.active()) do ... end
    ///     </c>
    ///     . Each is
    ///     <c>
    ///         { id, name, mode }
    ///     </c>
    ///     .
    /// </summary>
    [ScriptFunction(
        helpText:
        "The seasonal events that are on now, as an array of { id, name, mode }, mode being auto, on or off. An empty array when none is."
    )]
    public LuaTable Active()
    {
        return ToTable(_events.Events.Where(item => item.Active), false);
    }

    /// <summary>
    ///     Gets every event;
    ///     <c>
    ///         for _, e in ipairs(schedule.events()) do ... end
    ///     </c>
    ///     . Each is
    ///     <c>
    ///         { id, name, from, to, mode, active }
    ///     </c>
    ///     .
    /// </summary>
    [ScriptFunction(
        helpText:
        "Every seasonal event of data/schedule.toml in file order, as an array of { id, name, from, to, mode, active }: from and to are the first and last day of the window as month-day, mode is auto, on or off, active is whether it is on now."
    )]
    public LuaTable Events()
    {
        return ToTable(_events.Events, true);
    }

    /// <summary>
    ///     Changes the mode of an event;
    ///     <c>
    ///         schedule.set_event("halloween", "off")
    ///     </c>
    ///     .
    /// </summary>
    [ScriptFunction(
        helpText:
        "Sets the mode of the event to auto (follow the dates), on or off, as the .event command does, and runs its on_start or on_end hook if its state changes. True when done; false for an unknown id or mode. The module does not check who calls it: a script that is for the staff checks world.is_staff first. The mode is kept across restarts."
    )]
    public bool SetEvent(string id, string mode)
    {
        return _events.SetMode(id, mode);
    }

    /// <summary>
    ///     Gets when a timed task runs next, in Unix seconds;
    ///     <c>
    ///         schedule.next("nightly_restart")
    ///     </c>
    ///     .
    /// </summary>
    [ScriptFunction(
        helpText:
        "The Unix time in seconds of the next run of the timed task, the stop for a shutdown task; nil for an id that is not a task of data/schedule.toml, an event's included."
    )]
    public long? Next(string id)
    {
        return _schedule.NextOccurrence(id)?.ToUnixTimeSeconds();
    }

    private static LuaTable ToTable(IEnumerable<SeasonalEventState> events, bool full)
    {
        var table = new LuaTable();
        var index = 1;

        foreach (var item in events)
        {
            var entry = new LuaTable();
            entry["id"] = item.Id;
            entry["name"] = item.Name;
            entry["mode"] = item.Mode;

            if (full)
            {
                entry["from"] = item.From;
                entry["to"] = item.To;
                entry["active"] = item.Active;
            }

            table[index++] = entry;
        }

        return table;
    }
}
