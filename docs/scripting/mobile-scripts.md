# Mobile scripts

This page is part of [Writing Lua scripts](../scripting.md). The functions a script calls on its NPC are in
the reference: [`npc`](https://moongate.sh/lua/npc/) and [`mobile`](https://moongate.sh/lua/mobile/).

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
| `on_think(serial)` | On every think of the NPC: every `ultima.npcs.think_interval_ms` (500 ms by default) while a player is within the 5×5 sectors around it; see [NPC tick](../game-loop-and-timers.md#npc-tick). A think is instantaneous, as ModernUO's: it must not call `wait` (the server warns once per script), so keep the timing in the script, for example by counting thinks. |
| `on_speech(serial, speaker, text, keywords, type)` | When a player says `text` within hearing (commands are not heard): 15 cells aloud or as an emote, 1 cell for a whisper, 18 for a yell. `speaker` is the player's serial; `keywords` the speech keywords the client found, an array of numbers whatever its language, such as `SpeechKeywordType.Bank`; `type` how it was said, a number to compare with `SpeechType.Regular`, `SpeechType.Emote`, `SpeechType.Whisper` or `SpeechType.Yell`. It may call `wait`. |
| `on_spawn(serial)` | Once, right after the NPC is spawned (`.spawn`), in the world with its items and shown, before any other function of its script. Not when the saved NPCs are loaded at startup. It may call `wait`. |
| `on_mobile_in_range(serial, other)` | Each time another mobile, player or NPC, comes within `ultima.npcs.sense_range` cells (8 by default, a square along X and Y) by a step or by entering the world. Once per arrival: it fires again only after the mobile has left the range and come back. Both ways: an NPC walking toward a mobile senses it too. NPCs loaded together at startup do not sense each other until one moves out of range and back. `other` is its serial; `npc.name(other)` gives `nil` for a player. It may call `wait`. |
| `on_mobile_killed(serial, killed, killer)` | When a mobile, a player or an NPC, is killed within `ultima.npcs.sense_range` cells of this NPC (8 by default, a square along X and Y), the killed one excepted. `serial` is the NPC that is told, `killed` the serial of who died and `killer` the serial of who killed it, or `nil` when nobody did. `npc.name(killed)` gives `nil` for a player. A dying NPC is not told. |
| `on_death(serial, corpse, killer)` | When the NPC dies ([Death and resurrection](../death.md)), after its corpse exists and is shown and before the NPC leaves the world, so it can still be read. `corpse` is the serial of the corpse, nil when none was made; `killer` the serial of who killed it, nil when nobody did. Killed by a script, it runs on the next turn of the game loop, just before the NPC is removed; it must not `wait()`. |
| `on_drag_drop(serial, giver, item)` | When a player drops an item on the NPC from 2 tiles or closer, on the same map; the staff from any distance, as in ModernUO. From farther away a player reads `That is too far away.` and the item comes back. `giver` is the player's serial, `item` the item's. Return `true` when the script took the item, having moved or deleted it (`item.delete`, `item.move_into`, `item.move_to`): the item is on no cursor while the function runs. Anything else, a missing function, an error or a `wait()` gives the item back to where the player lifted it from, as every drop on an NPC without this function. The item's own `can_drop` is asked first, and its `on_drop` is not called for an item an NPC took. |
| `on_context_menu(serial, player)` | When that player asks for the NPC's [context menu](../context-menus.md). Return the entries the NPC adds, a table of `{ id, cliloc, range, enabled }`, or nothing; it answers at once and must not `wait`. |
| `on_context_menu_select(serial, player, id)` | When the player chose one of those entries: `id` is the entry's own. Called only for an entry the player was shown and is in range of. It may call `wait`. |

`on_spawn`, `on_mobile_in_range` and `on_mobile_killed` run right after what caused them, on the next
turn of the game loop: a step made by `npc.step` inside a running handler cannot
start another script at once. No function runs before the scripts are loaded at
startup, which is after the saved NPCs enter the world.

Scripts act on their NPC with the `npc` module, passing its serial. A serial that
is not an NPC in the world, such as a removed NPC or a player, gives `false` or
`nil`, never an error: a handler that waited may outlive its NPC, and a script can
never voice or move a player.

An NPC that takes what it is given, and keeps only gold:

```lua
collector = {}

function collector.on_drag_drop(serial, giver, given)
    if item.template(given) ~= "0x0eed_gold_coin" then
        npc.say(serial, "I take gold only.")

        return false
    end

    npc.say(serial, "Thank you.")
    item.delete(given)

    return true
end
```

The distribution's `scripts/mobiles/wander.lua`, copied into the root by `mgctl`:

```lua
wander = {}

local thinks = {}

function wander.on_think(serial)
    thinks[serial] = (thinks[serial] or 0) + 1

    if thinks[serial] % 4 == 0 then
        npc.wander(serial)
    end
end

function wander.on_speech(serial, speaker, text)
    if text:lower():find("hello", 1, true) then
        npc.look_at(serial, speaker)
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
`spawn.y1`, `spawn.x2` and `spawn.y2`, which `npc.home` gives as a table: `npc.wander` only steps
inside it, and walks the NPC back when it is outside. It strolls as ModernUO's creatures do, mostly
straight ahead, where the script before picked a new direction at every step.

A script's `local` tables live in memory: they start again empty after a restart or a
reload. To remember something across restarts, keep it in the NPC's props, prefixing the
key with the script name, as `vega.lua` counts the hellos it hears:

```lua
function vega.on_speech(serial, speaker, text)
    if text:lower():find("hello", 1, true) then
        npc.look_at(serial, speaker)

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

The script of the monsters, `monster.lua`, is described with the other
[shipped scripts](shipped-scripts.md#monsterlua).

## Walking a path

`npc.walk_to` lets an NPC reach a place around walls, water and cliffs. The script calls it
on every tick and the NPC takes one step each time:

```lua
guard = {}

function guard.on_think(serial)
    local state = npc.walk_to(serial, 1434, 1699)

    if state == "arrived" then
        npc.say(serial, "All quiet at the bank.")
    end
end
```

| Answer | Means |
| --- | --- |
| `"moving"` | The NPC took a step |
| `"arrived"` | It stands within `range` tiles of the place (default 0), at its height; it is not checked that nothing stands between them |
| `"blocked"` | The step was refused, or the NPC waits to look for another way |
| `"no_path"` | The last search did not reach the place: nothing leads there, or only somewhere near |
| `nil` | The serial is not an NPC, `range` is negative or `z` is outside -128 to 127 |

The path is found with the server's [path search](../world-queries.md#pathfinding) and kept for
the NPC, so most calls only take the next step. A search runs when the NPC has no steps left
or the place changed, and only:

- two seconds after the NPC's last search, as ModernUO; ten seconds when that search did not
  reach the same place from where the NPC stands, since such a search is the costly kind;
- for ten NPCs a second in the whole server; the others wait their turn.

While it may not search, an NPC goes on along the path it has, or with none steps straight
towards the place, so one that chases something keeps moving. A place that cannot be reached
is walked towards as far as a path leads. To follow someone, pass where it stands on every
tick and a `range` of 1 to stop beside it:

```lua
local where = mobile.location(target)
npc.walk_to(serial, where.x, where.y, where.z, 1, true)
```

`running` only changes how the step looks: an NPC takes one step per `on_think`, two a second.
Without `z` the place is the highest ground of the cell not above the NPC's head, else the
highest there. Start and goal must be within `ultima.world.pathfinding_range` tiles (38);
farther is `"no_path"`. A closed door blocks the way, and an NPC does not open it: it goes
around, or walks up to it and then answers `"no_path"`. Other mobiles do not block a path.
