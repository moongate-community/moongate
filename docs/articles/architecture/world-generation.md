# World Generation

The world generation system populates the game world at server startup. Services load seed data from JSON files and generate world objects (decorations, doors, signs, teleporters, spawns).

## Services

### WorldGenerationStartupService

Coordinates world generation at startup. Invokes individual generator services in sequence.

### WorldGeneratorBuilderService

Provides a builder pattern for composing world generation pipelines. Generators register themselves and the builder orchestrates execution order.

### SeedDataService

Loads base seed datasets from JSON files in `moongate_data/`. Provides structured access to spawn definitions and teleporter definitions.

### DecorationDataService

Manages decoration placement data. Decorations are static world objects loaded from JSON files and placed at specific coordinates on specific maps.

### DoorDataService

Manages door definitions. Works with `DoorGeneratorBuilder` and `DefaultDoorGenerationMapSpecProvider` to generate doors based on map-specific specifications and facing rules.

### SignDataService

Manages sign definitions and placement data loaded from JSON files.

### LocationCatalogService

Provides a catalog of named locations (cities, dungeons, landmarks). Used by the `location` script module and teleport commands.

### SpawnsDataService

Manages mob spawn definitions. Spawn entries define mobile templates, locations, counts, and respawn intervals.

Current status: spawn definition import is in place, but the NPC template migration from ServUO/RunUO/ModernUO is still incomplete. Runtime spawners therefore currently materialize `generic_npc` as the spawned mobile fallback.

Spawn definitions currently support two runtime kinds through the JSON `type` field:

- `Spawner` - periodic respawn, processed on the normal spawn tick and gated by nearby players
- `ProximitySpawner` - trigger-on-enter behavior, using `homeRange` as the proximity radius

### TeleportersDataService

Manages teleporter definitions. Teleporters are point-to-point transitions between map coordinates, including cross-map transitions.

## File Loaders

The following file loaders support world generation:

- `DecorationDataLoader` - loads decoration placement JSON
- `DoorDataLoader` - loads door definition JSON
- `SignDataLoader` - loads sign placement JSON
- `LocationsDataLoader` - loads named location catalog JSON
- `SpawnsDataLoader` - loads spawn definition JSON
- `TeleportersDataLoader` - loads teleporter definition JSON

## Console Commands

World generation commands (under `worldgen` group):

- `create_spawners` - creates mob spawner entities from spawn definitions
- `initial_spawn` - forces an initial spawn pass across persisted spawner items
- `spawn_decorations` - places decoration objects in the world
- `spawn_doors` - generates doors from door specifications
- `spawn_signs` - places sign objects in the world

## Data Format

Seed data is stored as JSON files under `moongate_data/`. The actual runtime structure:

```
moongate_data/
├── data/
│   ├── decoration/         (.cfg files per map)
│   ├── locations/          (one .json per map facet)
│   ├── spawns/
│   │   └── shared/         (zone .json files per facet)
│   └── teleporters/
│       └── teleporters.json
├── templates/
│   ├── items/
│   │   ├── base/           (doors, signs, spawners, teleports, static)
│   │   └── modernuo/       (35+ item definition files)
│   ├── mobiles/
│   │   ├── npcs_humans.json
│   │   └── test_mob.json
│   └── startup/            (character creation rules)
```

### Teleporter Entry

```json
{
  "src": { "map": "Felucca", "loc": [512, 1559, 0] },
  "dst": { "map": "Felucca", "loc": [5394, 127, 0] },
  "back": true
}
```

`back: true` means the teleporter is bidirectional.

### Spawn Entry

```json
{
  "type": "Spawner",
  "guid": "163c83bd-e63d-4654-8c0e-231486150f45",
  "name": "Spawner (201)",
  "location": [6540, 872, 0],
  "map": "Felucca",
  "count": 1,
  "minDelay": "00:05:00",
  "maxDelay": "00:10:00",
  "homeRange": 5,
  "walkingRange": 5,
  "entries": [
    { "name": "InsaneDryad", "maxCount": 1, "probability": 100 }
  ]
}
```

At the moment the `entries[].name` values are imported and preserved from source data, but if no matching Moongate mobile template exists yet the runtime spawn path falls back to `generic_npc`.

`type` may be either `Spawner` or `ProximitySpawner`. `initial_spawn` can still force a spawn attempt for both kinds.

### Location Entry

```json
{
  "name": "Felucca",
  "categories": [
    {
      "name": "Dungeons",
      "categories": [
        {
          "name": "Covetous",
          "locations": [
            { "name": "Entrance", "location": [2499, 919, 0] },
            { "name": "Level 1", "location": [5456, 1863, 0] },
            { "name": "Level 2", "location": [5614, 1997, 0] }
          ]
        }
      ]
    }
  ]
}
```

Locations use a hierarchical structure: map, category, subcategory, named locations with coordinates.

### Mobile Template

```json
{
  "type": "mobile",
  "id": "orione",
  "body": "0x00C9",
  "skinHue": 779,
  "brain": "orion",
  "name": "Orione",
  "title": "a beautiful cat"
}
```

The `brain` field links to a Lua script at `scripts/ai/orion.lua`.

### Item Template

```json
{
  "type": "item",
  "id": "brick",
  "name": "Brick",
  "category": "Test Category",
  "itemId": "0x1F9E",
  "hue": "hue(2000:2200)",
  "goldValue": "dice(1d4+1)",
  "weight": 1,
  "scriptId": "brick",
  "isMovable": true
}
```

Template fields support expressions: `hue(min:max)` for random hue ranges, `dice(notation)` for randomized values.

## Runtime Flow

1. `FileLoaderService` triggers all registered file loaders during startup.
2. Data services receive parsed data from their respective loaders.
3. `WorldGenerationStartupService` coordinates the generation sequence.
4. Individual services place objects into the spatial world (`ISpatialWorldService`).
5. Generated entities are persisted through the standard repository/journal system.

---

**Previous**: [Background Jobs](background-jobs.md) | **Next**: [Network System](../networking/packets.md)
