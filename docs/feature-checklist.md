# Feature checklist

The systems a complete Ultima Online server emulator usually offers, and where Moongate stands on
each. It complements the [Implementation status](implementation-status.md), which describes what
works today in more detail.

✅ done · 🟡 partly done · ❌ not built yet

**150 systems:** ✅ 45 done, 🟡 17 partly done, ❌ 88 not built yet. The foundations (network, login, persistence, scripting, world data) are in place; the gameplay systems (combat, magic, skills, economy, housing) are what is left.

## Accounts, login and network

| System | Moongate | Notes |
| --- | --- | --- |
| Accounts | ✅ | Create, list and verify from the console and the administration API; access levels |
| Login and server list | ✅ | Separate login server and game realms, or one standalone process |
| Character select, create and delete | ✅ | Deletion is delayed and can be restored by staff |
| Message of the day | ✅ | Configurable and extensible |
| Client versions and encryption | ✅ | POL-compatible encryption policies; Enhanced Client not tested |
| Keepalive and idle clients | ✅ | |
| IP bans and firewall | ❌ | |
| Account bans and kicks | ❌ | |
| Assistant (Razor) feature negotiation | ❌ | |
| Public shard list polls | ❌ | |
| Safe logout in inns and houses | ❌ | A character leaves the world when its session closes |

## Characters

| System | Moongate | Notes |
| --- | --- | --- |
| Creation: races, professions, starting items, starting cities | ✅ | |
| Stats | 🟡 | Rolled and stored; no gain, caps or locks |
| Skills | 🟡 | Stored; no use, gain, caps or locks |
| Hit points, mana and stamina regeneration | ❌ | |
| Titles | 🟡 | Fame and karma titles in the paperdoll; no skill titles |
| Fame and karma | 🟡 | Set by staff (`.fame`, `.karma`); nothing gains or loses them yet |
| Notoriety (innocent, criminal, murderer) | 🟡 | Name colour from the mobile template; no crimes or murder counts |
| Hunger and thirst | ❌ | |
| Poison | ❌ | |
| Hiding and stealth | ❌ | |
| Death, corpses, ghosts and resurrection | ❌ | |
| Young player protection | ❌ | |
| Virtues | ❌ | |

## Combat

| System | Moongate | Notes |
| --- | --- | --- |
| War mode, melee and swing timing | ❌ | |
| Archery | ❌ | |
| Weapons and armour: damage, armour, durability, resistances | ❌ | Templates carry damage, armour and resistances |
| Parrying | ❌ | |
| Weapon special moves | ❌ | |
| NPC combat AI | ❌ | |
| Guards in guarded regions | ❌ | |
| Monster special abilities | ❌ | |

## Magic

| System | Moongate | Notes |
| --- | --- | --- |
| Spell casting: mana, reagents, fizzle, resist | ❌ | |
| The magery spells | ❌ | |
| Necromancy and other schools | ❌ | |
| Spellbooks, scrolls and wands | ❌ | |
| Fields and summons | ❌ | |
| Region magic rules (no recall, no gate) | ❌ | |

## Skills

| System | Moongate | Notes |
| --- | --- | --- |
| Using a skill and gaining it | ❌ | |
| Gathering: mining, lumberjacking, fishing | ❌ | |
| Crafting: blacksmithing, tailoring, carpentry, tinkering, alchemy, cooking, inscription, fletching, cartography | ❌ | |
| Taming and animal lore | ❌ | |
| Healing and veterinary | ❌ | |
| Lockpicking, remove trap | ❌ | Doors lock and open with their key |
| Snooping and stealing | ❌ | |
| Tracking, detect hidden, forensics, spirit speak | ❌ | |
| Bard skills: musicianship, peacemaking, provocation, discordance | ❌ | |
| Lore skills: anatomy, arms lore, item ID, evaluate intelligence, taste ID | ❌ | |
| Meditation, begging, herding, camping, poisoning | ❌ | |
| Carving corpses | ❌ | |

## NPCs

| System | Moongate | Notes |
| --- | --- | --- |
| NPC templates, names, equipment and loot | ✅ | Dressed and with their loot at spawn |
| Scripted behaviour | ✅ | Lua mobile scripts: `on_think`, `on_speech`, `on_spawn`, `on_mobile_in_range` |
| Sleeping away from players | ✅ | NPCs think only near a player |
| Wandering | 🟡 | `wander.lua` keeps spawned NPCs in their home area |
| Speech keywords and answers | 🟡 | From the NPC's Lua script |
| Water and amphibious creatures | ✅ | They spawn on water and swim |
| AI types (vendor, guard, healer, animal, monster, caster) | ❌ | |
| Pathfinding, following and fleeing | ❌ | |
| Pets and followers: commands, loyalty, bonding | ❌ | |
| Mounts | ❌ | |

## NPC services

| System | Moongate | Notes |
| --- | --- | --- |
| Vendors: buy, sell, restock | ❌ | |
| Banker and bank box | ❌ | |
| Stable master, veterinarian | ❌ | |
| Skill trainers | ❌ | |
| Healers that resurrect | ❌ | |
| Player vendors | ❌ | |
| Hirelings and escort quests | ❌ | |
| Guildmasters, bulk order deeds | ❌ | |

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
| Scripted items | ✅ | Lua item scripts: use, equip, pick up, drop, create, darkness |
| Loot tables | ✅ | Rolled into every spawned NPC's backpack |
| Doors | ✅ | Open and close; linked double doors |
| Locks and keys | ✅ | Locked doors open for a player carrying their key |
| Lights | ✅ | Lit and doused; lamp posts light up at night |
| Potions and food | 🟡 | A sample potion is drunk and used up; no effects yet |
| Books | ❌ | |
| Maps and treasure maps | ❌ | |
| Runes, recall and gates | ❌ | |
| Moongates and teleporters | ❌ | |
| Dyes and dye tubs | ❌ | |
| Secure trade | ❌ | |
| Corpses | ❌ | |
| Magic items | ❌ | |
| Games (chess, checkers), plants, farming | ❌ | |
| Dungeon traps and puzzles | ❌ | |

## World

| System | Moongate | Notes |
| --- | --- | --- |
| Maps, statics and multis from the client files | ✅ | MUL and UOP |
| Movement and line of sight checks | ✅ | Terrain and statics; not items, mobiles or multis yet |
| Map sectors and view range | ✅ | |
| Day and night | ✅ | By map and longitude; `.globallight` |
| Dungeon and jail light | ✅ | |
| Weather by region | ✅ | Rain, snow, storms with thunder, dry indoors; no weather damage |
| Seasons | ❌ | |
| Regions | 🟡 | Found for every player; they set the weather and the dungeon light |
| Guarded towns, region music and region rules | ❌ | |
| Town politics (mayors, taxes) | ❌ | |
| World decoration | ✅ | Placed by `.decorate` |
| Spawn regions | ✅ | Gradual fill and respawn, land and water; see [NPC spawns](spawns.md) |
| Spawner items | ❌ | |
| Housing | ❌ | |
| Boats | ❌ | |
| Facet changes and facet rules | ❌ | |
| Ambient sounds | ❌ | |

## Social

| System | Moongate | Notes |
| --- | --- | --- |
| Local speech | ✅ | Players and NPCs hear what is said nearby |
| Party | ❌ | |
| Guilds | ❌ | |
| Chat window | ❌ | |
| Bulletin boards | ❌ | |
| Character profile | ❌ | |

## Economy

| System | Moongate | Notes |
| --- | --- | --- |
| Gold | 🟡 | Starting gold and NPC loot; nothing to spend it on |
| Banking and bank checks | ❌ | |
| Vendor prices | ❌ | |
| House costs and limits | ❌ | |

## Administration

| System | Moongate | Notes |
| --- | --- | --- |
| Commands with access levels | ✅ | From the console and in game; see [Commands](commands.md) |
| World save | ✅ | Periodic and on shutdown, with `.save` |
| Console | ✅ | |
| Server configuration | ✅ | `moongate.toml`, validated at startup |
| Account administration | 🟡 | Console and administration API; no bans |
| Remote administration | 🟡 | gRPC API with TLS; no web panel |
| Metrics and diagnostics | ✅ | Process and plugin metrics |
| Hot reload | 🟡 | Lua scripts; not the data or templates |
| GM help queue (pages) | ❌ | |
| Jails | ❌ | Jail regions are dim, nothing more |
| Who list | ❌ | |
| Web status pages | ❌ | |
| Backups | ❌ | |
| Bug reports | ❌ | |

## Scripting and content

| System | Moongate | Notes |
| --- | --- | --- |
| Script engine | ✅ | Sandboxed Lua 5.2 with an instruction budget |
| Scripts bound to templates | ✅ | `script_id` on item and mobile templates |
| Script events | 🟡 | NPC and item events; no combat, skill, login or region events |
| Script API | 🟡 | `npc`, `item`, `world`, `dice`, `localization`, `timer`, `events`; no character or inventory API |
| Script timers | ✅ | |
| Commands from plugins | ✅ | In C#; not from Lua |
| Data-driven content | ✅ | TOML templates and data files, validated at startup |
| Importing another emulator's content | ✅ | Items, loot, NPCs, names, starting items, NPC lists and spawn regions |

## Interface

| System | Moongate | Notes |
| --- | --- | --- |
| Target cursor | ✅ | |
| Localized messages | ✅ | 8 languages |
| Races | 🟡 | Human, elf and gargoyle bodies and looks; no racial gameplay |
| Gumps | ❌ | |
| Menus | ❌ | |
| Context menus | ❌ | |
| Buff bar | ❌ | |
| Walk sequence and speed checks | ✅ | |
| Weight and overloading | 🟡 | Items have their weight; nothing overloads |
| Timed effects (buffs and debuffs) | ❌ | |

## What Moongate adds

Systems most emulators do not have:

- Login server and game realms as separate processes, discovered through Redis, with one-use
  handoff tickets.
- PostgreSQL persistence with versioned migrations and a separate migration runner.
- A gRPC administration API with TLS.
- Plugins that add services, commands, Lua modules, metrics, entities and their own settings.
- Docker images and a multi-realm example.
