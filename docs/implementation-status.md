# Implementation status

Moongate is under active development: **the world is not a game yet**. This page says what the
server does today and what it does not. It describes the current source tree; the
[changelog](../CHANGELOG.md) records what each release added, and the
[feature checklist](feature-checklist.md) goes system by system through what UO emulators usually offer.

## At a glance

| Area | State | In short |
| --- | --- | --- |
| Login, realms and accounts | ✅ Works | Login server, game realms, handoff between them |
| Characters | ✅ Works | Create, delete and restore, enter the world, walk and run |
| Other players | ✅ Works | See each other, talk |
| Items | 🟡 Partial | Backpack, paperdoll, ground, tooltips; no ground containers |
| NPCs | 🟡 Partial | Spawn regions, Lua scripts, wandering; no combat or pathfinding |
| World | 🟡 Partial | Decoration, doors and keys, day and night, weather, seasons; no teleporters or houses |
| Combat, death, skill gain | ❌ Not yet | |
| Lua scripting | ✅ Works | NPC and item scripts, sandboxed |
| Persistence | ✅ Works | PostgreSQL, world saves, migrations; no backup tool |
| Administration | 🟡 Partial | Console and in-game commands, gRPC API; no web panel |

## What a player can do

- Log in, pick a realm, create a character (with its starting items) and enter the world.
- Walk and run, with the server checking the terrain, the statics and the speed.
- See the other players within 18 tiles, and talk to them.
- Open the backpack, move items in it, split and merge stacks, drop items on the ground and pick
  them up; items left on the ground decay.
- Open the paperdoll, dress and undress (two-handed weapons included).
- Read tooltips and names of what is in view.
- Open doors, and locked doors when carrying their key; light and douse lights.
- See day and night pass, dark dungeons, and the weather of each region (rain, snow, storms).
- Meet NPCs that wander around their home, greet and answer.

## What a game master can do

- Place the world decoration once: `.decorate`.
- Spawn and remove single NPCs: `.spawn`, `.remove`.
- See the spawn regions where they stand: `.spawns`; get a message when regions spawn.
- Lock and unlock doors and make their keys: `.lock`, `.unlock`, `.key`.
- Force the light, the weather or the season, try a music track: `.globallight`, `.weather`,
  `.season`, `.music`.
- Try any gump on themselves: `.gump`.
- Set fame and karma, inspect what a target cursor picks: `.fame`, `.karma`, `.where`.
- Save, broadcast, shut down, manage accounts and characters.

See all of them in [Commands](commands.md).

## Not built yet

- Combat, death, corpses and skill gain.
- Pathfinding AI: NPCs only run their Lua script (`on_think`, `on_speech`, `on_spawn`,
  `on_mobile_in_range`).
- Teleporters, public moongates, map changes and mounts.
- Houses and boats (placement, and multis in movement and line of sight).
- Containers on the ground, dressing other characters, strength requirements.
- Region rules: guards and housing. Regions drive the weather, the dungeon light, the music and the season.
- Spawner items (the [spawn regions](spawns.md) do the respawning).
- Per-player language, database backup and restore, a web administration panel.
- Old Kingdom Reborn AES/E3 encryption; the Enhanced Client has not been tested.

## By area

### Network and login

- Framed TCP with per-connection pipelines, session registries and graceful shutdown.
- POL-compatible client encryption with `Disabled`, `Optional` and `Required` policies
  ([configuration](server-configuration.md#uo-client-encryption)).
- `mode` runs separate login and game servers or one standalone process. Game realms advertise
  themselves through Redis leases; the login filters them by account level and hands the player
  over with a one-use ticket.
- The packets the server handles and sends are listed in the [packet reference](packets.md).

### World

- **Movement and sight:** walkability and landing height from the terrain and the statics (as
  ModernUO), line of sight (as POL and ModernUO). Items, mobiles and multis are not part of these
  checks yet. See [World queries](world-queries.md).
- **Map sectors:** players, NPCs and ground items are seen within the view range; NPCs away from
  every player sleep.
- **Light:** a game clock with day and night by map and longitude; dark dungeons and dim jails.
- **Regions, weather, music and seasons:** the region of every player is followed; each region has
  UOX3's weather, rolled every game hour (dry indoors), its music track and, if set, its season; the
  maps' seasons can rotate with the game days.
- **Decoration:** ModernUO's world decoration (and ServUO's New Haven) placed by `.decorate`: doors,
  locks and keys, signs, lights; the town lamp posts light up at night.
- **NPC spawns:** UOX3's spawn regions fill the world gradually and respawn NPCs, on land and on
  water. See [NPC spawns](spawns.md).

### Scripting

- Sandboxed Lua 5.2 with an instruction budget, `wait`, timers, events, hot reload and editor
  definitions. See [Writing Lua scripts](scripting.md).
- Modules: `engine`, `log`, `timer`, `events`, and in the Ultima plugin `dice`, `localization`,
  `npc`, `item`, `world` and `gump`.
- Gumps: XML layouts checked by `gump.xsd`, a Lua script per gump for the answers, slots and whole
  gumps built in Lua, and gumps chained with `bind` and `open`; see [Gumps](gumps.md) and
  [Your first gump](gump-tutorial.md).
- Mobile and item scripts are bound from their templates by `script_id`. Shipped scripts:
  `door.lua`, `light.lua`, `wander.lua`, `potion.lua`, and the cats Orione and Vega.
- Not yet: character and inventory APIs.

### Data and templates

- Shard data in `data/` (maps, starting cities, skills, professions, races, names, containers,
  bodies, regions, weather, messages), validated at startup. See [Data files](data-files.md).
- Templates in `templates/`: items, loot, mobiles, NPC lists, spawn regions and decoration, with
  `base_id` inheritance. See [Loading TOML templates](templates.md).
- Client files read from `ultima.ultima_path`: tile data, maps (MUL or UOP) and multis.
- Messages in 8 languages, ported from UOX3; a language can be split into several toml
  files. See [Localization](localization.md).

### Persistence

- Two PostgreSQL databases (accounts and world) with transactions, versioned SQL migrations and a
  separate migration runner.
- Characters, their items, ground items and NPCs are kept in memory and written by the periodic
  world save; characters are also saved when they leave.
- Not yet: backup and restore.

### Administration and tools

- Console and in-game [commands](commands.md), translated in every shipped language.
- Optional gRPC [administration API](admin-api.md) with TLS: accounts and server info. No web panel
  or character operations yet.
- Plugins under `plugins/` register services, commands, Lua modules, metrics, entities, SQL and
  their own config section. See [Writing a plugin](plugins.md).
- Tools: `mgboot` prepares the server root, `mg-uoxconv` converts UOX3 content, the migration
  runner applies database migrations. A [Docker example](docker-login-realms.md) runs one login and
  two game servers.

## Settings with no effect yet

`network.enable_ping_server` and the `--log-level` and `--log-packets` command-line options are
parsed and validated but nothing uses them yet; see the
[configuration reference](server-configuration.md#settings-and-validation).
