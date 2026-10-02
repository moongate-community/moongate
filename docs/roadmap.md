# Roadmap

What Moongate builds next, and in which order. The [feature checklist](feature-checklist.md) says
what is missing system by system; this page says what comes first and why.

The order comes from a comparison with five other emulators: ModernUO, ServUO, UOX3, POL and
SphereServer X. For each one we looked at which systems it has, how large they are, and what each
system needs before it can exist. The five agree on the same chain of dependencies, so the phases
below follow that chain. Inside a phase, the systems a player notices first come first.

This is an order, not a schedule: there are no dates. A phase is done when its systems are marked
✅ in the checklist.

## Where we are

The foundations are in place: network, login, persistence, world data, sectors, regions, spawns,
decoration, gumps, Lua scripting. A player can log in, walk a populated world, open doors, use the
bank and take a teleporter. What is missing is the gameplay: nothing can be fought, learned, bought
or built yet.

## One decision first: the ruleset

Every other emulator has to choose between the classic rules (before Age of Shadows) and the modern
ones (item properties, resistances, special moves). ModernUO checks the era in 378 files; UOX3 and
Sphere carry a setting for it.

Moongate builds **one** ruleset first: the classic one. It is smaller, the UOX3 data we already
convert is written for it, and the modern systems can be added on top later (see
[Later](#later)). The Enhanced Client keeps working: it only needs the packets, not the modern
rules.

## Phase 0: what Lua needs before any gameplay

POL leaves almost all gameplay to scripts, and Sphere and UOX3 do the same through triggers. All
three show that the core has to expose these things before combat, magic or crafting can be
written. Today the Lua surface has 5 events, 4 mobile functions and 9 item functions.

| Step | What | Why it comes here |
| --- | --- | --- |
| 0.1 | **Mobile state in Lua**: stats, skills, hits, mana, stamina, flags (hidden, frozen, dead, war mode), hue, body, name; props on players; global props | Every rule reads or changes them |
| 0.2 | **World queries**: mobiles and items near a point, in a box, in sight; find by serial; online players; tile and height lookups | Every AI, spell and area effect needs them |
| 0.3 | **Item API**: create (on the ground, in a backpack, in a container), move into a container, equip, list the content, find by type, set hue and name | Loot, crafting, vendors, quests |
| 0.4 | **Player input and output**: system message, text over any object, cliloc messages, the target cursor, a text prompt | Every skill and spell starts with a target |
| 0.5 | **Events that can refuse**: a handler stops or changes the default action (`can_equip`, `can_insert`, `can_pick_up`, and later `check_skill`, `on_damage`) | Lets a script own a rule; today only `on_use` can refuse |
| 0.6 | **Timers kept by the object** and saved with the world | A door's auto-close timer is lost on a restart today; timed effects need the same mechanism |
| 0.7 | **Region events**: enter and leave | Guards, magic rules, music scripts, quests |

## Phase 1: a character that lives

| Step | What | Why it comes here | Data ready to import |
| --- | --- | --- | --- |
| 1.1 | **Regeneration** of hits, mana and stamina; hunger | Nothing depends on anything else; visible in the first fight | |
| 1.2 | **Skill use, check and gain**; stat gain; caps and locks | The progression of the game; every later system calls the skill check | UOX3 `skills.dfn` (61 skills: stat weights, gain curves), ModernUO `skills.json` |
| 1.3 | **Timed effects**: one mechanism for poison, curses, blessings, polymorph, hiding; the buff bar shows them | Magic, potions and combat all need it | |
| 1.4 | **Containers on the ground**, with item and weight limits; weight and overloading | Corpses, vendors, chests and houses need them | |
| 1.5 | **Context menus, old-style menus, text prompts** | Vendors, pets, crafting and guilds open through them | |
| 1.6 | **Item combat fields** in the templates and the converter: damage, speed, armour, hit points, strength requirement | Combat reads them; the converter drops them today | UOX3 `items/gear/` |

## Phase 2: combat

| Step | What | Why it comes here |
| --- | --- | --- |
| 2.1 | **War mode, swing timer, melee and archery**: hit chance, damage, armour, parry, durability | The core loop of the game |
| 2.2 | **Aggressor lists** | Notoriety, guards and loot rights rest on them |
| 2.3 | **Death, corpse, ghost, resurrection**; healer NPCs and shrines | Gives combat a result |
| 2.4 | **Bandages and healing** | Needed as soon as damage exists |
| 2.5 | **Combat events for Lua**: attack, hit, miss, damage, death, resurrect | Lets content change the rules |
| 2.6 | **Combat settings**: swing speed, damage rules, corpse decay | A shard owner expects to tune them |

## Phase 3: a world that fights back

| Step | What | Why it comes here | Data ready to import |
| --- | --- | --- | --- |
| 3.1 | **Pathfinding** and movement that checks items and mobiles. Done: the A* path search, `npc.walk_to`, and items that block; mobiles do not block yet | AI cannot chase without it | |
| 3.2 | **NPC AI**: melee, archer, mage, animal, fleeing; NPC memory of who attacked | The 29,000 spawned NPCs become content | UOX3 NPC tags dropped today (`NPCAI`, `FLEEAT`, `SPATTACK`), ModernUO `npc-speeds.json` |
| 3.3 | **Loot on corpses**, carving, fame and karma gain | Reward for the fight | UOX3 `carve.dfn` (102 tables) |
| 3.4 | **Notoriety**: criminal and murderer flags, name colours, murder counts | Makes PvP rule-bound | |
| 3.5 | **Region rules and guards**: guarded towns, no recall, no gate, no housing | Makes towns safe | ModernUO `regions.json` (typed regions), UOX3 `regions.dfn` (179 rule sets) |

## Phase 4: economy and magic

Vendors need only phase 1, so they can be built in parallel with phases 2 and 3.

| Step | What | Why it comes here | Data ready to import |
| --- | --- | --- | --- |
| 4.1 | **Vendors**: buy, sell, restock; skill trainers; bank checks, deposit and withdraw by speech | Gives gold a use | UOX3 `shoplist.dfn` (38 lists used by 86 NPCs); prices are converted already |
| 4.2 | **Secure trade** between players | Player economy | |
| 4.3 | **Spell casting and Magery**: spellbooks, reagents, scrolls, words of power, the 64 spells | Half of all characters cast | UOX3 `spells.dfn` (mana, reagents, delay, mantra) |
| 4.4 | **Recall, mark, gate, runebooks** | The way players travel; needs the region rules of 3.5 | |
| 4.5 | **Potions and alchemy effects** | Small once timed effects and spells exist | |

## Phase 5: professions

| Step | What | Why it comes here | Data ready to import |
| --- | --- | --- | --- |
| 5.1 | **Gathering**: mining, lumberjacking, fishing, with resource regions that run out and regrow | Feeds crafting | |
| 5.2 | **Crafting engine**, then each craft as data; repair | The peaceful play style and the player economy | UOX3 `create/` (618 recipes) |
| 5.3 | **Taming, pet commands, stables, mounts** | Needs AI (3.2), notoriety (3.4) and vendors (4.1) | UOX3 `TOTAME`, `CONTROLSLOTS`, `FOOD` tags |
| 5.4 | **The remaining active skills**: hiding, stealth, stealing, snooping, lore skills, bard skills, tracking | Each is small once 1.2 exists | |

## Phase 6: playing together

| Step | What | Why it comes here |
| --- | --- | --- |
| 6.1 | **Party** | Needs nothing else; a small shard lives on group play |
| 6.2 | **Guilds**, with war and alliance colours | Needs notoriety (3.4) |
| 6.3 | **Chat, bulletin boards, books, profile** | Independent, small |

## Phase 7: houses and boats

The largest chain of prerequisites, and what keeps players for months.

| Step | What | Why it comes here |
| --- | --- | --- |
| 7.1 | **Multis in movement and line of sight** | Everything below stands on it |
| 7.2 | **House placement, sign, owners, friends, bans** | |
| 7.3 | **Lockdowns, secure containers, decay** | |
| 7.4 | **Addons** (forges, looms, multi-piece furniture) | |
| 7.5 | **Player vendors** | Needs houses and vendors |
| 7.6 | **Boats** | Needs multis and speech keywords |

## Running a shard

These do not depend on the gameplay phases and are done when an operator needs them.

- Account bans, IP limits, login attempt limits, packet throttles.
- Staff tools: a props gump, an add menu, area commands, named `.go` locations.
- GM page queue, help and stuck menu, jail.
- Commands written in Lua.

## Later

After phase 7. Each of these needs most of what comes before.

- **Quests**: an engine for quests and escorts; about 35,000 lines in ModernUO.
- **Champion spawns, treasure maps, dungeon chests that refill, camps.**
- **Virtues and factions.**
- **The modern ruleset**: item properties and resistances, random magic loot, special moves,
  Necromancy, Chivalry, Bushido, Ninjitsu, Spellweaving, Mysticism, bulk orders, custom house
  design. These need packets a classic server never sends (extended status, damage numbers,
  the house designer).
- **Late expansion content**: imbuing, masteries, High Seas ships, vendor search, the store.

## Not planned

- Experience and levels, skill classes: only Sphere has them.
- Tournaments and ladders, seasonal events, veteran rewards: content for a live shard, better made
  by its owner in Lua.

## Data we can import

Moongate already imports UOX3 items, NPCs and spawns, and ModernUO decoration, spawns and
teleporters. The same converters can bring most of the rule data the phases above need:

| Data | Source | Phase |
| --- | --- | --- |
| Skill definitions: stat weights, gain curves, titles | UOX3 `skills.dfn`, ModernUO `skills.json` | 1.2 |
| Weapon and armour fields | UOX3 `items/gear/` | 1.6 |
| NPC behaviour tags and speeds | UOX3 NPC files, ModernUO `npc-speeds.json` | 3.2 |
| Carve tables | UOX3 `carve.dfn` | 3.3 |
| Region rules: guarded, recall, gate, housing | ModernUO `regions.json`, UOX3 `regions.dfn` | 3.5 |
| Shop lists | UOX3 `shoplist.dfn` | 4.1 |
| Spells | UOX3 `spells.dfn` | 4.3 |
| Craft recipes | UOX3 `create/` | 5.2 |
| Named locations for `.go` | UOX3 `location.dfn`, ModernUO `Locations/*.json` | Running a shard |

ModernUO and ServUO keep vendor lists, loot packs and craft definitions as C# classes, not data;
for those UOX3 is the source.

## Settings come with each system

Sphere has about 230 settings and UOX3 about 370; Moongate has about 60, nearly all for the
infrastructure. Each system above arrives with its own settings (regeneration rates, criminal
timers, vendor restock, decay times), documented in [Configuration](server-configuration.md).
