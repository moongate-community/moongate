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
bank, take a teleporter or a moongate, open a treasure chest in a dungeon and a crate in a shop.
NPCs walk around what stands in their way. Players and NPCs fight with fists, swords or bows, die and come back as ghosts, train their skills and
are defended by the guards of the towns. What is missing is the rest of the gameplay: nothing can be
bought, cast, crafted or built yet.

Each step below carries its state: ✅ done, 🟡 partly done, ❌ not started.

## Priorities

The phases give the order of dependencies; this list says what is worked on, most urgent first.
A priority is closed when its steps are ✅ in the tables below.

| Priority | What | Steps | Why at this place |
| --- | --- | --- | --- |
| 1 | **Finish what Lua needs** (done) | 0.1, 0.3 and 0.4 are done; 0.5 and 0.6 are done for items, and what is left of them comes with skills, combat and timed effects | Every rule below is written against it, so a gap here is paid again in each system |
| 2 | **Skills and regeneration** | 1.1, 1.2 | The skill check is called by combat, magic, crafting, lockpicking and taming: nothing else unlocks as much |
| 3 | **What combat reads** | 1.3, 1.6, and the weight of 1.4 | Timed effects, the combat fields of the items, weight |
| 4 | **Combat and death** | 2.1 to 2.6 | The core loop: the 29,000 spawned NPCs are scenery until it exists |
| 5 | **A world that fights back** | 3.2 to 3.5, and mobiles that block in 3.1 | Makes the fight worth having and the towns safe |
| 6 | **Vendors** | 1.5, 4.1 | Needs only priority 2, so it can be taken between two combat steps; gives gold a use |
| 7 | **Magery and travel** | 4.3, 4.4, 4.5 | Half of all characters cast; recall and gate need the region rules of priority 5 |
| 8 | **Professions** | Phase 5 | Gathering, crafting, taming and the remaining skills, each small once priority 2 exists |
| 9 | **Playing together** | Phase 6, 4.2 | Party (6.1) needs nothing else and may come earlier, when players ask for it |
| 10 | **Houses and boats** | Phase 7 | The largest chain of prerequisites |

[Running a shard](#running-a-shard) has no place in this list: its items are done when an operator
needs them.

### World content that waits for a system

Some content is already in the data, placed or converted, and does nothing yet. It is not built on
its own: it comes with the priority that gives it its rule.

| Content | Waits for | Priority |
| --- | --- | --- |
| Dart boards (the training dummies and archery buttes work now) | Throwing skill (5.4) | 8 |
| Locks on the town containers (the dungeon treasure chests are locked and picked now) | Lock levels in the data | 2 |
| Traps of the chests, and the 490 traps placed in the dungeons | Damage (2.1) | 4 |
| Wands in the treasure chests | Spells (4.3) | 7 |
| Forges and anvils that craft | Crafting (5.2) | 8 |
| Chess and checker boards and bounty boards (the [bulletin boards](bulletin-boards.md) work) | Chat and boards (6.3) | 9 |
| The 621 addons of the shops and inns (anvils, ovens, beds, looms), skipped by `.decorate` | Addons (7.4) | 10 |
| The 87 spawners of quest characters, skipped by `.decorate` | Quests ([Later](#later)) | After 10 |

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
written. The [Lua API reference](https://moongate.sh/lua/) lists what Lua has today.

| Step | State | What | Why it comes here |
| --- | --- | --- | --- |
| 0.1 | ✅ | **Mobile state in Lua**: stats, skills, hits, mana, stamina, flags (hidden, frozen, war mode), hue, body, name; props on players; global props. The dead flag comes with death (2.3) | Every rule reads or changes them |
| 0.2 | ✅ | **World queries**: mobiles and items near a point, in sight; online players; region, height and line of sight lookups | Every AI, spell and area effect needs them |
| 0.3 | ✅ | **Item API**: create (on the ground, in a backpack, in a container), move into a container, equip, list the content, find by type, set hue and name | Loot, crafting, vendors, quests |
| 0.4 | ✅ | **Player input and output**: system message, text over any object, cliloc messages, the target cursor, a text prompt | Every skill and spell starts with a target |
| 0.5 | 🟡 | **Events that can refuse**: a handler stops or changes the default action. Done for items: `on_use`, `can_pick_up`, `can_drop`, `can_equip`, `can_insert`. Left: `check_skill`, `on_damage`, which come with skills and combat | Lets a script own a rule |
| 0.6 | 🟡 | **Timers kept by the object** and saved with the world. Done for items (`item.start_timer`, `on_timer`; the doors close with it). Left: timers on mobiles, which come with timed effects (1.3) | Timed effects need the same mechanism |
| 0.7 | 🟡 | **Region events**: enter and leave. Done for players (`player_region_changed`); not for NPCs | Guards, magic rules, music scripts, quests |

## Phase 1: a character that lives

| Step | State | What | Why it comes here | Data ready to import |
| --- | --- | --- | --- | --- |
| 1.1 | ✅ | **Regeneration** of hits, mana and stamina; hunger and thirst, food that is eaten and drinks that are drunk | Nothing depends on anything else; visible in the first fight | |
| 1.2 | ✅ | **Skill use, check and gain**; stat gain; caps and locks. A [skill](skills.md) is used from the client, checked by `skill.check`, gained under the skill cap and the total cap and locked up, down or locked from the skill window; a successful check raises strength, dexterity and intelligence as ModernUO's classic rule, to 100 each and 225 in all, with their own locks. Hiding is the first skill script; the others come with their systems | The progression of the game; every later system calls the skill check | UOX3 `skills.dfn` (61 skills: stat weights, gain curves), ModernUO `skills.json` |
| 1.3 | ❌ | **Timed effects**: one mechanism for poison, curses, blessings, polymorph, hiding; the buff bar shows them | Magic, potions and combat all need it | |
| 1.4 | ✅ | **Containers on the ground**, with item and weight limits; weight and overloading: a container on the ground opens, items go in and out of it, 125 at most and up to its limit of stones; a player carries 40 stones and 3.5 a point of strength, and moving overloaded or running costs stamina | Corpses, vendors, chests and houses need them | |
| 1.5 | 🟡 | **Context menus and old-style menus**. Done: the text prompt (`prompt.ask`), and [context menus](context-menus.md) with the server's entries and the ones a Lua script adds | Vendors, pets, crafting and guilds open through them | |
| 1.6 | ✅ | **Item combat fields** in the templates and the converter: damage, speed, armour, hit points, strength requirement | Combat reads them; the converter reads them from UOX3 now | UOX3 `items/gear/` |

## Phase 2: combat

| Step | State | What | Why it comes here |
| --- | --- | --- | --- |
| 2.1 | 🟡 | **War mode, swing timer, melee and archery**: hit chance, damage, armour, parry, durability. Done: a [fight](combat.md) with fists or a weapon, the swing timer, the hit by skill, the damage and the armor of an NPC or of what a player wears, and archery with a bow or a crossbow, for a player and for an NPC. Left: parry, durability | The core loop of the game |
| 2.2 | ❌ | **Aggressor lists**. Today only the [murder report](death.md#murder-counts) keeps who attacked an innocent, for `aggressor_seconds`; no attack timeouts and no loot rights | Notoriety, guards and loot rights rest on them |
| 2.3 | 🟡 | **Death, corpse, ghost, resurrection**; healer NPCs and shrines. Done: an NPC or a player dies by a fight, `.kill` or `mobile.kill` and leaves its [corpse](death.md) with what it carried; a player stays as a [ghost](death.md#death-of-a-player) and comes back at an ankh or at a healer, by `.resurrect` or `mobile.resurrect`. Left: bones, bounties and the places of the evil healers | Gives combat a result |
| 2.4 | 🟡 | **Bandages and healing**. Done: the clean [bandage](scripting/shipped-scripts.md#bandagelua) heals a player or a creature and raises a ghost, with the formulas of ModernUO's classic Healing. Left: poison and bleeding to cure, and pets to raise | Needed as soon as damage exists |
| 2.5 | ❌ | **Combat events for Lua**: attack, hit, miss, damage, death, resurrect | Lets content change the rules |
| 2.6 | ❌ | **Combat settings**: swing speed, damage rules, corpse decay | A shard owner expects to tune them |

## Phase 3: a world that fights back

| Step | State | What | Why it comes here | Data ready to import |
| --- | --- | --- | --- | --- |
| 3.1 | 🟡 | **Pathfinding** and movement that checks items and mobiles. Done: the A* path search, `npc.walk_to`, and items that block; mobiles do not block yet | AI cannot chase without it | |
| 3.2 | 🟡 | **NPC AI**: melee, archer, mage, animal, fleeing; NPC memory of who attacked. Done: archers (an NPC with a bow shoots, and so does a player), creatures that run when hurt. Left: mages. Today a Lua script on a tick, wandering, `monster.lua`, the melee AI, on the 200 or so evil and chaotic templates, which go for the players and the townsfolk and which the guards kill, and the animals (`animal.lua`, `scared_animal.lua`) on the shared `common/creature.lua` | The 29,000 spawned NPCs become content | UOX3 NPC tags dropped today (`NPCAI` values, `SPATTACK`), ModernUO `npc-speeds.json` |
| 3.3 | 🟡 | **Loot on corpses**, carving, fame and karma gain. Loot is rolled into the backpack at spawn and lies in the corpse of a dead NPC; no carving, fame or karma | Reward for the fight | UOX3 `carve.dfn` (102 tables) |
| 3.4 | 🟡 | **Notoriety**: criminal and murderer flags, name colours, murder counts. Done: the criminal flag with its timer, [murder counts](death.md#murder-counts) with the report gump of the victim, a red name from five murders, as ModernUO's. Left: other crimes, bounties; the name colour of an NPC is the one of its mobile template | Makes PvP rule-bound | |
| 3.5 | 🟡 | **Region rules and guards**: guarded towns, no recall, no gate, no housing. Done: guarded regions, the guards that arrest a criminal or a monster, called by saying "guards" or standing in the town, and archer guards in Ilshenar and Malas. Left: the guards do not punish a player, and no rules for recall, gate or housing | Makes towns safe | ModernUO `regions.json` (typed regions), UOX3 `regions.dfn` (179 rule sets) |

## Phase 4: economy and magic

Vendors need only phase 1, so they can be built in parallel with phases 2 and 3.

| Step | State | What | Why it comes here | Data ready to import |
| --- | --- | --- | --- | --- |
| 4.1 | 🟡 | **Vendors**: buy, sell, restock; skill trainers. Done: the bank box, balance, deposit and withdraw by speech, bank checks, gold handed to the banker, [buying from and selling to vendors](vendors.md) with ModernUO's shops, restock and resale; [skill trainers](skills.md#trainers) for vendors and healers; the [guildmasters](skills.md#guildmasters) with their guilds | Gives gold a use | UOX3 `shoplist.dfn` (38 lists used by 86 NPCs); prices are converted already |
| 4.2 | ❌ | **Secure trade** between players | Player economy | |
| 4.3 | ❌ | **Spell casting and Magery**: spellbooks, reagents, scrolls, words of power, the 64 spells | Half of all characters cast | UOX3 `spells.dfn` (mana, reagents, delay, mantra) |
| 4.4 | ❌ | **Recall, mark, gate, runebooks** | The way players travel; needs the region rules of 3.5 | |
| 4.5 | ❌ | **Potions and alchemy effects** | Small once timed effects and spells exist | |

## Phase 5: professions

| Step | State | What | Why it comes here | Data ready to import |
| --- | --- | --- | --- | --- |
| 5.1 | 🟡 | **Gathering**: mining, lumberjacking, fishing, with resource regions that run out and regrow. Done: fishing with a pole, lumberjacking with an axe, and the areas that run out and come back. Left: mining, the special catches of fishing, the wood types and boards | Feeds crafting | |
| 5.2 | ❌ | **Crafting engine**, then each craft as data; repair | The peaceful play style and the player economy | UOX3 `create/` (618 recipes) |
| 5.3 | ❌ | **Taming, pet commands, stables, mounts** | Needs AI (3.2), notoriety (3.4) and vendors (4.1) | UOX3 `TOTAME`, `CONTROLSLOTS`, `FOOD` tags |
| 5.4 | 🟡 | **The remaining active skills**: hiding, stealth, stealing, snooping, lore skills, bard skills, tracking. Done: hiding, [stealth](scripting/shipped-scripts.md#stealthlua), [snooping](scripting/shipped-scripts.md#snoopinglua), detect hidden, anatomy, evaluating intelligence and forensic evaluation ([the lore skills](scripting/shipped-scripts.md#the-lore-skills)). Left: stealing, tracking, arms lore, item ID, taste ID, the bard skills | Each is small once 1.2 exists | |

## Phase 6: playing together

| Step | State | What | Why it comes here |
| --- | --- | --- | --- |
| 6.1 | ❌ | **Party** | Needs nothing else; a small shard lives on group play |
| 6.2 | ❌ | **Guilds**, with war and alliance colours | Needs notoriety (3.4) |
| 6.3 | 🟡 | **Chat, bulletin boards, books, profile** | Independent, small. [Bulletin boards](bulletin-boards.md) are done |

## Phase 7: houses and boats

The largest chain of prerequisites, and what keeps players for months.

| Step | State | What | Why it comes here |
| --- | --- | --- | --- |
| 7.1 | ❌ | **Multis in movement and line of sight** | Everything below stands on it |
| 7.2 | ❌ | **House placement, sign, owners, friends, bans** | |
| 7.3 | ❌ | **Lockdowns, secure containers, decay** | |
| 7.4 | ❌ | **Addons** (forges, looms, multi-piece furniture) | |
| 7.5 | ❌ | **Player vendors** | Needs houses and vendors |
| 7.6 | ❌ | **Boats** | Needs multis and speech keywords |

## Running a shard

These do not depend on the gameplay phases and are done when an operator needs them.

- Account bans, IP limits, login attempt limits, packet throttles.
- Staff tools: a props gump, an add menu, area commands. The named places of
  [`.go`](commands/go.md) and their gump are done.
- The [jail](jail.md), the [help and stuck menu](help.md) and the GM page queue are done.
- Commands written in Lua.

## Later

After phase 7. Each of these needs most of what comes before.

- **Quests**: an engine for quests and escorts; about 35,000 lines in ModernUO.
- **Champion spawns, treasure maps, camps.** The dungeon chests respawn and the town containers
  fill up already; the dungeon chests are locked already, and their traps and the locks of the town containers come with priorities 4 and 2.
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

Moongate already imports UOX3 items, NPCs and spawns, and ModernUO decoration, spawns, named places and
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

ModernUO and ServUO keep vendor lists, loot packs and craft definitions as C# classes, not data;
for those UOX3 is the source.

## Settings come with each system

Sphere has about 230 settings and UOX3 about 370; Moongate has about 60, nearly all for the
infrastructure. Each system above arrives with its own settings (regeneration rates, criminal
timers, vendor restock, decay times), documented in [Configuration](server-configuration.md).
