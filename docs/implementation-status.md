# Implementation status

Moongate is under active development: **the world is not a game yet**. This page says what the
server does today and what it does not. It describes the current source tree; the
[changelog](../CHANGELOG.md) records what each release added, and the
[feature checklist](feature-checklist.md) goes system by system through what UO emulators usually offer.
The [roadmap](roadmap.md) gives the order in which the missing systems are built.

## At a glance

| Area | State | In short |
| --- | --- | --- |
| Login, realms and accounts | ✅ Works | Login server, game realms, handoff between them |
| Characters | ✅ Works | Create, delete and restore, enter the world, walk and run |
| Other players | ✅ Works | See each other, talk |
| Items | 🟡 Partial | Backpack, paperdoll, ground, tooltips; containers on the ground open, and items go in and out of them |
| NPCs | 🟡 Partial | Spawn regions, Lua scripts, wandering, walking a path, monsters that chase, flee and fight, guards, vendors, trainers, healers |
| World | 🟡 Partial | Decoration, doors and keys, teleporters and public moongates (also across maps), day and night, weather, seasons, dungeon treasure chests that respawn and town containers that fill up; no houses |
| Combat, death, skills | 🟡 Partial | Melee and archery, death and corpses, skill use and gain, mounts, taming; no spells or parry |
| Lua scripting | ✅ Works | NPC and item scripts, sandboxed |
| Persistence | ✅ Works | PostgreSQL, world saves, migrations, rotating SQL backups |
| Administration | 🟡 Partial | Console and in-game commands, gRPC API; no web panel |

## What a player can do

- Log in, pick a realm, create a character (with its starting items) and enter the world.
- Walk and run, with the server checking the terrain, the statics, the items in the way (a closed
  door, a crate, a wall) and the speed.
- See the other players within 18 tiles, and talk to them.
- Open the backpack, move items in it, split and merge stacks, drop items on the ground and pick
  them up; items left on the ground decay.
- Open the paperdoll, dress and undress (two-handed weapons included).
- Read tooltips and names of what is in view.
- Read personalized scrolls and native books from [text templates](data-files/books.md): their text stays fixed when traded.
  Write titles, authors and pages in writable books carried in the backpack or an open bank box.
  [Starting items](data-files/starting-items.md#personalized-starting-letters) can deliver them transactionally; the shipped common set includes a welcome letter and a blank writable book.
- Open doors, and locked doors when carrying their key; light and douse lights.
- See day and night pass, dark dungeons, and the weather, the season and the music of each region
  (rain, snow, storms).
- Step on a teleporter, or say the word of one that answers a word, and arrive elsewhere, also on
  another map; step into a moongate.
- Read the game time and the moon phases where they stand: `.time`.
- Meet NPCs that wander around their home, greet and answer, and, when their script says so, walk to a
  place or follow someone around what stands in the way.
- Open the bank box at a banker by saying *bank*, in any client language; ask the *balance*,
  *withdraw* and *deposit* gold by speech, have a bank *check* written and cash it with a double
  click, or drop gold and checks on the banker to deposit them. The box holds a limited number of items: [Bank](bank.md).
- Buy from the vendors and sell to them: pick *Buy* or *Sell* in their context menu or say *vendor buy* or *vendor sell*,
  choose in the window and pay with the gold of the backpack, or of the bank from 2000: [Vendors](vendors.md).
- Learn skills from the vendors and healers: pick *Train* in their menu or say *train*, then drop the gold they quote on
  them: [Trainers](skills.md#trainers).
- Fight with melee weapons and bows, die and leave a corpse, become a ghost and be brought back: [Combat](combat.md), [Death and resurrection](death.md).
- Use skills, which rise with use, and learn them from trainers: [Skills](skills.md).
- Tame animals, have them follow and obey, leave them in a stable, and ride mounts, ethereal statuettes included: [Animal taming](animal-taming.md), [Mounts](mounts.md).
- Fish, mine and cut wood, and craft carpentry with a tool: [Fishing](fishing.md), [Mining](mining.md), [Lumberjacking](lumberjacking.md), [Carpentry](carpentry.md).
- Ask for help from the help menu, and see the Halloween and Christmas events: [Help](help.md), [Holidays](holidays.md).
- Get hit points, mana and stamina back with time, get hungry and thirsty, eat and drink; tire by running or by carrying too much.
- Open the treasure chests of the dungeons and the shop crates that fill up; read a clock; switch war mode.
- Dye clothes: dyes give a dye tub the hue picked in the client's hue picker, and the tub gives it to the clothing.

## What a game master can do

- Spawn and remove single NPCs: `.spawn`, `.remove`; kill one, which leaves its corpse with what it
  carried: [`.kill`](commands/kill.md), see [Death and resurrection](death.md).
- See the spawn regions where they stand: `.spawns`; get a message when regions spawn.
- Go to any spot of any map, or to one of the 558 named places, by name or from a gump that
  lists them by map and category: [`.go`](commands/go.md); walk through doors.
- Lock and unlock doors and make their keys: `.lock`, `.unlock`, `.key`.
- Force the light, the weather or the season, try a music track: `.globallight`, `.weather`,
  `.season`, `.music`.
- Try any gump on themselves: `.gump`.
- Send a player or an NPC to a jail cell for some days, or release it, from a gump that lists the
  cells: [`.jail`](commands/jail.md). The sentence ends by itself, with a fine and a release note.
  `.jail <name>` jails a player who is offline: the cell is kept and the days start at its login;
  see [Jail](jail.md).
- Read, post, reply and remove on the bulletin boards of the towns, each with its own messages;
  threads expire and a full board lets its oldest thread go: [Bulletin boards](bulletin-boards.md).
- Set fame and karma, inspect what a target cursor picks: `.fame`, `.karma`, `.where`.
- Create personalized scrolls and books in your backpack: [`.book`](commands/book.md).
- Make gold and bank checks out of nothing: `.add_gold`, `.create_check`.
- Restore a character waiting to be deleted: `.character`.

An administrator also places the decoration, fills the spawn regions again (`.initial_spawn`),
saves, takes a SQL backup (`.sql_backup`), broadcasts, shuts down and manages accounts.

See all of them in [Commands](commands.md).

## Not built yet

- Spells, parry, weapon durability in combat and bounties.
- A built-in AI: NPCs only run their Lua script (`on_think`, `on_speech`, `on_spawn`,
  `on_mobile_in_range`), which can walk them along a [path](scripting/mobile-scripts.md#walking-a-path) with
  `npc.walk_to`; chasing, fleeing and fighting come from the shipped `creature.lua` and `monster.lua`, not from the engine.
- Recall and runebooks (the public moongates work).
- Houses and boats (placement, and multis in movement and line of sight).
- Dressing other characters and strength requirements. Weight limits of containers are enforced.
- Region rules for housing. Regions drive the weather, the dungeon light, the music, the season and the guards' protection.
- Spawner items (the [spawn regions](spawns.md) do the respawning).
- Per-player language, a restore command for the SQL backups, a web administration panel.
- Old Kingdom Reborn AES/E3 encryption. The Enhanced Client logs in, creates a character, enters the world and
  walks; the rest is partial: see [Enhanced Client](enhanced-client.md).

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
  ModernUO), line of sight (as POL and ModernUO). Ground items block movement (a closed door, a crate); mobiles and
  multis are not part of these checks yet, and line of sight ignores items. See [World queries](world-queries.md).
- **Map sectors:** players, NPCs and ground items are seen within the view range; NPCs away from
  every player sleep.
- **Light:** a game clock with day and night by map and longitude, and the phases of the two moons;
  dark dungeons and dim jails.
- **Regions, weather, music and seasons:** the region of every player is followed; each region has
  UOX3's weather, rolled every game hour (dry indoors), its music track and, if set, its season; the
  maps' seasons can rotate with the game days.
- **Decoration:** ModernUO's world decoration (and ServUO's New Haven) placed by `.decorate`: doors
  (those of the towns read from the map's door frames), locks and keys, shop signs, lights, and
  teleporters, both the ones a player steps on and the ones that answer a word; the town lamp posts
  light up at night.
- **Effects:** graphic effects at a spot, on a mobile or a ground item, flying from one to another,
  and lightning, from scripts with the `effect` module; particles for the Enhanced Client.
- **NPC spawns:** spawn regions on every map, from UOX3's data and ModernUO's for New Haven, Malas,
  Tokuno and TerMur. Each region fills to its maximum at its first spawn after the start, then
  respawns NPCs gradually, on land and on water. See [NPC spawns](spawns.md).

### Scripting

- Sandboxed Lua 5.2 with an instruction budget, `wait`, timers, events, hot reload and editor
  definitions. See [Writing Lua scripts](scripting.md).
- Modules: `engine`, `log`, `timer`, `events`, and in the Ultima plugin `dice`, `localization`,
  `npc`, `item`, `world`, `mobile`, `target`, `prompt`, `gump`, `bank`, `effect`, `moongates`, `locations`, `jail`, `board`, `book`, `commands`,
  `combat`, `craft`, `harvest`, `help`, `hue_picker`, `mount`, `npcguild`, `pet`, `schedule`, `skill`, `stable`, `trainer` and `vendor`: 34 modules in all.
  Every function they give scripts is listed in the [Lua API reference](https://moongate.sh/lua/),
  generated from the server's code.
- Gumps: XML layouts checked by `gump.xsd`, a Lua script per gump for the answers, slots and whole
  gumps built in Lua, and gumps chained with `bind` and `open`; see [Gumps](gumps.md) and
  [Your first gump](gump-tutorial.md).
- Mobile and item scripts are bound from their templates by `script_id`. The root ships 81 Lua files in `scripts/`:
  32 item scripts (doors, lights, teleporters and moongates, clocks, books and scrolls, bulletin boards, food and drink, dyes,
  tools such as axe, pickaxe and fishing pole, ore and forge, ethereal mounts, treasure chests, training dummies),
  11 mobile scripts (`wander.lua`, `monster.lua`, `guard.lua`, `banker.lua`, `shopkeeper.lua`, `healer.lua`, `stablemaster.lua`,
  animals and the cats Orione and Vega), 9 skill scripts, 13 gump scripts, 14 shared helpers in `common/`, the Halloween and
  Christmas events. `definitions.lua` is generated at startup and not shipped. `potion.lua` only consumes the item.
- Hooks scripts can implement include `on_use`, `on_speech`, `on_think`, `on_spawn`, `on_death`, `on_mobile_killed`,
  `on_mobile_in_range`, `on_drop`, `on_equip`, `on_login` and `on_timer`.
- Not yet: timers on mobiles, and events for attacks, hits and damage.

### Data and templates

- Shard data in `data/` (maps, starting cities, skills, professions, races, names, containers,
  bodies, regions, weather, messages), validated at startup. See [Data files](data-files.md).
- Templates in `templates/`: items, loot, mobiles, NPC lists, spawn regions and decoration, with
  `base_id` inheritance, and the XML gumps of `templates/gumps`. See
  [Loading TOML templates](templates.md) and [Gumps](gumps.md).
- Plain document sources in `templates/books`, with named variables resolved and saved on individual
  scrolls and books at creation. [Letter attachments](data-files/books.md#letter-attachments) are frozen per letter and delivered once to its backpack bearer, with deferred weight and atomic capacity checks. The converter ships [62 lore books in eight languages](book-content-import.md) from ModernUO. Native books have covers and turnable pages; writable books let their carrier edit the title, author and pages, with changes saved on the item. See [Readable text templates](data-files/books.md).
- Client files read from `ultima.ultima_path`: tile data, maps (MUL or UOP) and multis.
- Messages in 8 languages, ported from UOX3; a language can be split into several toml
  files. See [Localization](localization.md).

### Persistence

- Two PostgreSQL databases (accounts and world) with transactions and versioned SQL migrations,
  applied by `mgctl migrate`, or at startup when `persistence.auto_apply_migrations` is on.
- Characters, their items, ground items and NPCs are kept in memory and written by the periodic
  world save; characters are also saved when they leave.
- Not yet: a restore command; restoring a [SQL backup](persistence-operations.md#database-backups) is a manual `psql` step.

### Administration and tools

- Console and in-game [commands](commands.md), translated in every shipped language.
- A logged exception is one line on the console and a Markdown report under `logs/errors`, ready
  for a GitHub issue; see [When something fails](getting-started.md#when-something-fails).
- Optional gRPC [administration API](admin-api.md) with TLS: accounts and server info. No web panel
  or character operations yet.
- Plugins under `plugins/` register services, commands, Lua modules, metrics, entities, SQL and
  their own config section. See [Writing a plugin](plugins.md).
- Tools: [`mgctl`](mgctl.md) prepares the server root, applies the database migrations; the Python
  [`moongate-convert`](uox3-migration.md#run-it) converts UOX3 and ModernUO content. A [Docker example](docker-login-realms.md) runs one login and
  two game servers.

## Settings with no effect yet

The `--log-level` and `--log-packets` command-line options are parsed and validated but nothing
uses them yet; see the
[configuration reference](server-configuration.md#settings-and-validation).
