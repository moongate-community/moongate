# Migrate from UOX3

`mgctl convert uox` converts [UOX3](https://github.com/UOX3DevTeam/UOX3) `.dfn` item
definitions and loot lists into Moongate's `ItemTemplate` and `LootTemplate` TOML, and
UOX3 NPCs, NPC lists, spawn regions and name lists into `MobileTemplate`, NPC list and
spawn TOML and `names.toml`. Five more commands convert ModernUO's
[signs](#signs-of-modernuo), [teleporters](#teleporters-of-modernuo),
[named places](#named-places-of-modernuo), [treasure chests](#treasure-chests-of-modernuo) and
[spawners](#spawns-of-modernuo).
The shapes it writes are described in [Loading TOML templates](templates.md#the-template-shapes);
the server loads them at startup from `templates/`.

## Run it

From a source checkout:

```sh
dotnet run --project src/Moongate.Ctl -- convert uox \
  --source <file-or-directory> --destination <dir> [--loot-destination <dir>] \
  [--mobile-source <dfndata> --mobile-destination <dir> --names-destination <file>] \
  [--starting-items-destination <file>] [--scripts-source <js-dir>] \
  [--npc-lists-destination <dir> --spawns-destination <dir>]
```

Release archives and Docker images ship the same tool as `mgctl` (`/app/mgctl` in the image); see
[UOX3 content conversion](docker.md#uox3-content-conversion) for a `docker run`
example when there is no local .NET SDK.

`--source` is a single `.dfn` file or a directory scanned recursively for every
`.dfn` under it. `--destination` receives one `<name>.toml` per source `.dfn`, at the
same relative path, holding one `[[item]]` per block that has an `id=` of its own.
`--loot-destination` is optional; without it, `LOOTLIST` blocks are skipped. A bare
invocation prints the help and exits `0`; a missing required argument exits `1`.
The three mobile arguments go together (see [Mobiles and name lists](#mobiles-and-name-lists)).
`--scripts-source` is UOX3's `data/js` folder. With it, an item whose UOX3 script has a
Moongate Lua script gets its `script_id`: the script of the block's `script=`, else the one
`jse_objectassociations.scp` ([ENVOKE]) gives its graphic, looked up by number in
`jse_fileassociations.scp` ([SCRIPT_LIST]). Today `item/lights.js` becomes `light`
(`scripts/items/light.lua`); other scripts are left out. With or without it, what UOX3 calls food
(item type 14, on the block or on the one it gets its fields from) takes `script_id = "food"`
(`scripts/items/food.lua`), but for what UOX3 files under food and nobody eats as it is: the bowl of
flour (`0x0a1e_bowl_of_flour`) and the magic fish (`base_magic_fish`). What UOX3 calls a drink (item
type 105) takes `script_id = "drink"` (`scripts/items/drink.lua`) the same way, in place of UOX3's own
`pitchers.js`, but for the jar of honey (`0x09ec_jar_of_honey`).
A folder missing either file exits `2`.

`--npc-lists-destination` and `--spawns-destination` go together and need `--mobile-source`.
They convert the `[NPCLIST name]` blocks under `npc/` into `templates/npc_lists` (entries
`20|gorilla` keep their weight, `NPCLIST=trolls` becomes a nested list) and the
`[REGIONSPAWN n]` blocks under `spawn/` into `templates/spawns/<map>/`, one folder per map from
the region's own `WORLD=` (0 Felucca, 1 Trammel, 2 Ilshenar), else the source folder. A region's
`GET=` takes the fields of the region it names, its own winning, but never its map, NPCs, lists
or eras, as UOX3; a header defined twice keeps its last definition. An unweighted `NPCLIST=x`
inside a list brings x's entries in (UOX3 splices it); a weighted `n|NPCLIST=x` stays one pick.
Regions whose `ERAS=` leave out `tol` (the modern era; UOX3's default `lbr` keeps the same
ones) are skipped, reversed exclude corners and `MINTIME`/`MAXTIME` are put in order, and the
output is read back and checked as the server's loaders do.
`MINTIME`/`MAXTIME` stay in minutes as written (UOX3 itself truncates them to a byte). Regions
spawning only items, and NPCs or lists that do not resolve, are left out and counted.

Every block from every source file is read before any `get=` chain is resolved,
because a chain's target can live in another file: UOX3's own data keeps a sword's
facing variants beside its base definition but a shared `base_item` elsewhere. A
trailing `//comment` is stripped from every line first, as the UOX3 engine does;
real data glues one straight onto a block's opening brace (`{//approximately 1%`).
Text after the opening brace (`{ Random Hair`) is a label, not a line of the block.

## What maps

Verified against real UOX3 data:

| UOX3 | ItemTemplate | Note |
| --- | --- | --- |
| The block's own `id=` | `ItemId` | A list (`id=0x0c4f 0x0c50`, one picked at random in UOX3) keeps its first graphic; typos such as `0x0x04FC` and `0x15b6]` are forgiven. A block with no `id=` of its own converts only when it has one parent (below), with `item_id = 0`: the server's loader takes the parent's graphic |
| The block header, or `name=` when the header is a bare hex | `Id` | Run through `StringUtils.ToSnakeCase`; `name=` is free text ("pitcher of wine") |
| `name=` | `Name` | Carried as-is; UOX3 does not separate an identifier from display text |
| A single-target `get=` (else `getlbr=`) | `BaseId` | Only when that target itself converted; `get=a b`, a random alias with no `id=` of its own, converts nothing |
| `movable=1` or `3` / `2` | `Movable = true` / `false` | `0` or absent leaves it unset, so tiledata decides |
| `weight=` | `Weight` | Divided by 100: UOX3 weighs in hundredths of a stone |
| `amount=` | `Amount` | A fixed stack size |
| `pileable=` | `Stackable` | |
| `layer=` | `Layer` | The UOX3 layer number as a `LayerType` name |
| `layer=2` without `type=107` (shield) or `dir=` (light) | `two_handed_weapon = true` | As UOX3 decides at equip time. An unlit torch (`0x0F64`) has neither, so UOX3, and the converter, treat it as two-handed; the shipped templates leave it off |
| `value=buy sell` | `BuyPrice`, `SellPrice` | One number sets both |
| `decay=` | `Decays` | `1` is true, anything else false |
| `newbie` or `newbie=1` | `LootType = newbied` | |
| `custominttag=name value`, `customstringtag=name text` | `Tags` | Every line, so a block can set several |
| `color=` or `colour=` | `Hue` | A fixed value, not a range; unset writes no hue, so the server's loader takes the base template's |
| `weightmax=` | `MaxWeight` | UOX3 counts it in hundredths of a stone, as `weight=`: `weightmax=40000` is 400 stones, whole and rounded up |
| `visible=1`, `2` or `3` / `visible=0` | `Visibility = game_master` / `regular` | Hidden, magically invisible or GM hidden all keep the item from players; `visible=0` is written out so it overrides a hidden parent; absent leaves it unset |

Everything else has no home in `ItemTemplate` yet and is dropped: the combat stat
fields, `colorlist`, `script=`, and the multi and geometry fields. `BaseId` is a pointer only: the
converter does not flatten a parent's fields into its children; the loader will
resolve the chain once it exists. A parent block with no `id=` of its own, such as
`[base_coin]`, has its lines inlined into every child that `get=` it, as UOX3 does, with
the child's own lines winning. A coin so gets `weight = 0.02` and `stackable = true`, and
its `base_id` is the first ancestor that has an `id=` (`base_item`). An inherited `name=`
becomes the template's name but never part of its id: ids come from each block's own
lines only.

Numbers are read as UOX3 reads them (`stoi(value, nullptr, 0)`): hex with `0x` or
decimal, so `layer=0x08` is a ring. A tag with no value, such as `baseitem.dfn`'s bare
`decay=`, is ignored as UOX3 ignores it. A header defined twice keeps its **last**
definition, as UOX3 does, and source files are read in ordinal order so the result does
not depend on the filesystem. `[BESTSKILL n]` follows UOX3's own skill numbers, where
Imbuing is 55 and Mysticism 56 (the other way round from `SkillType`).

A block with no `id=` of its own but a single parent (`get=x`, else `getlbr=x`) that
converted is a template too, id = its header: UOX3's magic items, journals and other
variants (`[glacialstaff] get=0x0df1 name=glacial staff color=0x0480`) and single-target
aliases. It keeps `item_id = 0`, which the server's loader fills from its `base_id`, and
the chain is resolved over repeated passes, so a variant of a variant converts too. The
shipped data has 9665 item templates this way; loot entries and NPC equipment that name
such a block now resolve to it.

## Loot tables

UOX3's `[LOOTLIST name] { ... }` blocks are weighted loot tables. They convert into
`--loot-destination` (`templates/loots/`, next to `templates/items/`) as one
`<id>.toml` per table, named after the table's own Id rather than the source file:
real UOX3 data defines all 71 tables in one `lootlists.dfn`, and reviewing one has
no reason to load every other table alongside it. Each entry line is:

```text
weight|entry[,amount]
```

`weight` defaults to `1` when the `weight|` prefix is absent. `entry` is an item
header, resolved through the same map `get=` uses; `LOOTLIST=other`, a nested
weighted pick from another table; or the literal `blank`, a real weighted chance of
dropping nothing. `amount` is a single count or `min max` (a space, not a dash) and
maps onto `LootEntry.Amount`, a `RangeValueSpec<int>`.

| UOX3 | LootTemplate / LootEntry | Note |
| --- | --- | --- |
| The block header's name, after `LOOTLIST ` | `LootTemplate.Id` | Also through `StringUtils.ToSnakeCase`; real names are camelCase ("eartheleLoot") |
| An entry's `weight\|` prefix | `LootEntry.Weight` | Defaults to `1` |
| An item header entry | `LootEntry.ItemId` | Also fills `Comment` with that item's own `name=`, when it had one |
| `LOOTLIST=other` | `LootEntry.LootTemplateId` | Only when `other` itself converted |
| `blank` | Neither `ItemId` nor `LootTemplateId` set | A real, weighted chance of nothing |
| A trailing `,amount` | `LootEntry.Amount` | `RangeValueSpec<int>`; `min max` (space) becomes a range |

`ITEMLIST=`, UOX3's "spawn every entry" sibling, is a different mechanic, not a
weighted pick, and never appears in real `lootlists.dfn` data; it is dropped, as is
any entry the converter cannot resolve.

## Mobiles and name lists

With `--mobile-source` set to UOX3's `dfndata` folder, the same run converts, after the
items:

- every block under `npc/` (not `npc/npclists`) into a `[[mobile]]` template, one file
  per source file under `--mobile-destination`, id = the header in snake_case;
- the twenty `[RANDOMNAME n]` lists of `npc/namelists.dfn` into `--names-destination`.

It also reads `creatures/creatures.dfn` (sounds, and `MOVEMENT=WATER` or `BOTH` as
`movement`), `colors/colors.dfn` (colour lists) and
`../dictionaries/dictionary.ENG` (numeric names and titles). Equipment and loot are
resolved against the items and loot tables of the same run.

Every npc block is converted, so inheritance stays a `base_id` instead of being copied
in: `GET=x` and `GETLBR=x` become `base_id = "x"`. LBR is UOX3's default era; the other
era tags (`GETUO`, `GETAOS`, …) are ignored, although the blocks they name are converted
too.

| UOX3 | Template | Note |
| --- | --- | --- |
| `ID=0x0190` / `0x0191` | `race = "human"`, `gender` | elf 0x25D/0x25E and gargoyle 0x29A/0x29B likewise; the race gives the body |
| `ID=` other | `body` | |
| `RACE=0/1/2` | `race` human / elf / gargoyle | only when the block sets no non-human body (UOX3's `[giantrat]` has `RACE=2` on a rat); UOX3's other races are dropped |
| `NAME=`, `TITLE=` | `name`, `title` | a number is a dictionary id; `#` takes the line's `//` comment |
| `NAMELIST=n` | `name_list` | 1 `male`, 2 `female`, 3 `orc`, 5 `daemon`, …; `male`/`female` follow the gender, as UOX3 data has female NPCs on the male list |
| `STR`, `DEX`, `INT` | `strength`, … | `96 120` becomes the die `1d25+95`; one value a constant |
| `HPMAX` (else `HP`), `MANAMAX`, `STAMINAMAX` | `hits`, `mana`, `stamina` | dice |
| `DAMAGE`, `DEF` | `damage`, `armor` | dice |
| `RESISTFIRE/COLD/POISON/LIGHTNING`, `ELEMENTRESIST` | `resistances` | lightning is energy |
| skill tags (`MAGERY=500 700`) | `[mobile.skills]` | tenths to points, capped at 120; `MAGICRESISTANCE` is `resisting_spells` |
| `KARMA`, `FAME`, `GOLD` | `karma`, `fame`, `gold` | dice |
| `FLAG=INNOCENT/NEUTRAL/EVIL` | `notoriety` innocent / attackable / murderer | |
| `EQUIPITEM=x`, `EQUIPITEM=listobjectN` | `[[mobile.equipment]]` | a list gives every item of `[ITEMLIST N]`, one picked at random; its weights and `blank` lines are dropped; the hair and beard lists 13–15 are skipped. An item block with no `id=` of its own is followed: `getlbr=x` gives x, `get=a b` gives both |
| `COLOR`, `COLORLIST` after an `EQUIPITEM` | that entry's `hue` | a colour list only when it is one unbroken run of hues |
| `LOOT=list,n` | `loot` | the loot table, n times |
| `CUSTOMINTTAG`, `CUSTOMSTRINGTAG` | `tags` | |
| `[CREATURE id]` sounds | `[mobile.sounds]` | on the template whose own block sets the body |

`GET=m_guard f_guard`, a male and a female of the same race, becomes one `guard` with
`gender = "random"`, `name_list = "{gender}"` and the equipment only one of them wears
filtered by `gender`. Sounds the two set differently (humans die with a male or a female
scream) are left unset rather than giving a female the male sound; any other field set
differently takes the male value and is reported. An `f_` or `m_` NPC whose body is the
other gender's (UOX3's `[f_scribe]` has `ID=0x0190`) takes the prefix's gender, so its
pair still merges. Any other two-target `GET` (`[dragon] GET=graydragon reddragon`, a
random pick in UOX3) becomes a template whose `base_id` is the first target, and is
counted; a pair whose targets do not exist (`shepherd`) is skipped.

Two known mistakes in UOX3's item data are corrected as the blocks are read
(`UoxDataFixes`): `necro_sleeves` and `necro_leggings` name the leather gloves and a
leather tunic as their parents; they get the leather sleeves (`0x13cd`) and leggings
(`0x13cb`).

A mobile Moongate has a script for gets its `script_id`: a banker (`NPCAI=8`) takes `banker`
(`scripts/mobiles/banker.lua`), a town guard (`NPCAI=4`) takes `guard` (`scripts/mobiles/guard.lua`), and the undead of the graveyards (`skeleton`, `zombie`, `ghoul`, `headless`, `wraith`, `spectre`, `lich`) take
`monster` (`scripts/mobiles/monster.lua`);
the templates based on them take it through `base_id`.

Dropped, no home yet: the rest of AI and wandering (`NPCAI`, `NPCWANDER`, `FX*`, speeds, `FLEEAT`),
taming and bard skills (`TOTAME`, `CONTROLSLOTS`, `TOPROV`, `TOPEACE`), shops
(`SHOPKEEPER`, `SHOPLIST`), `PACKITEM`, `CARVE`, `FOOD`, `PRIV`, `SCRIPT` and the other
tags without a field. The run prints how often each kind of value was dropped.

The written mobiles are read back as well: ids unique, every `base_id`, equipment item,
loot table and name list resolves, and every template passes `MobileTemplate.Validate()`.

## Starting items

With `--starting-items-destination <file>` (which needs `--mobile-source`), the run also
converts `newbie/newbie.dfn` into one `starting_items.toml` of `[[set]]`s, after the
mobiles and against the same item ids.

| UOX3 | Set | Note |
| --- | --- | --- |
| `[BESTSKILL n]` | `skill` | the skill with id n; `[BESTSKILL X]` and empty sections are skipped |
| `[DEFAULT ALL]` | `common = true` | every character gets it |
| `[DEFAULT MALE]`, `[DEFAULT FEMALE]` | `race = "human"`, `gender` | UOX3 gives these to the human bodies only |
| `[DEFAULT ELF MALE]`, `[DEFAULT GARG FEMALE]`, … | `race`, `gender` | |
| `PACKITEM=item,amount,newbie` | `[[set.items]]`, `equip = false` | `amount` 1 is left unset; `newbie` 0 or 1 becomes `false` or `true` |
| `EQUIPITEM=item,hue,newbie` | `[[set.items]]`, `equip = true` | |

Items resolve as npc equipment does: `listobjectN` gives every item of `[ITEMLIST N]`,
an item block with no `id=` of its own is followed. Items that resolve to nothing are
dropped and counted. The file is read back and every item must exist. UOX3's own rules
(the three best skills, four with extended starting skills, and `STARTGOLD`) are not data
and are not converted. Set `ultima.starting_items.best_skills` in the server
configuration. The converter adds Moongate's own entries to the common set, for the items the
source has: 1000 gold coins first, in place of `STARTGOLD`, then three loaves of bread and a
pitcher of water last (see the [shipped file](data-files/starting-items.md)).

## Signs of ModernUO

The shop and world signs come from ModernUO's `signs.cfg`, the file its `[SignGen` places:

```sh
dotnet run --project src/Moongate.Ctl -- convert modernuo-signs \
  --source <ModernUO>/Distribution/Data/signs.cfg --destination moongate_root/templates/decorations
```

It writes one `signs.toml` per [decoration folder](templates.md#decorations) (`britannia` for the
signs of both Trammel and Felucca), replacing that of a previous run: a text of the client becomes
a `LocalizedSign` with `label_number`, a written one a `Sign` with `name`, and the signs of Luna and
Umbra keep the hue of their town. A line that is not a sign stops the run and names itself.
[`.decorate`](commands/decorate.md) places them.

## Teleporters of ModernUO

The world and dungeon teleporters come from ModernUO's `teleporters.json`, the file its `[TelGen`
places:

```sh
dotnet run --project src/Moongate.Ctl -- convert modernuo-teleporters \
  --source <ModernUO>/Distribution/Data/teleporters.json --destination moongate_root/templates/decorations
```

It writes one `teleporters.toml` per map folder of the
[decorations](templates.md#decorations) (`felucca`, `trammel`, `ilshenar`, `malas`, `tokuno`,
`termur`), replacing that of a previous run: one `Teleporter` block per destination, with
`map_dest` when the destination is on another map. An entry with `back` also gets the teleporter
from its destination to its source, and a later entry replaces an earlier one on the same cell
within 12 of height, as `[TelGen` does. An entry that is not a teleporter stops the run and names
itself, and nothing is written. [`.decorate`](commands/decorate.md) places them.

## Named places of ModernUO

The places of ModernUO's `[Go` gump, one JSON file per map with categories inside categories,
become [`locations.toml`](data-files/locations.md), which [`.go`](commands/go.md) lists and
travels to:

```sh
dotnet run --project src/Moongate.Ctl -- convert modernuo-locations \
  --source <ModernUO>/Distribution/Data/Locations --destination moongate_root/data/locations.toml
```

It reads `felucca.json`, `trammel.json`, `ilshenar.json`, `malas.json`, `tokuno.json` and
`termur.json`, those that are there, and writes one `[[location]]` per place with its map, its
categories joined by `/`, its name and its spot, replacing the file of a previous run. A place
without a name or without three numbers stops the run and names itself, and nothing is written.

## Treasure chests of ModernUO

The dungeon chests are entries of ModernUO's spawners, `TreasureChestLevel1` to `4`, beside the
creatures or alone. This command takes the chests only, on every map, and leaves the creatures to
the spawn converters:

```sh
dotnet run --project src/Moongate.Ctl -- convert modernuo-chests \
  --source <ModernUO>/Distribution/Data/Spawns --destination moongate_root/templates/spawns
```

It reads the `shared` and `post-uoml` eras and writes one `treasure_chests.toml` per map folder
that has chests, replacing that of a previous run: a
[spawn region of items](spawns.md#regions-of-items-treasure-chests) per spawner with chests,
which picks among the levels the spawner lists, with the spawner's delays, its home range as the
area and the caps of its chest entries together as `max`, the spawner's count at most. A chest of
another level is counted in the report and left out. With no chest at all it fails and writes nothing.

## Spawns of ModernUO

UOX3 has no spawns for Malas, Tokuno and TerMur. The `modernuo-spawns` command takes them from
[ModernUO](https://github.com/modernuo/ModernUO)'s spawners:

```sh
dotnet run --project src/Moongate.Ctl -- convert modernuo-spawns \
  --source <ModernUO>/Distribution/Data/Spawns --maps malas,tokuno,termur \
  --mobiles moongate_root/templates/mobiles --destination moongate_root/templates/spawns
```

It reads the `shared` and `post-uoml` eras of each map (the world of a modern client) and writes
the spawn regions into `<map>/modernuo_<file>.toml`, such as `malas/modernuo_doom.toml`, replacing
the `modernuo_` files a previous run wrote in the folders of the maps it converts; the other files
of the folder are left alone, and a map with nothing to write keeps its files. A region's id names the era, the file and
the spawner's index in it (`malas_modernuo_post_uoml_south_12`), so it stays the same when a later
run, with more templates, resolves more mobiles. Use it for maps UOX3 does not cover: on Felucca or
Trammel it would add ModernUO's spawns on top of UOX3's. A region takes the map its spawner
names, which is not always its folder's, and goes into the folder of that map, since the server
takes a region's map from its folder: the Yomotsu Mines and the Fan Dancer's Dojo lie in
ModernUO's `tokuno` folder and on the Malas map, so their regions are in
`malas/modernuo_yomutso_mines.toml` and `malas/modernuo_fan_dancers_dojo.toml`, with ids that
still start with `tokuno_`. A spawner becomes:

- `mobile_ids`: its entries, each ModernUO class found among the `--mobiles` templates. The command
  tries an alias of its table first (`Minter` is `banker`, `GreatHart` is `hart`, guildmasters are
  their trade's vendor), then the class in snake case (`GreatHart` is `great_hart`), then the class
  with the ids' underscores ignored, then UOX3's short name of an elemental (`DullCopperElemental`
  is `dullcopperele`). A class with no template is counted as `unknown mobile <Class>`, and a
  spawner with none left is skipped.
- `max`: its `count`. An entry whose `maxCount` is below the count, such as a single wandering
  healer among the beasts, becomes a region of its own with that cap (its id ends with the
  mobile); the other entries share one region with the rest of the count, less the share of the
  entries no template matches.
- `min_minutes` and `max_minutes`: its delays, a minute at least; `call` 1.
- `areas`: its `spawnBounds` when it has them, else the square of its home range around its spot,
  the spot alone when it has none, as in ModernUO.
- `z`: the top of its `spawnBounds`, else 16 above the spot, so a spawner in a cave does not spawn
  on the land over it. ModernUO looks at any height, so a wide outdoor spawner here keeps to the
  ground about its own level. Its `walkingRange` is left out: the NPCs' Lua script decides how
  they wander.

An unknown map name exits `2`, as do a missing `--source` and a `--mobiles` folder without
templates, which would otherwise replace the shipped files with nothing. The shipped Malas, Tokuno and TerMur
spawns come from this command; the classes it reports unknown are the NPCs still to write.

## Verifying the output

After writing every file, the converter reads all of it back from disk, as a real
loader would, and checks it: no two items or loot tables share an Id, and every
`BaseId`, `LootEntry.ItemId` and `LootEntry.LootTemplateId` names something that
was written. This catches a TOML round-trip going wrong and two headers, say
`Base-Item` and `base_item`, that collide only once both go through `ToSnakeCase`.
Any problem exits `1` and lists every one, prefixed `Verification failed:`; a clean
run prints `Verified <N> item(s) and <M> loot table(s) read back from disk`.
