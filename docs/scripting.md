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
The Ultima plugin registers `dice`, `localization`, `npc`, `item`, `mobile`, `world`, `target`, `prompt`, `gump`, `bank`, `effect`, `moongates` and `locations` in game and standalone modes.
Log levels still follow the host's logging policy, so a `log.debug` call need not
appear in the default console output. Use templates rather than concatenating
changing values into messages.

The `npc`, `item`, `mobile`, `effect`, `world`, `target`, `prompt`, `bank` and `gump` modules serve the [mobile](scripting/mobile-scripts.md) and
[item scripts](scripting/item-scripts.md). A script reads and writes a mobile's numbers and skills; nothing
uses them yet, so a skill a script sets gains nothing by itself (see the
[Roadmap](roadmap.md#phase-0-what-lua-needs-before-any-gameplay)). To expose application
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
| `dice` | The forms of [DiceSpec](toml-types.md#dicespec) |
| `localization` | [Read a message from Lua](localization.md#read-a-message-from-lua) |
| `locations` | [Locations](data-files/locations.md) and the [`go` command](commands/go.md) |
| `moongates` | [Moongates](data-files/moongates.md) |

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
