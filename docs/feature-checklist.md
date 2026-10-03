# Feature checklist

The systems a complete Ultima Online server emulator usually offers, and where Moongate stands on
each. It complements the [Implementation status](implementation-status.md), which describes what
works today in more detail. The [Roadmap](roadmap.md) says in which order the missing systems are
built.

✅ done · 🟡 partly done · ❌ not built yet

**268 systems:** ✅ 64 done, 🟡 38 partly done, ❌ 166 not built yet.

**Coverage: 24%** of the systems done, **31%** counting a partly done system as half.

The foundations (network, login, persistence, scripting, world data) are in place; the gameplay systems (combat, magic, skills, economy, housing) are what is left.

## Accounts, login and network

| System | Moongate | Notes |
| --- | --- | --- |
| Accounts | ✅ | Create, list and verify from the console and the administration API; access levels |
| Login and server list | ✅ | Separate login server and game realms, or one standalone process |
| Character select, create and delete | ✅ | Deletion is delayed and can be restored by staff |
| Message of the day | ✅ | Configurable and extensible |
| Client versions and encryption | ✅ | POL-compatible encryption policies; the [Enhanced Client](enhanced-client.md) logs in, creates a character and enters the world, the rest is partial |
| UDP ping server | ✅ | Echoes pings on UDP 12000, as ModernUO does |
| Keepalive and idle clients | ✅ | |
| IP bans and firewall | ❌ | |
| Account bans and kicks | ❌ | |
| Assistant (Razor) feature negotiation | ❌ | |
| Shard status for public shard lists | ❌ | |
| Safe logout in inns and houses | ❌ | A character leaves the world when its session closes |
| Hashed passwords | ✅ | |
| Login attempt limits and connection flood protection | ❌ | |
| Proxy protocol (real client address behind a proxy) | ❌ | |
| Packet filtering by login phase | 🟡 | Unknown packets are refused; no per-phase filter |
| Packet hooks: override or extend any packet | 🟡 | Plugins add incoming packets in C#; not from scripts |
| Suspicious acts logging (forged replies, out-of-range drops) | ❌ | Out-of-range actions are refused, not logged as suspicious |
| Network statistics per client | ❌ | |
| One or many characters in the world per account | 🟡 | One per account, not configurable |
| Login and logout scripts | 🟡 | Script events when a character enters or leaves the world |
| Obscene word filter | 🟡 | Banned names at character creation; nothing on speech |

## Characters

| System | Moongate | Notes |
| --- | --- | --- |
| Creation: races, professions, starting items, starting cities | ✅ | |
| Stats | 🟡 | Rolled and stored; no gain, caps or locks |
| Skills | 🟡 | Stored; no use, gain, caps or locks |
| Hit points, mana and stamina regeneration | ❌ | |
| Titles | 🟡 | Fame and karma titles in the paperdoll; no skill titles |
| Fame and karma | 🟡 | Set by staff (`.fame`, `.karma`); nothing gains or loses them yet |
| Notoriety (innocent, criminal, murderer) | 🟡 | Name colour from the mobile template |
| Crimes, criminal timer and murder counts | ❌ | |
| Hunger and thirst | ❌ | |
| Poison | ❌ | |
| Hiding and stealth | ❌ | |
| Death, corpses, ghosts and resurrection | ❌ | |
| Young player protection | ❌ | |
| Murder reports and bounty boards | ❌ | |
| Virtues | ❌ | |
| Status bar | ✅ | Name, stats, hit points, mana, stamina, gold, weight |
| Extended status (resistances, luck, caps, stat locks) | ❌ | |
| Staff privileges (move anything, see hidden, invulnerable) | 🟡 | Hidden ground items are shown only to staff, and `.go` takes a game master anywhere; no move anything or invulnerability |
| Gargoyle flying | ❌ | |
| Movement cost and stamina use by weight | ❌ | |
| Polymorph and incognito | ❌ | |
| Experience and levels (optional) | ❌ | |
| Factions | ❌ | |
| Slayer weapons | ❌ | |
| Renaming pets and characters | ❌ | |
| Face selection | ❌ | |

## Combat

| System | Moongate | Notes |
| --- | --- | --- |
| War mode, melee and swing timing | ❌ | |
| Archery | ❌ | |
| Weapons and armour: damage, armour, durability, resistances | ❌ | NPC templates carry damage and armour; item templates do not yet |
| Parrying | ❌ | |
| Weapon special moves | ❌ | |
| NPC combat AI | ❌ | |
| Guards in guarded regions | ❌ | |
| Monster special abilities | ❌ | |
| Elemental damage and resistances | ❌ | |
| Aggressor lists and attack timeouts | ❌ | |
| Damage numbers over heads | ❌ | |

## Magic

| System | Moongate | Notes |
| --- | --- | --- |
| Spell casting: mana, reagents, fizzle, resist | ❌ | |
| The magery spells | ❌ | |
| Necromancy | ❌ | |
| Spellbooks, scrolls and wands | ❌ | |
| Fields and summons | ❌ | |
| Region magic rules (no recall, no gate) | ❌ | |
| Chivalry | ❌ | |
| Bushido | ❌ | |
| Ninjitsu | ❌ | |
| Spellweaving | ❌ | |
| Mysticism | ❌ | |
| Skill masteries | ❌ | |
| Words of power | ❌ | |

## Skills

| System | Moongate | Notes |
| --- | --- | --- |
| Using a skill and gaining it | ❌ | |
| Gathering: mining, lumberjacking, fishing | ❌ | |
| Crafting engine: menus, recipes, resources, quality | ❌ | |
| The crafts: blacksmithing, tailoring, carpentry, tinkering, alchemy, cooking, inscription, fletching, cartography | ❌ | |
| Repairing and enhancing items | ❌ | |
| Taming and animal lore | ❌ | |
| Healing and veterinary | ❌ | |
| Lockpicking, remove trap | ❌ | Doors lock and open with their key |
| Snooping and stealing | ❌ | |
| Tracking, detect hidden, forensics, spirit speak | ❌ | |
| Bard skills: musicianship, peacemaking, provocation, discordance | ❌ | |
| Lore skills: anatomy, arms lore, item ID, evaluate intelligence, taste ID | ❌ | |
| Meditation, begging, herding, camping, poisoning | ❌ | |
| Carving corpses | ❌ | |
| Resource regions (ore, wood, fish per area, regrowing) | ❌ | |
| Resource processing: smelting, looms, spinning wheels, hides | ❌ | |
| Skill classes and caps (skill total, stat total) | ❌ | |
| Training objects: dummies, pickpocket dips, archery buttes | ❌ | |

## NPCs

| System | Moongate | Notes |
| --- | --- | --- |
| NPC templates, names, equipment and loot | ✅ | Dressed and with their loot at spawn |
| Scripted behaviour | ✅ | Lua mobile scripts: `on_think`, `on_speech`, `on_spawn`, `on_mobile_in_range` |
| Sleeping away from players | ✅ | NPCs think only near a player |
| Wandering | 🟡 | `wander.lua` keeps spawned NPCs in their home area |
| Speech keywords and answers | 🟡 | The client's keywords reach `on_speech` in any language; the bankers answer *bank*; no vendor keywords yet |
| Water and amphibious creatures | ✅ | They spawn on water and swim |
| AI types (vendor, guard, healer, animal, monster, caster) | ❌ | |
| Pathfinding, following and fleeing | 🟡 | A* path search; a script walks an NPC to a place or after someone with `npc.walk_to`, around closed doors and furniture. No fleeing, NPCs do not open doors, and mobiles do not block a path |
| Pets and followers: commands, loyalty, bonding | ❌ | |
| Mounts | ❌ | |
| Script events for NPCs (speech, range, damage) | 🟡 | Speech and range; no combat events |
| Name pools | ✅ | Name lists by kind and gender |
| Needs: food, grazing, desires | ❌ | |
| Special creature actions (breath, rock throwing, webs) | ❌ | |
| Returning home when lost | 🟡 | Spawned NPCs walk back to their home area |
| Pet figurines (shrinking pets) | ❌ | |
| Champion spawns | ❌ | |
| Peerless bosses and paragons | ❌ | |
| Camps (brigands, orcs, prisoners) | ❌ | |

## NPC services

| System | Moongate | Notes |
| --- | --- | --- |
| Vendors: buy, sell, restock | ❌ | Item prices are in the templates; nothing buys or sells |
| Banker and bank box | ✅ | The *bank* keyword in any client language; open while the player stands still; see [Bank](bank.md) |
| Stable master, veterinarian | ❌ | |
| Skill trainers | ❌ | |
| Healers that resurrect | ❌ | |
| Player vendors | ❌ | |
| Hirelings | ❌ | |
| Escort quests | ❌ | |
| Guildmasters | ❌ | |
| Bulk order deeds | ❌ | |

## Items

| System | Moongate | Notes |
| --- | --- | --- |
| Item templates and creation | ✅ | TOML templates with inheritance |
| Moving, stacking, splitting and merging | ✅ | |
| Wearing: layers and two-handed weapons | ✅ | No strength requirements yet |
| The character's own containers | ✅ | |
| Containers on the ground, weight and item limits | ❌ | |
| Tooltips and single-click names | ✅ | |
| Items on the ground and their decay | ✅ | |
| Scripted items | ✅ | Lua item scripts: use, equip, unequip, pick up, drop, create, darkness, a player stepping on them, speech nearby |
| Loot tables | ✅ | Rolled into every spawned NPC's backpack |
| Doors | ✅ | Open and close; linked double doors; a closed door blocks the way, staff walks through |
| Locks and keys | ✅ | Locked doors open for a player carrying their key |
| Lights | ✅ | Lit and doused; lamp posts light up at night |
| Potions and food | 🟡 | A sample potion is drunk and used up; no effects yet |
| Books | ❌ | |
| Maps and treasure maps | ❌ | |
| Runes, recall and gates | ❌ | |
| Moongates and teleporters | 🟡 | Walk-on teleporters and those that answer a word, also across maps, placed by `.decorate` with ModernUO's world and dungeon ones; public moongates with a destination gump; plain moongates with one destination (`.moongate`); no Gate Travel |
| Dyes and dye tubs | ❌ | |
| Hair stylist, hair dye and disguise kit | ❌ | |
| Secure trade | ❌ | |
| Magic items and item properties | ❌ | |
| Random magic loot and artifacts | ❌ | |
| Imbuing and reforging | ❌ | |
| Power scrolls, stat scrolls and soulstones | ❌ | |
| Games (chess, checkers, backgammon) | ❌ | |
| Plants and farming | ❌ | |
| Dungeon traps and puzzles | ❌ | |
| Dungeon and town chests that refill | ❌ | |
| Messages in a bottle and fished-up treasure | ❌ | |
| Item attributes: blessed, cursed, newbie, insured | 🟡 | Blessed and cursed in tooltips; no rules behind them |
| Deeds and redeeding | ❌ | |
| Bandages | ❌ | |
| Musical instruments | ❌ | |
| Navigation tools: sextant, spyglass, clocks | ❌ | |
| Potion kegs and commodity deeds | ❌ | |
| Trash cans | ❌ | |
| Communication crystals | ❌ | |
| Item stones (dispensers) | ❌ | |
| Cannons | ❌ | |
| Talismans and jewelry properties | ❌ | |
| Integrity checks and orphan detection | ❌ | |

## World

| System | Moongate | Notes |
| --- | --- | --- |
| Maps, statics and multis from the client files | ✅ | MUL and UOP |
| Movement and line of sight checks | ✅ | Terrain and statics; not items, mobiles or multis yet |
| Map sectors and view range | ✅ | |
| Day and night | ✅ | By map and longitude, with the moon phases; `.globallight`, `.time` |
| Dungeon and jail light | ✅ | |
| Weather by region | ✅ | Rain, snow, storms with thunder, dry indoors; no weather damage |
| Seasons | ✅ | By map and region, optional rotation with the game days; `.season` |
| Regions | 🟡 | Found for every player; they set the weather, the music, the season and the dungeon light; no guards or housing rules |
| Region music | ✅ | The region's track, else the map's; `.music` |
| Region rules: safe, no PvP, no housing, instant logout | ❌ | |
| Town politics (mayors, taxes) | ❌ | |
| World decoration | ✅ | Placed by `.decorate`, with the shop signs and the town doors read from the map |
| Spawn regions | ✅ | On every map: UOX3's data, ModernUO's for New Haven, Malas, Tokuno and TerMur; fast first fill, `.initial_spawn`, respawn, land and water; see [NPC spawns](spawns.md) |
| Spawner items | ❌ | |
| Housing: placement, sign, owners, friends and bans | ❌ | |
| House lockdowns, secure containers and decay | ❌ | |
| House addons (forges, looms, multi-piece furniture) | ❌ | |
| Boats | ❌ | |
| Facet changes and facet rules | ❌ | |
| Ambient sounds | ❌ | |
| Several maps at once | ✅ | Every map of `maps.toml` |
| Moon phases | ✅ | Trammel and Felucca on the game clock, as ModernUO's spyglass; `.time`, `world.moon` in Lua |
| Custom house design | ❌ | |
| Sector sleep | ✅ | NPCs away from players cost nothing |
| World import and export | ❌ | |
| Parallel and incremental world saves | 🟡 | Only the snapshots that changed are written, in one background transaction; not parallel |

## Social

| System | Moongate | Notes |
| --- | --- | --- |
| Local speech | ✅ | Players and NPCs hear what is said nearby |
| Party | ❌ | |
| Guilds | ❌ | |
| Chat window | ❌ | |
| Bulletin boards | ❌ | |
| Duels, arenas and tournaments | ❌ | |
| Character profile | ❌ | |
| Tips window | ❌ | |
| Quest arrow and quest button | ❌ | |
| Quests | ❌ | |
| Speech modes: say, whisper, yell, emote | 🟡 | Local speech; no whisper or yell ranges |

## Economy

| System | Moongate | Notes |
| --- | --- | --- |
| Gold | 🟡 | Starting gold and NPC loot; nothing to spend it on |
| Banking and bank checks | 🟡 | The bank box; no withdraw, balance or checks |
| House costs and limits | ❌ | |
| Account-wide gold | ❌ | |
| Vendor search and auctions | ❌ | |
| Veteran rewards | ❌ | |

## Administration

| System | Moongate | Notes |
| --- | --- | --- |
| Commands with access levels | ✅ | From the console and in game; see [Commands](commands.md) |
| World save | ✅ | Periodic and on shutdown, with `.save` |
| Database backup | ✅ | Rotating SQL exports on a schedule and with `.sql_backup`; restore with psql |
| Console | ✅ | |
| Server configuration | ✅ | `moongate.toml`, validated at startup |
| Era ruleset (classic or modern rules) | ❌ | One ruleset, the classic one first; see the [Roadmap](roadmap.md) |
| Settings for the game rules (combat, magic, regeneration, crimes) | ❌ | They arrive with each system |
| Account administration | 🟡 | Console and administration API; no bans |
| Remote administration | 🟡 | gRPC API with TLS; no web panel |
| Metrics and diagnostics | ✅ | Process and plugin metrics |
| Hot reload | 🟡 | Lua scripts; not the data or templates |
| GM help queue (pages) | ❌ | |
| Help menu and stuck menu | ❌ | |
| Jails | ❌ | Jail regions are dim, nothing more |
| Who list | ❌ | |
| Staff tools: property gump, add menu, area commands | 🟡 | The gump of the named places, `.go`, with ModernUO's 558 places; no property gump, add menu or area commands |
| Named locations and travel menu for staff | ❌ | `.go` takes coordinates |
| Web status pages | ❌ | |
| Bug reports | 🟡 | Exception reports ready for a GitHub issue; no in-game report |
| Logging | ✅ | Structured logs with levels |
| Packet logging per client | ❌ | The `--log-packets` option is parsed but unused |
| Command log | ❌ | |
| Remote console (telnet) | ❌ | The gRPC API instead |
| Crash dumps and watchdog | ❌ | |
| Windows service | ❌ | Docker images instead |

## Scripting and content

| System | Moongate | Notes |
| --- | --- | --- |
| Script engine | ✅ | Sandboxed Lua 5.2 with an instruction budget |
| Scripts bound to templates | ✅ | `script_id` on item and mobile templates |
| Several scripts on one object, scripts by item type | ❌ | |
| Script events | 🟡 | NPC, item and character events, and `player_say`; no combat, skill or region events |
| Events that can refuse the default action | 🟡 | Only `on_use` |
| Region enter and leave events | ❌ | |
| Script API | 🟡 | `npc`, `item`, `world`, `mobile`, `gump`, `bank`, `effect`, `moongates`, `locations`, `dice`, `localization`, `timer`, `events`, `engine`, `log`; no stats, skills or inventory API |
| World queries from scripts (objects near, in sight, by serial) | 🟡 | `world.is_occupied`, `world.carries` |
| Creating and moving items from scripts | 🟡 | Move on the ground, consume, delete; no create, container or equip |
| Messages, target cursor and prompts from scripts | 🟡 | `npc.say`, `item.message`, gumps; no system message, target or prompt |
| Script timers | ✅ | |
| Timers kept by an object and saved with the world | ❌ | A timer is lost on a restart |
| Commands from plugins | ✅ | In C#; not from Lua |
| Data-driven content | ✅ | TOML templates and data files, validated at startup |
| Importing another emulator's content | ✅ | UOX3 items, loot, NPCs, names, starting items, NPC lists and spawn regions; ModernUO spawners |
| Runaway script protection | ✅ | Instruction budget per resume and per chunk |
| Persistent values on objects | ✅ | Props on items and NPCs, saved with the world |
| Global persistent script data | ❌ | |
| Script debugger for an IDE | ❌ | Editor definitions for completion only |
| Script profiling | 🟡 | Script metrics; no per-function profile |
| Files, HTTP, SQL and email from scripts | ❌ | |
| External TCP services handled by scripts | ❌ | |
| Scriptable gumps and dialogs | ✅ | XML layouts with Lua callbacks, slots and builder; see [Your first gump](gump-tutorial.md) |
| Hooks that replace core rules (skill check, combat, decay) | ❌ |  |
| Overridable system messages | ✅ | Every message in the `data/messages` files |

## Interface

| System | Moongate | Notes |
| --- | --- | --- |
| Target cursor | ✅ | |
| Localized messages | ✅ | 8 languages; a language can be split into `data/messages/<language>/*.toml` |
| Races | 🟡 | Human, elf and gargoyle bodies and looks; no racial gameplay |
| Gumps | ✅ | XML layouts checked by an XSD, Lua scripts, gumps built in Lua, chained gumps, checked answers |
| Menus | ❌ | |
| Context menus | ❌ | |
| Buff bar | ❌ | |
| Walk sequence and speed checks | ✅ | |
| Weight and overloading | 🟡 | Items have their weight; nothing overloads |
| Timed effects (buffs and debuffs) | ❌ | |
| Text prompts and input | ❌ | |
| Visual effects: moving, lightning, particles | ✅ | From scripts with the `effect` module; particles for the Enhanced Client |
| Sounds and music | ✅ | Sounds from scripts, thunder and region music |
| Client language | ❌ | One server language for everyone, out of the 8 shipped |
| Store and other modern client panels | ❌ | |

## What Moongate adds

Systems most emulators do not have:

- Login server and game realms as separate processes, discovered through Redis, with one-use
  handoff tickets.
- PostgreSQL persistence with versioned migrations, applied by `mgctl migrate` while the server is
  stopped.
- World saves that never stop the game: about 0.1 s on the game loop to copy 173,000 entities, then
  only the changed rows written in the background; see
  [A save does not stop the game](persistence-operations.md#a-save-does-not-stop-the-game).
- A gRPC administration API with TLS.
- Plugins that add services, commands, Lua modules, metrics, entities and their own settings.
- Docker images and a multi-realm example.
