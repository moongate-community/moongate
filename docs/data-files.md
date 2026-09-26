# Shard data files

The shard data files describe the world the game server runs: its maps, starting
cities, skills, professions, races, containers, bodies, weather, regions and
messages. They are TOML files under `data/` in the server root. The server reads
them once at startup, in game and standalone modes, through the `IDataLoader<T>`
loaders of `Moongate.Server.Ultima`. A file that is missing or invalid stops the
server at startup with an error that says what is wrong.

The files ship beside the `Moongate.Server` binary, in `data/`; the repository copy
is `moongate_root/data/`. `mgboot` (or `Moongate.Server --initialize-root`) copies
every missing file into `<root>/data` and never replaces one that exists, so a file
you edited survives an upgrade. After an upgrade, run `mgboot` again to add new
files and compare your edited files with the shipped ones to pick up upstream
changes. See [What it creates](mgboot.md#what-it-creates).

C# code reads the loaded entries through `IDataLoaderService`:

```csharp
var cities = dataLoaderService.GetEntities<StartingCityContent>();
```

`GetEntities<T>()` returns the entries of one file, or of one directory for
regions and messages. It throws `InvalidOperationException` when no loader is
registered for `T`.

## Overview

The loaders run in the order below; a loader that checks another file runs after
it.

| File | Content type | Loaded after / depends on | Used at runtime |
| --- | --- | --- | --- |
| `maps.toml` | `MapContent` | first; its `weather` is checked by the regions loader | Yes, `IMapService` opens the client files of each map |
| `starting_cities.toml` | `StartingCityContent` | maps | Yes, in the character list |
| `skills.toml` | `SkillContent` | starting cities | No |
| `professions.toml` | `ProfessionContent` | skills (every starting skill must exist) | No |
| `races.toml` | `RaceContent` | professions | No |
| `banned_names.toml` | `BannedNamesContent` | races | No |
| `containers.toml` | `ContainerContent` | banned names | No |
| `bodies.toml` | `BodyContent` | containers | No |
| `weather.toml` | `WeatherContent` | bodies | No |
| `regions/<map>.toml` | `RegionContent` | weather (every profile must exist), maps | No |
| `messages/<lang>.toml` | `MessageContent` | regions | Yes, through `ILocalizationService` |

"No" means the file is loaded and validated, but no game system reads it yet. A
mistake in such a file still stops the server.

Field names are snake_case. Some fields use value types with their own TOML form:

| Type | Form | Example |
| --- | --- | --- |
| `Point2D` | quoted `"(x, y)"` | `size = "(7168, 4096)"` |
| `Point3D` | quoted `"(x, y, z)"` | `location = "(1602, 1591, 20)"` |
| `Serial` | bare integer, decimal or hex | `cliloc = 1150168` |
| `Rectangle2D` | quoted `"(x1, y1)..(x2, y2)"` | `bounds = "(44, 65)..(186, 159)"` |
| `HueSpec` | bare integer, or a quoted `"min-max"` range | `0x00BF`, `"0x03EA-0x0422"` |

`Rectangle2D` writes two corners: the first included and the second excluded.
The legacy `"(x, y)+(width, height)"` format is still accepted when reading.

`Point2D`, `Point3D` and `Serial` are described in
[Loading TOML templates](templates.md#registering-a-toml-converter). A value that
does not match its form fails to parse and stops the server.

## Maps

`maps.toml` lists the facets of the shard:

```toml
[[map]]
map = "felucca"
file_index = 0
name = "Felucca"
size = "(7168, 4096)"
rules = "FeluccaRules"
season = "desolation"
weather = "temperate"
```

| Field | Meaning |
| --- | --- |
| `map` | The map id sent to the client: `felucca`, `trammel`, `ilshenar`, `malas`, `tokuno` or `termur` (`MapType`). |
| `file_index` | The number of the client map files: `map{n}.mul` or `map{n}LegacyMUL.uop`, `staidx{n}.mul` and `statics{n}.mul`. |
| `name` | The name shown in logs and commands. |
| `size` | Width and height in tiles, a `Point2D`. |
| `rules` | The name of the rule set of the map. |
| `season` | The season of packet 0xBC: `spring`, `summer`, `fall`, `winter` or `desolation`. |
| `weather` | The profile of `weather.toml` used where no region covers a place. Defaults to `none`. |

The shipped file lists the six maps; Felucca and Trammel use `temperate`, the others
`none`.

At character creation the client reports which maps it has installed as
`ClientFlags` (`src/Moongate.Ultima/Types/ClientFlags.cs`); nothing compares them
with this file yet.

### Validation at startup

The server stops when:

- `maps.toml` does not exist;
- a map's `weather` is not a profile of `weather.toml`. The regions loader makes
  this check, since maps load before the weather profiles;
- the client directory lacks the map, `staidx` or `statics` file of a map's
  `file_index`. `IMapService` makes this check after the loaders; remove the map
  from `maps.toml` when the client has no files for it.

### Read the map from code

`IMapService` reads the terrain and statics of these maps, and `IMovementService` and
`ILineOfSightService` answer movement and sight questions on them; see
[Client files and world queries](world-queries.md).

## Starting cities

`starting_cities.toml` lists the cities a new character can start in. The game
server sends them with the character list (packet 0xA9) right after the game login.
The client sends back the index of the chosen city, so the order of the entries
matters.

```toml
[[starting_city]]
town = "New Haven"
description = "The Bountiful Harvest Inn"
location = "(3503, 2574, 14)"
map = "trammel"
cliloc = 1150168
```

| Field | Meaning |
| --- | --- |
| `town` | The city name the client shows. |
| `description` | The place in the city, such as an inn. |
| `location` | Where the character appears, a `Point3D`. |
| `map` | The map of `location`. |
| `cliloc` | The id of the localized description the client shows. |

### Validation at startup

The loader checks the limits of the character list packet, so a bad city stops the
server at startup instead of failing at each game login. It stops when:

- the file does not exist;
- it has no `[[starting_city]]` entries, or more than 255;
- a `town` or `description` is empty or blank, longer than 32 characters or not ASCII.

## Skills

`skills.toml` lists the 58 skills, with the ids the client uses (0 to 57):

```toml
[[skill]]
id = 0
name = "Alchemy"
title = "Alchemist"
profession_name = "Alchemy"
primary_stat = "int"
secondary_stat = "dex"
str_scale = 0.0
dex_scale = 5.0
int_scale = 5.0
str_gain = 0.0
dex_gain = 0.5
int_gain = 0.5
gain_factor = 1.0
```

| Field | Meaning |
| --- | --- |
| `id` | The skill id of the client (`SkillType`). |
| `name` | The skill name. |
| `title` | The title of a character whose best skill is this one. |
| `profession_name` | The name the profession files use for the skill. |
| `primary_stat`, `secondary_stat` | The stats the skill depends on: `str`, `dex` or `int`. |
| `str_scale`, `dex_scale`, `int_scale` | The chance, in percent, that a skill gain also raises that stat. |
| `str_gain`, `dex_gain`, `int_gain` | How much a skill gain favours that stat when a stat rises. |
| `gain_factor` | How fast the skill rises; 1.0 is normal. |

No skill gain system reads this file yet.

### Validation at startup

The server stops when:

- `skills.toml` does not exist or has no `[[skill]]` entries;
- the ids do not start at 0 and follow the order of the file without gaps: entry
  0 must have id 0, entry 1 id 1, and so on.

## Professions

`professions.toml` lists the professions a player can pick at character creation.
The client sends the chosen id (packet 0xF8) and leaves skills and stats empty; the
character is meant to get the stats and skills listed here. Id 0 is the "Advanced" choice, where the player picks
everything, so it is not listed.

```toml
[[profession]]
id = 1
name = "Warrior"
name_cliloc = 1061180
description_cliloc = 1061230
gump = 5577
str = 45
dex = 35
int = 10
skills = [
    { skill = "Tactics", value = 30 },
    { skill = "Healing", value = 30 },
    { skill = "Swordsmanship", value = 30 },
    { skill = "Anatomy", value = 30 },
]
```

| Field | Meaning |
| --- | --- |
| `id` | The profession id the client sends. |
| `name` | The profession name. |
| `name_cliloc`, `description_cliloc` | The ids of the localized name and description the client shows. |
| `gump` | The id of the gump image the client shows. |
| `str`, `dex`, `int` | The starting stats. |
| `skills` | The starting skills: the `SkillType` name without spaces (`"SpiritSpeak"`) and the value in whole points. |

The shipped professions give 90 stat points and 120 skill points, the totals of
`CharacterCreationRules` (`StatTotal = 90`, skill totals of 100 or 120). The loader
does not check these totals. Character creation does not read this file yet.

### Validation at startup

The server stops when:

- `professions.toml` does not exist or has no `[[profession]]` entries;
- an id is below 1 or is used twice;
- a starting skill is not in `skills.toml`.

## Races

`races.toml` lists the races a player can pick and, for each gender, the body and
the allowed hair and beard styles:

```toml
[[race]]
race = "human"
name = "Human"
skin_hues = ["0x03EA-0x0422"]
hair_hues = ["0x044E-0x047D"]

[race.male]
body = 400
hair = [0x203B, 0x203C, 0x203D, 0x2044, 0x2045, 0x2047, 0x2048, 0x2049, 0x204A]
beard = [0x203E, 0x203F, 0x2040, 0x2041, 0x204B, 0x204C, 0x204D]

[race.female]
body = 401
hair = [0x203B, 0x203C, 0x203D, 0x2044, 0x2045, 0x2046, 0x2047, 0x2049, 0x204A]
beard = []
```

| Field | Meaning |
| --- | --- |
| `race` | `human`, `elf` or `gargoyle` (`RaceType`). |
| `name` | The race name. |
| `skin_hues` | The allowed skin hues, as `HueSpec` values or ranges. Empty allows any hue. |
| `hair_hues` | The allowed hair and beard hues, in the same form. |
| `[race.male]`, `[race.female]` | One section per gender. |
| `body` | The body id of a living character of this race and gender. |
| `hair`, `beard` | The item ids of the allowed styles. No hair or beard (0) is always allowed and is not listed. |

A hue outside the allowed ones is meant to become the nearest allowed hue, and a
style not listed is dropped. `CharacterCreationRules` implements these checks, but
character creation does not call it yet.

### Validation at startup

The server stops when:

- `races.toml` does not exist or has no `[[race]]` entries;
- a race is listed twice;
- a race has no `[race.male]` or `[race.female]` section;
- a `body` is below 1;
- a hair or beard style is outside 1 to 0xFFFF.

## Banned names

`banned_names.toml` holds the words a player character name may not use. Matching
ignores case.

```toml
starts_with = [
    "admin",
    "counselor",
    "gm",
]

words = [
    "adept",
    "apprentice",
]
```

| Field | Meaning |
| --- | --- |
| `starts_with` | Words a name may not start with: `gm` also bans `GMaria`. |
| `words` | Words a name may not contain as a whole word: `mage` bans `Aria the Mage` but not `Magenta`. |

The loader trims every word and returns one `BannedNamesContent`. A banned name is
meant to become `Generic Player` (`CharacterCreationRules.ValidateName`), but
character creation does not call it yet.

### Validation at startup

The server stops when:

- `banned_names.toml` does not exist;
- a word is empty after trimming, which would ban every name.

## Containers

`containers.toml` says how the client shows each kind of container:

```toml
[[container]]
name = "default"
gump = 0x003C
bounds = "(44, 65)..(186, 159)"
drop_sound = 0x0048
default = true
items = []

[[container]]
name = "bag"
gump = 0x003D
bounds = "(29, 34)..(137, 128)"
drop_sound = 0x0048
items = [0x0E76, 0x2256, 0x2257]
```

| Field | Meaning |
| --- | --- |
| `name` | A label for people reading the file and for logs. Optional. |
| `gump` | The id of the gump the client opens. |
| `bounds` | The area of the gump where items can be placed, a `Rectangle2D`. |
| `drop_sound` | The sound of an item dropped in. Leave it out for none. |
| `items` | The item ids (graphics) of the containers that use this entry. |
| `default` | `true` on the one entry used for containers not listed. |

No container system reads this file yet.

### Validation at startup

The server stops when:

- `containers.toml` does not exist;
- not exactly one entry has `default = true`;
- a `gump` is below 1, or `bounds` is smaller than 1x1;
- an item id is listed by two entries.

## Bodies

`bodies.toml` says what kind of creature each body is. There is one list per kind:
`human`, `animal`, `monster`, `sea` and `equipment` (`BodyType`). Each entry is a
single id or an inclusive `"min-max"` range, always quoted:

```toml
human = [
    "183-186", "400-403", "605-608", "666-667", "694-695", "744-745",
    "750-751", "987-988", "990-991", "994", "1253",
]

sea = [
    "144-145", "150-151",
]
```

A body not listed counts as `Empty`. The loader returns one `BodyContent` per body id,
sorted by id, not one per entry. No system reads the body kinds yet.

### Validation at startup

The server stops when:

- `bodies.toml` does not exist;
- an entry is not a number or a `"min-max"` range, a range starts after its end, or
  an id is above 0xFFFF;
- a body id is listed under two kinds.

## Weather

`weather.toml` holds the weather profiles, named by climate. Maps and regions pick
one by name:

```toml
[[weather]]
name = "desert"
rain_chance = 1
snow_chance = 0
storm_chance = 0
snow_threshold = 0
min_temperature = 10
max_temperature = 30
cold_chance = 0
cold_temperature = 0
heat_chance = 80
heat_temperature = 35
rain_temperature_drop = 5
storm_temperature_drop = 10
```

| Field | Meaning |
| --- | --- |
| `name` | The name maps and regions use. |
| `rain_chance`, `snow_chance`, `storm_chance` | Chance (%) of that weather in the next hour, checked storm first, then snow, then rain. |
| `snow_threshold` | No snow at this temperature or above. |
| `min_temperature`, `max_temperature` | The range of a normal day. |
| `cold_chance`, `cold_temperature` | Chance (%) of a cold day, between `min_temperature` and `cold_temperature`. |
| `heat_chance`, `heat_temperature` | Chance (%) of a hot day, between `max_temperature` and `heat_temperature`. |
| `rain_temperature_drop`, `storm_temperature_drop` | How much rain or a storm lowers the temperature. |

The shipped profiles are `none`, `desert`, `tropical`, `temperate`, `highland`,
`stormy`, `mild`, `snowy`, `cool` and `rainy`. `none` never changes the weather.
The fields describe how weather is meant to work; no weather system reads them yet.

### Validation at startup

The server stops when:

- `weather.toml` does not exist or has no `[[weather]]` entries;
- a profile has no name, or a name is used twice (names are case-sensitive);
- a chance is outside 0 to 100;
- `min_temperature` is above `max_temperature`.

## Regions

`data/regions/` holds one file per map, named after the map: `felucca.toml`,
`trammel.toml`, `ilshenar.toml`, `malas.toml`, `tokuno.toml` and `termur.toml`. The
file name sets the map of every region in it; a region has no `map` field.

```toml
[[region]]
name = "The Heartwood"
type = "town"
priority = 50
areas = ["(6911, 255)..(7168, 512)"]
go_location = "(6984, 337, 0)"
entrance = "(535, 995, 0)"
music = "ElfCity"
weather = "none"
guarded = true
housing = false
```

| Field | Meaning | Default |
| --- | --- | --- |
| `name` | The region name. Leave it out for areas that only apply rules. | none |
| `type` | `base`, `town`, `dungeon`, `nohousing`, `guarded`, `jail` or `greenacres` (`RegionType`). | `base` |
| `priority` | Where regions overlap, the highest one applies. | 50 |
| `parent` | The name of the region this one is part of, on the same map. | none |
| `areas` | The rectangles of the region. | required |
| `go_location` | Where a "go to region" command takes a character, a `Point3D`. | none |
| `entrance` | The entrance of the town or dungeon, a `Point3D`. | none |
| `music` | The music track, a `MusicType` name such as `Britain1`. | none |
| `weather` | The profile of `weather.toml`. | `none` |
| `rune_name` | The name of a rune marked here. | none |
| `guarded` | Whether guards protect the region. | `false` |
| `housing` | Whether players may place houses. | `true` |
| `instant_logout` | Whether a character with no fight in progress leaves the world at once on logout. | `false` |
| `recall_in`, `recall_out`, `gate_in`, `gate_out`, `mark`, `teleport_in`, `teleport_out` | Whether those travel spells work into, out of or in the region. | `true` |

`MusicType` (`src/Moongate.Ultima/Types/MusicType.cs`) lists the track names; its
values follow `Music/Digital/Config.txt` of the client.

### Areas

Write each rectangle as `"(x1, y1)..(x2, y2)"`: two corners, not a position
and a size. The first corner is included and the second is excluded. For example,
`"(1330, 1991)..(1343, 2004)"` covers X from 1330 through 1342 and Y from 1991
through 2003. Both points use the same `(x, y)` notation as `Point2D`.

Without height limits, use strings directly:

```toml
areas = [
    "(1330, 1991)..(1343, 2004)",
    "(1494, 3767)..(1506, 3778)",
]
```

To limit height, use an inline table with `bounds` and optional `z1` and `z2`.
`z1` is included and `z2` is excluded. Either limit may be omitted independently;
when neither is present, the rectangle covers every height. Strings and tables
may be mixed in the same `areas` array.

```toml
areas = [
    { bounds = "(1416, 1498)..(1740, 1777)", z1 = -10, z2 = 128 },
    { bounds = "(1500, 1408)..(1546, 1498)", z1 = 0, z2 = 128 },
]
```

The loader also accepts the legacy `{ x1 = ..., y1 = ..., x2 = ..., y2 = ... }`
tables. Serialization always writes the new corner format, using a string when
there are no height limits and a `bounds` table otherwise.

### Parents and overlaps

A `parent` only records that a region is part of another, such as a building inside
Britain. Rules are not inherited when the file is read: every region writes out all
its rules, already combined with its parents', so each region can be read on its own.

Where regions overlap, the one with the highest `priority` gives the name, music,
guards, housing and logout rules. Travel works differently: a travel spell is
blocked when any region covering the place blocks it, whatever its priority.

### Travel zones

Travel zones are unnamed regions of priority 0 that only limit travel, such as the
Lost Lands of Felucca:

```toml
[[region]]
type = "base"
priority = 0
areas = ["(5120, 2304)..(6144, 4096)"]
weather = "none"
recall_in = false
recall_out = false
gate_in = false
gate_out = false
mark = false
teleport_in = true
teleport_out = true
```

No region system reads these files yet: the rules are loaded and validated only.

### Validation at startup

The server stops when:

- the `regions/` directory does not exist;
- a file name is not a map name (the match ignores case);
- a region has no areas;
- an area has malformed bounds, missing coordinates, mixed `bounds` and legacy
  coordinate fields, unknown fields, or non-integer height limits;
- the second corner is not above the first on both X and Y, or, when both height
  limits are set, `z2` is not above `z1`;
- a region's `weather` is not a profile of `weather.toml`;
- a name is used twice in the same file;
- a `parent` is not a region of the same file, or the parents loop back.

### Add a region

1. Open the file of the map, such as `data/regions/trammel.toml`.
2. Add a `[[region]]` with its `areas`, and every rule written out, including those
   of its parent. Omitted fields take the defaults of the table above.
3. Give it a `priority` above the regions it should override where they overlap.
4. Use a `weather` profile that exists in `weather.toml`.

## Messages

`data/messages/<lang>.toml` holds the texts the server sends, one file per
language. `ILocalizationService` reads them at runtime. See
[Localization](localization.md) for the format, the English fallback and the
validation rules.

## Check your changes

Before restarting a server, load the repository files with the real loaders:

```sh
dotnet test --filter RepositoryDataFiles
```

The test loads every file of `moongate_root/data`, in the server's order, and every
shipped language. It also checks a few known values, such as the number of skills
and regions, so update it when you add or remove entries.

For the files of a running server root, restart the server: the files are read only
at startup. A broken file stops the start, and the log shows the error.

## See also

- [Loading TOML templates](templates.md): the `IDataLoader<T>` contract, how
  loaders are registered and run, and the TOML converters.
- [Localization](localization.md): the message files and `ILocalizationService`.
- [Prepare a server root with mgboot](mgboot.md): how the data files get into a
  root.
