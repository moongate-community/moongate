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
| `npc.play_sound(serial, sound)` | Plays a sound where the NPC stands, for the players within 15 cells (0x54): a sound id from 0 to 65535, such as `0x69`, or a kind of the NPC template's `[mobile.sounds]`, `"start_attack"`, `"idle"`, `"attack"`, `"hurt"` or `"death"`, so a script makes each creature sound like itself; `false` for a sound out of range, a kind its template does not set, or a serial that is not an NPC in the world |
| `npc.step(serial, direction, running?)` | One step toward a `DirectionType` (`North` to `NorthWest`), turning first when needed, seen by the players in range; a run when `running` is `true`. How often the script calls it sets the speed. `false` when blocked or for `DirectionType.Running`, which is not a direction |
| `npc.location(serial)`, `npc.name(serial)` | `{ x, y, z, map }` and the name of the NPC, or `nil` |
| `npc.get_prop(serial, key)`, `npc.set_prop(serial, key, value)` | A value the NPC keeps across restarts: a string, a number or a bool, saved with the NPC by the world save; `get_prop` gives `nil` when it has none, `set_prop` with `nil` removes it and gives `false` for a table, a function or a blank key |
| `item.name(serial)`, `item.amount(serial)`, `item.owner(serial)` | The item's name (its template id when it has none), its amount, and the serial of the mobile carrying or wearing it (`nil` on the ground); `nil` for an unknown item |
| `item.consume(serial, amount?)` | Takes `amount` units (default 1) off the item, deleting it at 0, and updates the owner's container or the players around a ground stack; `false` for a worn item, an `amount` below 1, fewer units left, or an item a player holds on the cursor |
| `item.get_prop(serial, key)`, `item.set_prop(serial, key, value)` | The same for an item, saved with it by the world save or its owner's save |
| `item.item_id(serial)`, `item.set_item_id(serial, graphic)` | The item's graphic, and changing it (0 to 65535), as a door opening: the players around a ground item, or the owner of a carried one, see it change; `false` for an unknown, worn or held item or a graphic out of range; an item inside a container on the ground changes without being shown again |
| `item.set_light(serial, type)` | The light shape a light source gives, by `LightType` name such as `circle150`, `circle300` or `west_big`; `nil` clears it. The players who see the item are shown it again; the client draws the light only for a lit graphic. `false` for an unknown shape or a worn or held item |
| `item.location(serial)`, `item.move_to(serial, x, y, z)` | Where a ground item lies, `{ x, y, z, map }`, and moving it on its map: the players around the old spot lose it and those around the new one see it; `nil`/`false` for an item not on the ground, a spot outside the map or a `z` outside -128 to 127; moving restarts a decaying item's decay |
| `item.play_sound(serial, sound)` | Plays a sound id (0 to 65535) where the item lies, or where the mobile carrying it stands, for the players within 15 cells; `false` for an unknown item, a sound out of range, or an item inside a container on the ground |
| `mobile.teleport(serial, x, y, z, map?)` | Teleports a mobile, a player or an NPC, to `x`, `y`, `z` of its own map, or of `map` (a `MapType`) when given: a player's client is told of the map change (0xBF 0x08) and where it stands (0x20), the players around the old spot lose the mobile and those around the new one see it; `false` for a mobile not in the world, a map that does not exist or is not loaded, a spot outside the map or a `z` outside -128 to 127 |
| `mobile.location(serial)`, `mobile.play_sound(serial, sound)` | Where a mobile stands, `{ x, y, z, map }` (`nil` when it is not in the world), and a sound id (0 to 65535) played where it stands for the players within 15 cells |
| `world.is_occupied(map, x, y)` | Whether a player or an NPC stands on the tile, at any height, such as a door's doorway; `map` is a `MapType` |
| `world.moon(moon, x)` | The phase of `MapType.Trammel` or `MapType.Felucca` seen from the column `x`, a `MoonPhaseType` (`NewMoon`, `WaxingCrescent`, `FirstQuarter`, `WaxingGibbous`, `FullMoon`, `WaningGibbous`, `LastQuarter`, `WaningCrescent`): `world.moon(MapType.Trammel, x) == MoonPhaseType.FullMoon`. Felucca turns every 10 game minutes, Trammel every 30 |
| `world.time(map, x)` | The time of day on the map at the column `x`, as `{ hours, minutes }`: `world.time(MapType.Trammel, 1600).hours`; see `ultima.world.seconds_per_uo_minute` |
| `world.is_staff(player)` | Whether the player is a game master or an administrator in the world; `false` for an NPC or a player not in the world |
| `world.carries(mobile, key, value)` | Whether the mobile wears or carries, in its containers at any depth, an item whose prop `key` is `value`, such as the key of a door: `world.carries(user, "key.value", 1234)` |
| `bank.open(player)`, `bank.is_open(player)` | Opens the player's bank box, made the first time, open while the player stands still; and whether it is open. `false` for an NPC or a player not in the world; see [Bank](bank.md) |
| `gump.open(player, id, args)`, `gump.close(player, id)` | Opens the gump `templates/gumps/<id>.xml` on the player, its `${name}` filled from `args`, and closes it; its script `scripts/gumps/<id>.lua` gets the answer. `false` for an unknown player or gump. See [Gumps](gumps.md) |
| `gump.create(id, x, y)`, `gump.send(player, g, args)` | Builds a gump in Lua (`g:text{...}`, `g:button{...}`, `g:paginate(...)`, ...) and opens it; a button's `on_click` may be a function. See [Gumps built in Lua](gumps.md#gumps-built-in-lua) |
| `item.delete(serial)` | Deletes the item; `false` for a worn item, an item a player holds on the cursor, or a container that still holds items |
| `item.message(serial, player, text)` | A label over the item seen only by `player` (cut to 128 characters); `false` for blank text, an unknown item, or a player not in the world |

The default host registers `log`; the engine supplies `engine`, `timer`, `events` and `wait`.
The Ultima plugin registers `dice`, `localization`, `npc`, `item`, `world`, `gump` and `bank` in game and standalone modes. The repository also ships two cats of Moongate v2, `orione` and `vega` (`templates/mobiles/moongate_cats.toml` with `scripts/mobiles/orione.lua` and `vega.lua`): spawn them with `.spawn orione` or `.spawn vega`.
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

Apart from the `npc`, `item` and `world` modules of the [mobile](#mobile-scripts) and [item scripts](#item-scripts),
there are no character or inventory APIs yet ([Implementation status](implementation-status.md)). To expose application
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
| `player_say` | `serial`, `name`, `text`. Raised after a player's character said something and the players and NPCs around heard it; `text` is what they heard. A command (text starting with a dot) raises nothing. |
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
| `on_speech(serial, speaker, text, keywords)` | When a player says `text` within 15 cells (commands are not heard). `speaker` is the player's serial; `keywords` the speech keywords the client found, an array of numbers whatever its language, such as `SpeechKeywordType.Bank`. It may call `wait`. |
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

The distribution's `scripts/mobiles/wander.lua`, copied into the root by `mgboot`:

```lua
wander = {}

local thinks = {}

function wander.on_think(serial)
    thinks[serial] = (thinks[serial] or 0) + 1

    if thinks[serial] % 4 == 0 then
        -- pick_direction (in the file) keeps a spawned NPC in its home area; nil when no step does.
        local direction = pick_direction(serial)

        if direction ~= nil then
            npc.step(serial, direction)
        end
    end
end

function wander.on_speech(serial, speaker, text)
    if text:lower():find("hello", 1, true) then
        wait(1)
        npc.say(serial, "Well met, traveller.")
    end
end

function wander.on_spawn(serial)
    npc.say(serial, "*stretches*")
end

function wander.on_mobile_in_range(serial, other)
    if npc.name(other) == nil then
        npc.say(serial, "Who goes there?")
    end
end
```

No template in the repository uses it: add `script_id = "wander"` to a mobile template
to try it. An NPC spawned by a spawn region carries its home area in the props `spawn.x1`,
`spawn.y1`, `spawn.x2` and `spawn.y2`: `wander.lua` only steps inside it, and walks the NPC back
when it is outside.

A script's `local` tables live in memory: they start again empty after a restart or a
reload. To remember something across restarts, keep it in the NPC's props, prefixing the
key with the script name, as `vega.lua` counts the hellos it hears:

```lua
function vega.on_speech(serial, speaker, text)
    if text:lower():find("hello", 1, true) then
        local times = (npc.get_prop(serial, "vega.greeted") or 0) + 1
        npc.set_prop(serial, "vega.greeted", times)
        npc.say(serial, "Meow! That's " .. times .. " hellos.")
    end
end
```

A change made after the last world save is lost if the server stops without saving.

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
# a potion template of your own
[[item]]
id = "my_potion"
item_id = 0x0F0C
script_id = "potion"
```

| Function | When |
| --- | --- |
| `on_use(serial, user)` | A player double clicks the item, carried (worn or in its containers) or on the ground within 2 tiles and in sight; farther, the player reads "That is too far away." and nothing runs. Items inside a container lying on the ground cannot be used yet: the player reads "That is too far away.". A missing `on_use`, or one that raises an error, lets the default action follow. Return `true` to stop the default action, such as opening a container; return nothing to let it follow. A handler that calls `wait` counts as handled; after the wait the item may have moved, so check it again, for example `item.owner(serial) == user`. |
| `on_move_over(serial, mobile)` | A player stepped onto the cell of the item, lying on the ground at the player's height, up to 14 above its feet, or below them and tall enough to reach them (ModernUO's rule). It runs after the step was acknowledged and shown to the players around; NPCs do not trigger it yet. Once a script moved the player off the cell, the other items of the cell are not run. Arriving by teleport does not trigger it, so two teleporters that point at each other do not loop. |
| `on_equip(serial, wearer)` | The item went onto a layer of the mobile `wearer`, dropped on the paperdoll. A worn item lifted and bounced back never left its layer, and items loaded or spawned already dressed raise nothing. It cannot refuse the item. |
| `on_unequip(serial, wearer)` | The item left the layer of `wearer`: dropped in a container or on the ground, or merged into a stack (the item is gone then, so `item.*` gives `nil`). Logging out, removing an NPC or deleting a mobile with its items raise nothing. |
| `on_pickup(serial, picker)` | The player `picker` lifts the item from a container, the paperdoll or the ground; lifting part of a stack lifts this item, and the rest left behind is not new. While it is held, `item.consume` and `item.delete` refuse it. A held item ends in `on_drop`, in `on_equip` when it is worn by a new wearer, or in nothing: when it bounces back, is worn again on the layer it came from, or its player logs out holding it. |
| `on_drop(serial, dropper)` | The player `dropper` puts the held item down: into a container, on the ground, or onto a stack (the item is gone then, so `item.*` gives `nil`). Not when it bounces back or is worn. A worn item put down runs `on_unequip` first, then `on_drop`. |
| `on_create(serial)` | A newly created item enters the world: today the equipment, backpack and loot of a spawned NPC, before that NPC's `on_spawn`. A new character's starting items and the rest of a split stack raise nothing. |

`on_equip`, `on_unequip`, `on_pickup`, `on_drop` and `on_create` run right after what
caused them, on the next turn of the game loop, once the players have seen it: a script
may then delete or consume the item. They are notifications: none can refuse the move.

The script acts on its item with the `item` module, passing its serial; `user` is
the serial of the player. The distribution's `scripts/items/potion.lua`, copied into the root by `mgboot`; no
template uses it yet:

```lua
potion = {}

function potion.on_use(serial, user)
    item.message(serial, user, "You drink the potion.")
    item.consume(serial)

    return true
end
```

The distribution also ships `scripts/items/door.lua`, the script of the `decoration_door`
template that [`.decorate`](commands/decorate.md) gives to doors and gates. Double clicking
a closed door opens it and its linked door (prop `door.link`): the graphic goes to the next
one, the door swings aside by its `facing` prop and plays the sound of its
`decoration_type` (metal, wood, gate or secret). Double clicking an open door closes both
when nobody stands in either doorway. An open door closes by itself after 20 seconds, then
tries again every 10 seconds while the doorway is taken. A door that cannot swing aside, such
as one at the edge of the map, stays closed. The open state is the prop `door.open`, with the
closed spot in `door.x`, `door.y` and `door.z`, saved with the door; the auto-close timer is not, so a door left open when the
server stops stays open until someone uses it. A closed door with the prop `locked` does not
open for players, who read "That is locked." (message 398, in the server language), unless
they carry anywhere in their backpack a key whose prop `key.value` is the door's `key.value`
(message 405: they open it and it stays locked); game masters and administrators open it
(message 404). The prop comes from the decoration data
(`props = { facing = "west_cw", locked = true }`), such as the side doors of the New Haven
bank. `.lock` gives a door a key number and `.key` makes its key.

`scripts/items/light.lua` lights and douses candles, candelabras, lanterns, lamp posts, wall
sconces and torches: the `decoration_light` template and the light templates of
`templates/items` use it. Double clicking an unlit light gives it the lit graphic (ModernUO's
pairs), a light shape if it has none, and sound `0x47`; double clicking a lit one gives the
unlit graphic and sound `0x3BE`, keeping the shape for the next time. A light without an unlit
graphic, such as a brazier, stays as it is. The lights `.decorate` places have the prop
`protected`: only game masters and administrators light or douse them. The town lamp posts
light and douse themselves: every 30 seconds the server calls `on_darkness(serial, dark)` on a
lamp post whose spot turned dark or light (`ultima.world.lamp_post_light`), and `light.lua`
switches its graphic silently.

`scripts/items/teleporter.lua` is the script of the `decoration_teleporter` template that
[`.decorate`](commands/decorate.md) gives to ModernUO's `Teleporter`: on `on_move_over` it
teleports the player to the props `teleport.x`, `teleport.y` and `teleport.z` with
`mobile.teleport`, then plays the prop `sound_id` there when the teleporter has one. The prop
`active = false` turns a teleporter off. A teleporter with the prop `teleport.map`, a `MapType`
number, takes the player to that map: the client changes map, then gets the season, the light,
the weather and the music of the place; when the map is not loaded nothing happens. The template has `visibility = "game_master"`: a ground item is sent only to
the accounts its visibility allows, so players walk onto a teleporter they never see.

LuaCSharp does not read a hexadecimal number between brackets (`t[0x0A27]` or
`{ [0x0A27] = ... }` fail with "malformed number"): pass it through a function or a variable,
as `light.lua` does with `add(0x0A27, 0x0B1D, "circle225")`.

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
