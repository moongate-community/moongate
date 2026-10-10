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

## What scripts can call

The [Lua API reference](https://moongate.sh/lua/) has a page for each module, with every
function's signature, parameters and return type; it is generated from the server's code.

The default host registers `log`; the engine supplies `engine`, `timer`, `events` and `wait`.
The Ultima plugin registers `dice`, `localization`, `npc`, `item`, `mobile`, `world`, `target`, `prompt`, `gump`, `bank`, `vendor`, `trainer`, `effect`, `moongates`, `locations`, `jail`, `board`, `commands`, `mount`, `stable`, `pet`, `hue_picker`, `skill`, `combat`, `npcguild`, `schedule`, `help`, `book`, `harvest` and `craft` in game and standalone modes.
Log levels still follow the host's logging policy, so a `log.debug` call need not
appear in the default console output. Use templates rather than concatenating
changing values into messages.

The `npc`, `item`, `mobile`, `effect`, `world`, `target`, `prompt`, `bank` and `gump` modules serve the [mobile](scripting/mobile-scripts.md) and
[item scripts](scripting/item-scripts.md). A script reads and writes a mobile's numbers and skills, and
tries a mobile at a skill with `skill.check`, which may raise it (see [Skills](skills.md)). To expose application
behavior, bind a C# module using [Writing a Lua module](lua-modules.md).

Some modules have a page that says more:

| Module | Read |
| --- | --- |
| `events`, `timer` | [Events and timers](scripting/events.md) |
| `npc` | [Mobile scripts](scripting/mobile-scripts.md), with [walking a path](scripting/mobile-scripts.md#walking-a-path) |
| `item` | [Item scripts](scripting/item-scripts.md) |
| `effect` | [Effects](scripting/effects.md) |
| `gump` | [Gumps](gumps.md), with [gumps built in Lua](gumps.md#gumps-built-in-lua), and [Your first gump](gump-tutorial.md) |
| `bank` | [Bank](bank.md) |
| `vendor` | [Vendors](vendors.md) |
| `trainer` | [Trainers](skills.md#trainers) |
| `dice` | The forms of [DiceSpec](toml-types.md#dicespec) |
| `localization` | [Read a message from Lua](localization.md#read-a-message-from-lua) |
| `locations` | [Locations](data-files/locations.md) and the [`go` command](commands/go.md) |
| `moongates` | [Moongates](data-files/moongates.md) |
| `jail` | [Jail](jail.md) and the [`jail` command](commands/jail.md) |
| `board` | [Bulletin boards](bulletin-boards.md) |
| `commands` | [Commands](commands.md): `commands.execute` runs one as the console, `commands.execute_as` as a player |
| `skill` | [Skills](skills.md): `skill.check(mobile, skill, min, max)` tries a mobile at a skill, which may rise |
| `combat` | `attack`, `stop`, `target`, `range`, `armor_rating`, `weapon`, `swing`, `spend_ammo`: starts and ends melee fights and tells whom a mobile fights; see [Combat](combat.md) |
| `mount` | `ride_ethereal(player, statuette)`: puts a player on the mount of a statuette; see [Mounts](mounts.md) |
| `stable` | `attend`, `pets`, `stable`, `claim`, `max_pets`, `fee`: leaves a player's pets in the stable and takes them back |
| `pet` | `info`, `lore`, `corpse`, `followers`, `max_followers`, `tame`, `attend`, `release`, `loyalty`, `control_chance`, `obey`, `feed`: the creatures a player has tamed, and taming a wild one; see [Animal taming](animal-taming.md) |
| `hue_picker` | `open(player, graphic, callback)`: shows the client's hue picker and runs the function with the hue picked |
| `npcguild` | `of`, `member`, `quote`, `is_join_payment`, `join`, `resign`: the guild of a trade, as a guildmaster takes members |
| `schedule` | `is_active`, `active`, `events`, `set_event`, `next`: the calendar of `data/schedule.toml` |
| `help` | The help gump's needs: the wait of the I am stuck button, the nearest starting city, and the queue of requests for the game masters (`create_page`, `pages`, `take`, `answer`, `close`...) |
| `book` | `give`, `write`, `open`: creates and reads personalized scrolls and books from `templates/books` |
| `harvest` | `has`, `amount`, `vein`, `take`: what is gathered from the world and runs out by area, such as fish |
| `craft` | `get`, `resource`: the crafts of `data/crafts`, their groups and recipes, and the resource lists they use |

## Where to read next

- [Lua in Moongate](scripting/lua-in-moongate.md): the Lua version, the libraries a script has, and where it
  differs from the manual.
- [Events and timers](scripting/events.md): server events, `wait` and repeating timers.
- [Mobile scripts](scripting/mobile-scripts.md): the functions an NPC's script may define, and walking a path.
- [Item scripts](scripting/item-scripts.md): the functions an item's script may define, and how one refuses a move.
- [Shipped scripts](scripting/shipped-scripts.md): what each script of the distribution does: monsters, doors,
  lights, food, teleporters, moongates, clocks and the containers that fill up.
- [Effects](scripting/effects.md): the options of the `effect` module.
- [Reload, budgets and editor](scripting/runtime.md): reloading a script, the instruction budgets, and
  completion in an editor.
- [Lua API reference](https://moongate.sh/lua/): every function, with its signature and, for many, an
  example.
