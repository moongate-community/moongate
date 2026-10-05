# Shard data files

The shard data files describe the world the game server runs: its maps, starting
cities, skills, professions, races, containers, bodies, weather, regions,
messages and reputation titles. They are TOML files under `data/` in the server root. The server reads
them once at startup, in game and standalone modes, through the `IDataLoader<T>`
loaders of `Moongate.Server.Ultima`. A required file that is missing or invalid
stops the server at startup with an error that says what is wrong. The
[MOTD](motd.md) is optional: a missing file logs a warning and sends no welcome
message.

The files ship beside the `mgserver` binary, in `data/`; the repository copy
is `moongate_root/data/`. `mgctl` (or `mgserver --initialize-root`) copies
every missing file into `<root>/data` and never replaces one that exists, so a file
you edited survives an upgrade. After an upgrade, run `mgctl init` again to add new
files and compare your edited files with the shipped ones to pick up upstream
changes. See [What it creates](mgctl.md#what-it-creates).

C# code reads the loaded entries through `IDataLoaderService`:

```csharp
var cities = dataLoaderService.GetEntities<StartingCityContent>();
```

`GetEntities<T>()` returns the entries of one file, or of one directory for
regions and messages. It throws `InvalidOperationException` when no loader is
registered for `T`.

## Overview

Choose a file to open its examples, field reference and startup validation rules.

The loaders run in the order below; a loader that checks another file runs after
it.

| File | Content type | Loaded after / depends on | Used at runtime |
| --- | --- | --- | --- |
| <span id="maps"></span><span id="validation-at-startup"></span><span id="read-the-map-from-code"></span>[`maps.toml`](data-files/maps.md) | `MapContent` | first; its `weather` is checked by the regions loader | Yes, `IMapService` opens the client files of each map |
| <span id="starting-cities"></span><span id="validation-at-startup-1"></span>[`starting_cities.toml`](data-files/starting-cities.md) | `StartingCityContent` | maps | Yes, in the character list |
| <span id="moongates"></span>[`moongates.toml`](data-files/moongates.md) | `MoongateFacet` | maps | Yes, by `.decorate` and the moongate script |
| <span id="locations"></span>[`locations.toml`](data-files/locations.md) | `NamedLocation` | optional | Yes, by `.go` and its gump |
| <span id="jail"></span>[`jail.toml`](data-files/jail.md) | `JailFile` | optional; before book templates | Yes, by `.jail` and its gump |
| <span id="skills"></span><span id="validation-at-startup-2"></span>[`skills.toml`](data-files/skills.md) | `SkillContent` | starting cities | No |
| <span id="professions"></span><span id="validation-at-startup-3"></span>[`professions.toml`](data-files/professions.md) | `ProfessionContent` | skills (every starting skill must exist) | Yes, character creation |
| <span id="races"></span><span id="validation-at-startup-4"></span>[`races.toml`](data-files/races.md) | `RaceContent` | professions | Yes, character creation and mobile appearance |
| <span id="banned-names"></span><span id="validation-at-startup-5"></span>[`banned_names.toml`](data-files/banned-names.md) | `BannedNamesContent` | races | Yes, character name validation |
| <span id="containers"></span><span id="validation-at-startup-8"></span>[`containers.toml`](data-files/containers.md) | `ContainerContent` | banned names | Yes, backpack item placement |
| <span id="bodies"></span><span id="validation-at-startup-9"></span>[`bodies.toml`](data-files/bodies.md) | `BodyContent` | containers | No |
| <span id="weather"></span><span id="validation-at-startup-10"></span>[`weather.toml`](data-files/weather.md) | `WeatherContent` | bodies | Yes, `IWeatherService` rolls each profile's weather and sends it to the players |
| <span id="regions"></span><span id="areas"></span><span id="parents-and-overlaps"></span><span id="travel-zones"></span><span id="validation-at-startup-11"></span><span id="add-a-region"></span>[`regions/<map>.toml`](data-files/regions.md) | `RegionContent` | weather (every profile must exist), maps | Yes, `IRegionService` keeps each player's region for its music, weather, season and light; the guard, housing and travel rules are not read yet |
| <span id="messages"></span>[`messages/<lang>.toml`, `messages/<lang>/*.toml`](data-files/messages.md) | `MessageContent` | regions | Yes, through `ILocalizationService` |
| <span id="names"></span><span id="validation-at-startup-6"></span>[`names.toml`](data-files/names.md) | `NameList` | messages | Yes, through `INameService` |
| <span id="starting-items"></span><span id="validation-at-startup-7"></span>[`starting_items.toml`](data-files/starting-items.md) | `StartingItemSet` | names (after the item templates of `templates/items/`, which every item must name) | Yes, through `IStartingItemsService` |
| [`motd.toml`](motd.md) | `MotdLine` | after mobile templates and plugin variable registration | Yes, on every character entry; optional file |
| [`titles.toml`](data-files/titles.md) | `FameKarmaTitle` | after MOTD | Shown in the paperdoll title |
| [`templates/books/<name>.toml`](data-files/books.md) | `BookTemplate` | after item templates | Personalized scrolls; saved text is read through a gump |

"No" means the file is loaded and validated, but no game system reads it yet. A
mistake in such a file still stops the server.

## Value formats

Field names are snake_case. Some fields use value types with their own TOML form;
[TOML value types](toml-types.md) lists every accepted form and error:

| Type | Form | Example |
| --- | --- | --- |
| `Point2D` | quoted `"(x, y)"` | `size = "(7168, 4096)"` |
| `Point3D` | quoted `"(x, y, z)"` | `location = "(1602, 1591, 20)"` |
| `Serial` | bare integer, decimal or hex, or the same quoted | `cliloc = 1150168` |
| `Rectangle2D` | quoted `"(x1, y1)..(x2, y2)"` | `bounds = "(44, 65)..(186, 159)"` |
| `HueSpec` | bare integer, or a quoted hue or `"min-max"` range | `0x00BF`, `"0x03EA-0x0422"` |
| `DiceSpec` | bare integer, or a quoted dice expression | `karma = -2500`, `strength = "1d25+95"` |
| Any enum | quoted name, case and underscores ignored; flags joined by `\|` | `map = "felucca"`, `music = "mountn_a"` |

`Rectangle2D` writes two corners: the first included and the second excluded.
The legacy `"(x, y)+(width, height)"` format is still accepted when reading.

A value that does not match its form fails to parse and stops the server.

## Check your changes

Before restarting a server, load the repository files with the real loaders:

```sh
scripts/test.sh fast --filter 'FullyQualifiedName~RepositoryDataFiles'
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
- [Prepare a server root with mgctl](mgctl.md): how the data files get into a
  root.
