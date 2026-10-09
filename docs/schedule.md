# Schedule

`data/schedule.toml` is the calendar of the server. It holds two things: **tasks** that run at a
time of the day or week, such as a shutdown with warnings, and **seasonal events** that are on
between two dates, such as Halloween. No task runs until the operator writes one: the shipped
file has the tasks as examples in comments, and the event `halloween` on (see [Holidays](holidays.md)).

```toml
[[task]]
id = "nightly_restart"
when = { every = "day", at = "04:00" }
action = "shutdown"
warnings = [600, 300, 60, 10]

[[task]]
id = "sunday_tip"
when = { every = "week", days = ["sun"], at = "18:00" }
action = "broadcast"
text = "Remember to visit the bank."

[[task]]
id = "daily_cleanup"
when = { every = "day", at = "06:00" }
action = "lua"
script = "cleanup"

[[event]]
id = "halloween"
name = "Halloween"
from = "10-20"
to = "11-02"
```

## Tasks

| Key | Meaning |
| --- | --- |
| `id` | The name of the task and of the event. Lower case letters, digits and `_`, at most 40, unique in the file. |
| `when.every` | `hour`, `day` or `week`. |
| `when.at` | `"HH:MM"` (24 hours); for `hour`, `":MM"`. |
| `when.days` | For `week`: `mon` to `sun`; every day when missing. |
| `action` | `shutdown`, `broadcast` or `lua`. |
| `warnings` | For `shutdown`: the seconds before the stop at which everybody is told, each smaller than the one before, from 1 to 86400. |
| `message` or `text` | For `broadcast`: the id of a [localized message](localization.md), or a plain text. One of the two. |
| `script`, `function` | For `lua`: the file `scripts/events/<script>.lua` and the function to call, `run` when missing. |

There are no cron expressions. A task whose time passed while the server was off is not run late.
A task that fails is logged with its id and does not stop the others; two tasks at the same moment
both run.

### Shutdown

The `at` of a `shutdown` task is the time **of the stop**; the warnings come before it. With the
task above, everybody reads `The server will shut down in 10 minutes.` at 03:50, then 5 minutes,
`60 seconds` and `10 seconds`, and at 04:00 `The server is shutting down now.`, the world is saved
and the server stops, as with [`shutdown`](commands/shutdown.md). If the server starts inside the
window, only the warnings still ahead are sent, with the time that really remains. A shutdown that
is already pending, asked by the command or by another task, is not asked twice.

The server stops; it does not start again by itself. That is the job of what runs it:

```yaml
# docker compose
restart: unless-stopped
```

```ini
# systemd unit
Restart=always
```

### Lua tasks

`scripts/events/cleanup.lua` defines a global table with the name of the file, and the task calls
its function with the id of the task and the Unix time at which it was due:

```lua
cleanup = {}

function cleanup.run(id, scheduled)
  -- ...
end
```

A missing script or function is logged once and tried again at the next run.

## Time zone

The hours are read in the zone of `[ultima.schedule] time_zone` (see
[Server configuration](server-configuration.md)): an IANA id such as `Europe/Rome`, or empty for
the zone of the system. A Docker container is in UTC unless `TZ` is set or this setting names a
zone; on Linux the zones need the `tzdata` package. An unknown id stops the startup.

Daylight saving follows the zone: a time that does not exist the day the clocks go forward runs at
the first minute that exists, and a time that exists twice runs once, the first time.

## Seasonal events

An `[[event]]` has an `id`, a `name`, and a window `from` and `to` as `MM-dd`, every year. Both days
are inside the window, and a window may cross the new year (`12-20` to `01-06`). The dates are
read in the time zone above.

Each event has a **mode**: `auto` follows the dates, `on` and `off` force it. The staff changes it
with [`.event`](commands/event.md), a script with `schedule.set_event`; the mode is kept with the
world and survives a restart.

When an event starts or ends, at midnight of the zone or because its mode changed, the server calls
a function of `scripts/events/<id>.lua`, with the id and the name:

```lua
halloween = {}

function halloween.on_start(id, name) end
function halloween.on_end(id, name) end
```

A missing script or function is fine. The last state told is kept with the world, so an event that
started or ended while the server was off calls its function once at the next startup; a shard that
never had the event on calls nothing.

## From Lua

The [`schedule` module](https://moongate.sh/lua/schedule/): `schedule.is_active(id)`,
`schedule.active()`, `schedule.events()`, `schedule.set_event(id, mode)` and `schedule.next(id)`
(the Unix time of the next run of a task). A script cannot add entries to the calendar; a custom
action is a task with `action = "lua"`.

## What is not there

Cron expressions, running late the tasks missed while the server was off, a restart that starts
the process again, and the content of the events (decorations, monsters, treats): the events only
tell the scripts when to start and stop.
