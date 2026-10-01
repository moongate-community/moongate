# NPC spawns

The world fills itself with NPCs from spawn regions, as UOX3's `[REGIONSPAWN]`: every region keeps
up to its `max` NPCs alive, spawning a few at a time, and spawns new ones when some are removed or
killed. The shipped data is UOX3's, converted by [`mg-uoxconv`](uox3-migration.md): 2778 regions on
Felucca, Trammel and Ilshenar, for up to about 25,000 NPCs, picking from 446 NPC lists.

## Where the data is

| Folder | What it holds |
| --- | --- |
| `templates/npc_lists/` | Lists of mobile templates a region picks from, such as the animals of a forest |
| `templates/spawns/<map>/` | The spawn regions; the folder is the map (`felucca`, `trammel`, `ilshenar`, ...) |
| `templates/mobiles/` | The [mobile templates](templates.md) the lists and regions name |

Both load at startup, after the mobile templates. A mistake stops the server with the file and the
reason: an unknown mobile template or list, a list without entries or looping through its nested
lists, a region with nothing to spawn, a `max` or `call` below 1, `min_minutes` above
`max_minutes`, a region without an area, reversed corners, a duplicate id, or a folder that is not a
map.

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

## How spawning works

1. **The check.** Every 10 seconds (the `npc_spawn` timer) each region whose time has come and
   which has fewer than `max` NPCs alive spawns up to `call` more. It then waits a random time
   between `min_minutes` and `max_minutes`. A region already at `max` spawns nothing and waits
   again.
2. **Gradual fill.** At startup the first spawn of each region comes at a random time within its
   `min_minutes`, at most 10 minutes. An empty world fills over the first minutes instead of all
   at once. The NPCs are saved with the world, so after a restart the regions are already full.
3. **The spot.** For each NPC the region picks the template first, then tries up to 100 random
   cells of its `areas`, outside `exclude`:
   - a land mobile stands on the highest surface at most `pref_z` above the ground (or at most `z`),
     with room for a person above it;
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
  full the world is: `- world 3120/24805 (12%)`, the live NPCs of every region against their
  `max`. The same line goes to the server log at Information level, so the gradual fill can be
  followed there; the detail of each spawn is at Debug level.
- [`.spawns`](commands/spawns.md) lists the regions where you stand, with their live NPCs, their
  `max` and the minutes to their next spawn, or `no spot found, retrying` for a region that could
  not place anything.
- [`.spawn`](commands/spawn.md) and [`.remove`](commands/remove.md) place and remove single NPCs by
  hand; a removed spawned NPC is replaced at its region's next spawn.

The messages are in the server language (ids 30074-30081, see [Localization](localization.md)).

## See also

- [Loading TOML templates](templates.md): the mobile template fields, `movement` included
- [Migrate from UOX3](uox3-migration.md): `--npc-lists-destination` and `--spawns-destination`
- [Writing Lua scripts](scripting.md)
