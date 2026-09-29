# Writing Lua scripts

Put scripts under `<root>/scripts`. The default bootstrap is `init.lua`, selected
by `[scripting].bootstrap_file` in [server configuration](server-configuration.md).
The host uses Lua 5.2 through LuaCSharp; execution and resumes occur on the game
loop. No separate Lua installation is required.

## First script and modules

Create `scripts/common/greeting.lua`:

```lua
local greeting = {}

function greeting.for_name(name)
    return "Welcome, " .. name
end

return greeting
```

Create `scripts/init.lua`:

```lua
local greeting = require("common.greeting")
log.info("{Message}", greeting.for_name("Moongate"))
log.info("{Engine} {Version} ({Codename}) on {Platform}",
    engine.name, engine.version, engine.codename, engine.platform)

local pulse = timer.every(30, function()
    log.info("Pulse")
    wait(2)
    log.info("Pulse resumed")
end)

timer.after(95, function()
    log.info("Pulse cancelled: {Cancelled}", timer.cancel(pulse))
end)
```

Start the server or enter `script reload init.lua` in its console. `require` maps
dotted module names to relative `.lua` paths: `common.greeting` loads
`common/greeting.lua`. It caches the module result. Resolution stays inside the
scripts directory, including checks against symbolic links escaping that root.
A missing bootstrap logs a warning and starts an empty engine. A bootstrap that
exists but fails compilation/execution aborts server startup.

## Available host functions

| API | Purpose |
| --- | --- |
| `engine.name`, `.version`, `.codename`, `.platform` | Read-only engine metadata |
| `log.debug/info/warning/error(template, ...)` | Structured Serilog events; extra arguments fill template properties |
| `log.LEVEL_DEBUG/.LEVEL_INFO/.LEVEL_WARNING/.LEVEL_ERROR` | Numeric constants for the matching Serilog level |
| `print(...)` | Tab-separated values written to the server log at Information level |
| `timer.after(seconds, fn)` | One callback after a positive delay; returns a cancellation handle |
| `timer.every(seconds, fn)` | Repeating callbacks with a positive interval; returns a handle |
| `timer.cancel(handle)` | Cancels a pending registration; returns false if no timer remains |
| `wait(seconds)` | Parks the current scheduled coroutine, then resumes it on the loop |
| `events.on(name, fn)` | Runs `fn` with the event's table each time the named server event happens; returns a handle. See [Events](#events) |
| `events.off(handle)` | Removes a subscription; returns false when the handle is unknown |
| `dice.roll(expression)` | Rolls dice notation such as `"1d4+2"` or `"4d6k3"`, the forms of [DiceSpec](toml-types.md#dicespec); a malformed expression raises an error naming it |
| `dice.try_roll(expression)` | The same roll, or `nil` when the expression is malformed: `dice.try_roll(text) or 0` |
| `localization.get(id, ...)` | Message `id` of `data/messages` in the server language, with `{0}`, `{1}`, ... filled by the extra arguments; see [Localization](localization.md#read-a-message-from-lua) |
| `localization.text(id)`, `localization.language()` | The raw text of a message, or `nil`; the server language code |
| `npc.say(serial, text)` | The NPC says `text` overhead to the players within 15 cells (cut to 128 characters); `false` for blank text or a serial that is not an NPC in the world |
| `npc.step(serial, direction, running?)` | One step toward a `DirectionType` (`North` to `NorthWest`), turning first when needed, seen by the players in range; a run when `running` is `true`. How often the script calls it sets the speed. `false` when blocked or for `DirectionType.Running`, which is not a direction |
| `npc.location(serial)`, `npc.name(serial)` | `{ x, y, z, map }` and the name of the NPC, or `nil` |
| `item.name(serial)`, `item.amount(serial)`, `item.owner(serial)` | The item's name (its template id when it has none), its amount, and the serial of the mobile carrying or wearing it (`nil` on the ground); `nil` for an unknown item |
| `item.consume(serial, amount?)` | Takes `amount` units (default 1) off the item, deleting it at 0, and updates the owner's container or the players around a ground stack; `false` when fewer are left or a player holds it on the cursor |
| `item.delete(serial)` | Deletes the item; `false` for a worn item, an item a player holds on the cursor, or a container that still holds items |
| `item.message(serial, player, text)` | A label over the item seen only by `player` (cut to 128 characters); `false` when that player is not in the world |

The default host registers `log`; the engine supplies `engine`, `timer`, `events` and `wait`.
The Ultima plugin registers `dice`, `localization`, `npc` and `item` in game and standalone modes.
Log levels still follow the host's logging policy, so a `log.debug` call need not
appear in the default console output. Use templates rather than concatenating
changing values into messages.

Call `wait` from a scheduled coroutine, such as a timer callback, not at the top
level of `init.lua` or a required module. It needs a positive, finite delay that
fits the timer range. It yields the coroutine rather than blocking the thread.
Each repeating timer occurrence starts a coroutine: if one calls `wait` for longer
than the repeat interval, multiple suspended invocations can coexist. Cancelling
the timer prevents later starts; it does not cancel an already-started coroutine.
For sequences that must not overlap, use a one-shot callback that schedules its
next run only after its work finishes.

Apart from the `npc` module of the [mobile scripts](#mobile-scripts), there are no world, character or inventory
APIs yet ([Implementation status](implementation-status.md)). To expose application
behavior, bind a C# module using [Writing a Lua module](lua-modules.md).

## Events

Scripts react to server events with the built-in `events` module:

```lua
local handle = events.on("character_created", function(e)
    log.info("New character {Name}", e.name)
end)

events.off(handle) -- returns false when the handle is unknown
```

- Each handler runs on the game loop as a coroutine, so it may call `wait()`.
- Handlers of one event run in the order they subscribed. Subscribing or
  unsubscribing inside a handler takes effect from the next event.
- Every handler receives its own table; changing it does not affect other handlers.
- Events are notifications: a handler cannot cancel or change what happened. An
  error in a handler is reported like any script error, and the other handlers
  still run.
- Subscriptions belong to the file that made them. Reloading or invalidating the
  file removes them, like its timers; stopping the engine removes all of them.
- An unknown event name raises an error in `events.on`. The generated
  `definitions.lua` lists the valid names as the `EventName` alias, so editors
  complete them.

### Available events

| Event | Fields |
| --- | --- |
| `character_created` | `serial`, `account_id`, `name`, `race` and `gender` (numbers of `RaceType` and `GenderType`), `map`, `x`, `y`, `z`. Raised after a new character and its starting items are saved. |
| `character_deletion_requested` | `serial`, `account_id`, `name`. Raised after a player asks to delete a character; it stays restorable until removed. |
| `character_entered_world` | `serial`, `account_id`, `name`, `map`, `x`, `y`, `z`. Raised after a character entered the world and the client's login completed. |
| `character_left_world` | `serial`, `account_id`, `name`, `map`, `x`, `y`, `z`. Raised after a character left the world because its session closed, once its save was attempted. |

### Publishing an event from C#

Hosts and plugins publish a bus event to Lua with an explicit registration,
before the engine starts:

```csharp
container.AddScriptEvent<MyEvent>(
    "my_event",
    e => new Dictionary<string, object?> { ["name"] = e.Name, ["amount"] = e.Amount });
```

The name must be snake_case and unique, and each event type is published once.
The mapping runs on the publishing thread and must only read the event. It may
return strings, booleans, numbers, enums (sent as numbers) or null. A mapping
that fails is logged, and the event is skipped for Lua only. Events published
while no script is subscribed cost one lookup and are not queued.

## Mobile scripts

A mobile template names its script with `script_id`, the name of a global Lua table
defined by `scripts/mobiles/<script_id>.lua`. The server loads every `*.lua` directly
in that directory at startup, in name order, after `init.lua`; a script that fails
to load is reported like any script error, and the server starts with the others.

```toml
# templates/mobiles/animals.toml
[[mobile]]
id = "cat"
name = "a cat"
body = 201
script_id = "wander"
```

The table may define these functions; each one is optional:

| Function | When |
| --- | --- |
| `on_think(serial)` | On every think of the NPC: every `ultima.npcs.think_interval_ms` (500 ms by default) while a player is within the 5×5 sectors around it; see [NPC tick](game-loop-and-timers.md#npc-tick). A think is instantaneous, as ModernUO's: it must not call `wait` (the server warns once per script), so keep the timing in the script, for example by counting thinks. |
| `on_speech(serial, speaker, text)` | When a player says `text` within 15 cells (commands are not heard). `speaker` is the player's serial. It may call `wait`. |
| `on_spawn(serial)` | Once, right after the NPC is spawned (`.spawn`), in the world with its items and shown, before any other function of its script. Not when the saved NPCs are loaded at startup. It may call `wait`. |
| `on_mobile_in_range(serial, other)` | Each time another mobile, player or NPC, comes within `ultima.npcs.sense_range` cells (8 by default, a square along X and Y) by a step or by entering the world. Once per arrival: it fires again only after the mobile has left the range and come back. Both ways: an NPC walking toward a mobile senses it too. NPCs loaded together at startup do not sense each other until one moves out of range and back. `other` is its serial; `npc.name(other)` gives `nil` for a player. It may call `wait`. |

`on_spawn` and `on_mobile_in_range` run right after what caused them, on the next
turn of the game loop: a step made by `npc.step` inside a running handler cannot
start another script at once. No function runs before the scripts are loaded at
startup, which is after the saved NPCs enter the world.

Scripts act on their NPC with the `npc` module, passing its serial. A serial that
is not an NPC in the world, such as a removed NPC or a player, gives `false` or
`nil`, never an error: a handler that waited may outlive its NPC, and a script can
never voice or move a player.

The shipped `scripts/mobiles/wander.lua`:

```lua
wander = {}

local thinks = {}

function wander.on_think(serial)
    thinks[serial] = (thinks[serial] or 0) + 1

    if thinks[serial] % 4 == 0 then
        npc.step(serial, dice.roll("1d8") - 1)
    end
end

function wander.on_speech(serial, speaker, text)
    if text:lower():find("hello", 1, true) then
        wait(1)
        npc.say(serial, "Well met, traveller.")
    end
end
```

Reload one script with `script reload mobiles/wander.lua`. Its table is replaced,
so the NPCs use the new functions from their next think; state kept in `local`
tables of the old file starts again, and the waits its handlers left are cancelled,
because a script's calls belong to `mobiles/<script_id>.lua`. When the server stops,
the scripts are no longer called, before the script engine stops.

## Item scripts

An item template names its script with `script_id`, the name of a global Lua table
defined by `scripts/items/<script_id>.lua`. The files of `scripts/items/` load at
startup like the [mobile scripts](#mobile-scripts), and `script reload
items/potion.lua` reloads one.

```toml
# templates/items/potions.toml
[[item]]
id = "0x0f0e_potion"
item_id = 0x0F0E
script_id = "potion"
```

| Function | When |
| --- | --- |
| `on_use(serial, user)` | A player double clicks the item, carried (worn or in its containers) or on the ground within 2 tiles and in sight; farther, the player reads "That is too far away." and nothing runs. Items inside a container lying on the ground cannot be used yet. Return `true` to stop the default action, such as opening a container; return nothing to let it follow. A handler that calls `wait` counts as handled; after the wait the item may have moved, so check it again, for example `item.owner(serial) == user`. |
| `on_equip(serial, wearer)` | The item went onto a layer of the mobile `wearer`, dropped on the paperdoll. A worn item lifted and bounced back never left its layer, and items loaded or spawned already dressed raise nothing. It cannot refuse the item. |
| `on_unequip(serial, wearer)` | The item left the layer of `wearer`: dropped in a container or on the ground, or merged into a stack (the item is gone then, so `item.*` gives `nil`). Logging out, removing an NPC or deleting a mobile with its items raise nothing. |

`on_equip` and `on_unequip` run right after the move, on the next turn of the game
loop, once the players have seen it: a script may then delete or consume the item it
was taken off.

The script acts on its item with the `item` module, passing its serial; `user` is
the serial of the player. The shipped `scripts/items/potion.lua`:

```lua
potion = {}

function potion.on_use(serial, user)
    item.message(serial, user, "You drink the potion.")
    item.consume(serial)

    return true
end
```

## Reload and ownership

The console accepts:

```text
script reload init.lua
script metrics
```

Reload invalidates the named file, evicts its matching `require` cache entry,
cancels timers/coroutines owned by that file and executes it again on the loop.
It does not rebuild the whole Lua state, recursively invalidate dependencies or
roll back globals if the new file fails. Existing references to a module table
remain references to that old table. After changing a required helper, invalidate
that helper and reload the consuming script to obtain the new module value.

Ownership follows the engine's active load/coroutine context. In particular,
`require` executes a module within its caller's context; timers created there
must not be assumed to belong to the module filename. Prefer modules that return
functions/data and let `init.lua` or an explicitly loaded owner create timers.
This makes cancellation on reload predictable. Engine shutdown cancels all owned
scheduled work before disposing its Lua state.

`script metrics` shows file loads, calls, coroutine resumes/completions/errors,
active coroutines, budget aborts, string-cap hits and server events dropped
because the game loop refused them (each drop is also logged as a warning). Script errors include source
information where available, are logged, and publish `ScriptErrorEvent`. Ordinary
runtime errors are reported by the scheduler; host C# callers of `LoadFile` must
handle its exceptions. Missing files and cancellation have their own failure paths.

## Budgets and sandbox

Defaults allow 150,000 instructions per coroutine resume and 10,000,000 per
loaded top-level chunk, checked every 1,000 instructions. They bound VM instruction
execution, not wall-clock duration or native C# work. A module that blocks on I/O
can still block the loop; keep host-bound functions short and synchronous.
`string.rep` caps its result at 16,777,216 UTF-16 characters by default. Other
allocations, including tables and concatenation, do not have a global memory cap.
Treat scripts and C# plugins as trusted shard content, not as an isolation boundary
for arbitrary hostile code.

Available libraries are base, `string`, `table`, `math`, restricted `coroutine` and
restricted `package`. There is no `io`, `os`, `debug`, `dofile`, `loadfile` or
`rawset`. Filesystem/native package search paths and script-created/resumed
coroutines are disabled. Use `require` for local modules and `wait` for scheduled
yielding. See the [library sandbox reference](../src/Moongate.Scripting/README.md#sandbox)
for the exact removed functions.

## Editor support

With `write_definitions = true`, startup writes `scripts/definitions.lua` and
`scripts/.luarc.json` from registered modules, functions, constants and enums.
Open the scripts folder in an editor using the Lua language server for completion.
These files are generated: put handwritten content in separate files and register
C# modules before startup so they appear in the definitions. They provide editor
metadata, not runtime loading; do not `require("definitions")`.
