# Client files and world queries

The game server reads the Ultima Online client files it needs for gameplay: the tile
properties (`tiledata.mul`), the maps with their statics, and the multi layouts of
houses and boats. On top of them it answers two questions every game system asks:
can a mover take this step, and can this point see that one. C# code reaches all of
this through five services of `Moongate.Server.Ultima`, registered by the Ultima
plugin in game and standalone modes.

| Service | Answers | Call from |
| --- | --- | --- |
| `ITileDataService` | Name, flags, weight, height of a land or item graphic | Any thread |
| `IMapService` | Terrain and statics of a map cell | The game loop |
| `IMultiService` | The components of a house, boat or other multi | Any thread |
| `IMovementService` | Whether a step is allowed and the Z it lands at | The game loop |
| `ILineOfSightService` | Whether one point sees another | The game loop |

The map readers share their buffers and one file handle per map, so the map,
movement and line of sight services belong on the game loop. Tile data and multis are
copied into memory and only read afterwards.

## What is read at startup

| Priority | Service | Reads | Stops the server when |
| --- | --- | --- | --- |
| -10 | `IUltimaDataService` | `ultima.ultima_path`, the client version, `tiledata.mul` | the directory or `tiledata.mul` is missing |
| -5 | `IDataLoaderService` | the [shard data files](data-files.md), including `data/maps.toml` | a data file is missing or invalid |
| -4 | `IMapService` | `map{n}.mul` or `map{n}LegacyMUL.uop`, `staidx{n}.mul` and `statics{n}.mul` for every map of `maps.toml`, `n` being its `file_index` | a map lacks one of its files |
| -4 | `IMultiService` | every multi from `MultiCollection.uop`, or from `multi.idx` with `multi.mul` when the client has no UOP file | neither format is there, or no multi can be read |

The error messages and their fixes are in
[Common startup problems](getting-started.md#common-startup-problems). Set the client
directory with [`ultima.ultima_path`](server-configuration.md#settings-and-validation).
A plugin service that reads maps or multis starts after -4; see the
[startup priorities](plugins.md#what-register-may-do).

## Tile data

```csharp
var backpack = tileDataService.GetItem(0x0E75);   // "backpack", weight 3, Container | Wearable
var water = tileDataService.GetLand(0x00A8);      // "water", Impassable | Wet
```

| Member | Meaning |
| --- | --- |
| `LandCount`, `ItemCount` | The number of land and item graphics; the item count depends on the client version |
| `GetLand(id)`, `GetItem(id)` | The tile; `ArgumentOutOfRangeException` for an id out of range |
| `TryGetLand(id, out tile)`, `TryGetItem(id, out tile)` | The tile, or false for an id out of range |

`LandTile` has `Id`, `Name`, `Flags` and `TextureId`. `ItemTile` has `Id`, `Name`,
`Flags`, `Weight` (255 means it cannot be picked up), `Height`, `StandHeight` (half the
height for a `Bridge` such as a stair), `Layer`, `Quantity` and `Animation`. The first
call copies the tables into read-only arrays, so every later lookup is an array index.

## Maps

```csharp
var land = mapService.GetLand(MapType.Felucca, 1602, 1591);        // cobblestones, Z 20
var statics = mapService.GetStatics(MapType.Felucca, 1400, 1500);  // willow tree and leaves, Z 10
var name = tileDataService.GetItem(statics[0].Id).Name;
```

| Member | Meaning |
| --- | --- |
| `Maps` | The maps loaded, in the order of `maps.toml` |
| `Contains(map, x, y)` | Whether the map is loaded and the cell lies inside it |
| `GetLand(map, x, y)` | The land graphic id and Z of the cell |
| `GetStatics(map, x, y)` | Each static object's item graphic id, Z and hue, in file order; empty when there are none |

A map that is not loaded throws `KeyNotFoundException`; a cell outside the map throws
`ArgumentOutOfRangeException`. Blocks of 8x8 cells are read the first time a cell in
them is asked for and kept in a bounded cache.

## Multis

```csharp
var house = multiService.GetMulti(0x64);   // 148 components, from (-3, -3) to (4, 4), height 36
```

| Member | Meaning |
| --- | --- |
| `Count` | The number of multis loaded |
| `GetMulti(id)` | The multi; `KeyNotFoundException` when the client has none with this id |
| `TryGetMulti(id, out multi)` | The multi, or false |

A `MultiDefinition` has `Id` (its item graphic is `0x4000` plus the id), `Min` and
`Max`, the smallest and largest X and Y offsets, `Height`, the highest Z offset, and
`Components`. Each `MultiComponent` has an `ItemId`, an `Offset` from the multi's
centre as a `Point3D`, and `Visible`: hidden components, such as the centre marker,
only take up space.

## Movement

`IMovementService` uses the rules of ModernUO, which match the client's own
prediction: a mover is 16 units tall, climbs at most 2 units per step, stands on half
the height of a bridge such as a stair, and a diagonal step needs both cells beside
it free.

```csharp
if (movementService.CheckMovement(MapType.Felucca, from, DirectionType.East,
        MovementAbilityType.Walk, out var newZ))
{
    // the step is allowed; the mover lands at newZ
}
```

- `MovementAbilityType.Walk` moves over land, statics and surfaces that are not
  water; `Swim` enters water; `Walk | Swim` does both.
- Only the low three bits of the direction count, so `DirectionType.Running` is
  ignored.
- A step that leaves the map, or starts outside it, returns false with `newZ` equal
  to the starting Z; a blocked step returns false with the Z the mover stands at.
- `GetAverageZ(map, x, y)` gives the terrain height at the centre of a cell, from its
  four corners.

## Line of sight

`ILineOfSightService` walks POL's integer 3D line between two points and tests each
point with ModernUO's rules. Pass both points at eye or target height; a mobile's eye
is its Z plus 14:

```csharp
var visible = lineOfSightService.HasLineOfSight(MapType.Felucca,
    new Point3D(1602, 1591, 20 + 14), new Point3D(1610, 1591, 20 + 14));
```

- Statics flagged `Window` or `NoShoot` and terrain block the line; a blocker at the
  target's cell and height does not.
- Points farther than [`line_of_sight.max_distance`](server-configuration.md#settings-and-validation)
  (default 25) along X or Y are never in sight, and neither is a point outside the map.
- A map that is not loaded throws `KeyNotFoundException`.
- The service allocates nothing: each cell's statics are read once, even when the
  line crosses it several times.

## Not included yet

World items, mobiles and placed multis are not part of the movement and line of sight
checks, because the world does not hold them yet. See
[Implementation status](implementation-status.md).
