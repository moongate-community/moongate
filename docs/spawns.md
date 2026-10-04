# NPC spawns

The world fills itself with NPCs from spawn regions, as UOX3's `[REGIONSPAWN]`: every region keeps
up to its `max` NPCs alive, spawning a few at a time, and spawns new ones when some are removed or
killed. The shipped data is UOX3's, converted by [`mgctl convert uox`](uox3-migration.md): 2778 regions on
Felucca, Trammel and Ilshenar, for up to about 25,000 NPCs, picking from 446 NPC lists. UOX3 has no
spawns for New Haven, so `spawns/trammel/town_new_haven.toml` adds its 57 spawn points from
ModernUO: the vendors, the bankers, the townsfolk and the town animals, 99 NPCs in all. Malas,
Tokuno and TerMur, which UOX3 has no spawns for either, take theirs from ModernUO's spawners through
[`mgctl convert modernuo-spawns`](uox3-migration.md#spawns-of-modernuo): 1,206 regions, about 4,200
NPCs, in the `modernuo_*.toml` files of their folders; the spawners whose creatures have no template
yet are left out.

## Where the data is

| Folder | What it holds |
| --- | --- |
| `templates/npc_lists/` | Lists of mobile templates a region picks from, such as the animals of a forest |
| `templates/spawns/<map>/` | The spawn regions; the folder is the map (`felucca`, `trammel`, `ilshenar`, ...) |
| `templates/mobiles/` | The [mobile templates](templates.md) the lists and regions name |

Both load at startup, after the mobile templates. A mistake stops the server with the file and the
reason:

- a list or a region without an `id`, or a duplicate id;
- an unknown mobile template, list or item template;
- a region with both NPCs and `item_ids`;
- a list without entries or looping through its nested lists;
- a list entry with both `mobile_id` and `npc_list_id`, with neither, or with a `weight` below 1;
- a region with nothing to spawn, or without an area;
- a `max` or `call` below 1, `min_minutes` below 0 or above `max_minutes`;
- reversed corners in `areas` or `exclude`;
- a folder that is not a map, or whose name is not in lower case.

## NPC lists

```toml
[[npc_list]]
id = "jungle"
entries = [{ mobile_id = "gorilla", weight = 20 }, { npc_list_id = "all_trolls", weight = 7 }]
```

An entry names a mobile template (`mobile_id`) or another list (`npc_list_id`), and is picked in
proportion to its `weight` (1 by default). An entry naming a list picks again from that list, with
that list's weights. In the example a gorilla comes 20 times in 27, a troll 7 times in 27.

## Spawn regions

```toml
[[spawn]]
id = "felucca_0"                      # unique across every map
name = "The Hammer And Anvil"         # shown in the staff messages and .spawns
mobile_ids = ["weaponsmith"]          # mobile templates, weight 1 each
npc_list_ids = []                     # lists whose entries join the pool with their weights
max = 1                               # NPCs alive at once
min_minutes = 480                     # the next spawn comes min_minutes to max_minutes later
max_minutes = 600
call = 1                              # NPCs spawned at a time
areas = [{ x1 = 1422, y1 = 1547, x2 = 1426, y2 = 1550 }]   # both corners included
exclude = []                          # parts of the areas where nothing spawns
only_outside = false                  # true: never under a roof
# pref_z = 18                         # how high above the ground a spot may be (18 when unset)
# z = 36                              # the highest a spot may be, instead of ground + pref_z
```

A region picks each NPC from one pool: its `mobile_ids`, weight 1 each, and the entries of its
`npc_list_ids` with their weights.

The shipped files also carry `map = "..."`, written by the converters. The server takes the map
from the folder and ignores the key, so a `map` that disagrees with its folder changes nothing.

## How spawning works

1. **The check.** Every 10 seconds (the `npc_spawn` timer) each region whose time has come and
   which has fewer than `max` NPCs alive spawns up to `call` more. It then waits a random time
   between `min_minutes` and `max_minutes`. A region already at `max` spawns nothing and waits
   again.
2. **Gradual fill.** At startup the first spawn of each region comes at a random time within its
   `min_minutes`, at most 10 minutes, and fills the region to its `max` at once: an empty world is
   full about 10 minutes after the start, without every NPC arriving in the same moment. A region
   that finds a spot for only some of its NPCs counts as filled, and brings the rest by `call`.
   Later spawns follow `call` and the times again. `[ultima.spawns] initial_fill = false` keeps UOX3's way, where
   the first spawn also brings only `call` NPCs, and a region with a `call` of 1 can take hours to
   fill. The NPCs are saved with the world, so after a restart the regions are already full.
3. **The spot.** For each NPC the region picks the template first, then tries up to 100 random
   cells of its `areas`, outside `exclude`:
   - a land mobile stands on the highest surface at most `pref_z` above the ground (or at most `z`,
     but never below the ground of the cell, since nothing lies under the land), with room for a
     person above it;
   - a mobile whose template has `movement = "water"` goes on water (not blood, swamp or a
     trough), and one with `both` on land, else on water;
   - with `only_outside`, a spot under a roof (a static more than 10 above it) is refused.

   A pick with no spot is skipped, and the others of the same `call` still spawn. A region that
   placed nothing tries again a minute later.
4. **The NPC.** It spawns dressed, with its loot, and runs its Lua `on_spawn`. It keeps its region
   in the prop `spawn.region` and the area it came from in `spawn.x1`, `spawn.y1`, `spawn.x2`,
   `spawn.y2`, saved with it from the start.
5. **The count.** The live NPCs of a region are the NPCs whose `spawn.region` is its id, counted at
   every check. An NPC removed with `.remove`, or killed, frees its slot, and a restart keeps the
   count.

Regions on a map the server does not load are skipped, and the startup log says how many.

## Regions of items: treasure chests

A region with `item_ids` instead of `mobile_ids` and `npc_list_ids` spawns items on the ground. It
follows the same check, times, `max`, `call`, areas and spot rules as a region of NPCs; each spawn
picks one of its item templates at random. An item always takes a land spot; `only_outside`, `pref_z`
and `z` apply as for NPCs.

```toml
[[spawn]]
id = "felucca_chest_shared_shame_12"
name = "Treasure chest level 3"
item_ids = ["treasure_chest_level_3"]
max = 1
min_minutes = 5
max_minutes = 10
z = 36
[[spawn.areas]]
x1 = 5398
y1 = 18
x2 = 5402
y2 = 22
```

The item is made with what its template puts inside: its `gold` in piles and each table of its
`loot` rolled once (see [the item fields](templates.md#the-template-shapes)). It keeps its region in
the prop `spawn.region` and counts for the region while it lies on the ground: when it decays, is
deleted or is taken from the ground, its slot is free and the region spawns a new one at its next
time. The staff messages and the world progress are about NPCs only; `.spawns` lists a region of
items with its live items, and `.initial_spawn` fills it too.

The shipped `treasure_chests.toml` of Felucca, Trammel and Ilshenar hold the dungeon chests of
ModernUO's spawners, written by
[`mgctl convert modernuo-chests`](uox3-migration.md#treasure-chests-of-modernuo): 399 regions for
up to 633 chests. The four templates, `treasure_chest_level_1` to `treasure_chest_level_4` in
`templates/items/treasure_chests.toml`, are ModernUO's `TreasureChestLevel1` to `4`:

| Level | Chest | Gold | What it may hold |
| --- | --- | --- | --- |
| 1 | Wooden | 30-129 | 1-3 gems of a kind, a weapon, an armour, clothing, jewellery |
| 2 | Metal | 70-169 | Up to two piles of 1-2 reagents, 1-8 scrolls of the first five circles, a potion, 1-6 gems |
| 3 | Metal bound | 180-419 | One or two piles of 1-9 reagents; up to two each of 1-12 scrolls of the first six circles, potions, 1-9 gems, clothing and jewellery; magic items |
| 4 | Golden | 200-599 | 1-4 blank scrolls; up to three each of 12 reagents, 16 scrolls, potions and 12 gems; up to two of clothing and jewellery; magic items |

Each pile of gems, reagents or scrolls and each potion comes one time in two; a weapon, an armour,
clothing or jewellery a little less, since their own tables also give nothing at times; a magic
item one time in five, rolled four times at level 3 and six at level 4. They come from the tables of `templates/loots/treasure_chests.toml`, which give the
piles of ModernUO out of the gems, reagents and scrolls of the other loot tables. ModernUO's wands
are not there: no wand template exists yet.

A player within two tiles opens a chest with a double click, takes what is inside and may put
items into it, a pile onto a pile of the same kind; the players around see what comes and goes. A region never puts a chest on the cell
of another spawned chest. A chest
cannot be picked up, by all but the staff, and decays 45 minutes after it was made, opened or not,
with what is left inside; the region then makes a new one 5 to 10 minutes later. The containers of the towns work another way: they stay and
[fill up when opened](scripting.md#item-scripts). Chests have no lock and no trap yet, each level has one
look, and the time to decay is fixed, where ModernUO picks 15 to 74 minutes.

## Water and amphibious NPCs

A mobile template's `movement` says where its NPCs move: `land` (the default), `water` or `both`.
The converter takes it from UOX3's `MOVEMENT` in `creatures.dfn`: the dolphin, the seahorse, the
kraken and the sea serpents move in water; the walrus, the alligator and the water elementals on
both. They spawn as described above, and `npc.step` makes them swim. `.spawn` refuses a water
template on a spot with no water, since it could never move there.

## NPC behaviour

A spawned NPC runs its template's Lua script, if any. The shipped
[`wander.lua`](scripting.md#mobile-scripts) keeps a spawned NPC inside its home area, stays put
when its area is a single cell, and walks it back when it is outside.

## For the staff

- Game masters and administrators in the world get one message after each check that spawned
  something: `Spawn: The Hammer And Anvil (Felucca): 1 NPCs` for one region, or
  `Spawn: 12 NPCs in 9 regions: Yew Woods 3, ... and 4 more` naming at most five, followed by how
  full the world is: `- world 3120/29064 (10%)`, the live NPCs of every region against their
  `max`. The same line goes to the server log at Information level, so the gradual fill can be
  followed there; the detail of each spawn is at Debug level.
- [`.spawns`](commands/spawns.md) lists the regions where you stand, with their live NPCs, their
  `max` and the minutes to their next spawn, or `no spot found, retrying` for a region that could
  not place anything.
- [`.initial_spawn`](commands/initial_spawn.md) (administrators) fills every region to its `max`
  at the next check, whatever `initial_fill` says, for a new world or after a large cleanup.
- [`.spawn`](commands/spawn.md) and [`.remove`](commands/remove.md) place and remove single NPCs by
  hand; a removed spawned NPC is replaced at its region's next spawn.

The messages are in the server language (ids 30074-30081, 30088 and 30092, see
[Localization](localization.md)).

## See also

- [Loading TOML templates](templates.md): the mobile template fields, `movement` included
- [Migrate from UOX3](uox3-migration.md): `--npc-lists-destination` and `--spawns-destination`, and the
  chests of ModernUO
- [Writing Lua scripts](scripting.md)
