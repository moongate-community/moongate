# Spatial System

The spatial system in Moongate v2 manages entity positioning, visibility, and broadcasting across the game world. The design is inspired by Minecraft's chunk-based world partitioning: the map is divided into fixed-size sectors (similar to Minecraft chunks), entities are indexed by sector, and the server only loads/syncs sectors within a configurable radius around active players.

## Sector Model

The world is divided into a grid of **32x32 tile sectors** (constant `MapSectorConsts.SectorSize = 32`). Sector coordinates are computed from tile coordinates using bit-shift division for performance:

```
sectorX = tileX >> 5   (equivalent to tileX / 32)
sectorY = tileY >> 5
```

Each `MapSector` stores entities in concurrent dictionaries organized by type:

- All entities (items + mobiles)
- Mobiles only (players and NPCs)
- Items only
- Players only (subset of mobiles, used for broadcasting)

`SpatialMapIndex` holds the sector grid for a single map facet. `SpatialEntityIndex` is the top-level index that tracks all maps and maintains a reverse lookup from entity serial to current sector location.

## Configuration

Spatial behavior is controlled by `MoongateSpatialConfig` in `moongate.json`:

```json
{
  "Spatial": {
    "LazySectorItemLoadEnabled": true,
    "SectorWarmupRadius": 1,
    "SectorEnterSyncRadius": 3,
    "LazySectorEntityLoadRadius": 3,
    "SectorUpdateBroadcastRadius": 3,
    "LightWorldStartUtc": "1997-09-01T00:00:00Z",
    "LightSecondsPerUoMinute": 5.0
  }
}
```

| Setting | Default | Description |
|---------|---------|-------------|
| `LazySectorItemLoadEnabled` | true | Enable on-demand sector loading from persistence |
| `SectorWarmupRadius` | 1 | Radius around player login sector to preload (3x3 area) |
| `SectorEnterSyncRadius` | 3 | Radius for sending item/mobile snapshots on sector entry (7x7 area) |
| `LazySectorEntityLoadRadius` | 3 | Radius for lazy-loading entities when a sector is accessed (7x7 area) |
| `SectorUpdateBroadcastRadius` | 3 | Radius for live update broadcasts (7x7 area) |

The radius values define a square area: radius 1 means 3x3 sectors, radius 3 means 7x7 sectors centered on the player.

## Lazy Loading and Warmup

Like Minecraft chunks that load around the player and unload when distant, Moongate sectors are loaded on demand:

1. **Login warmup**: when a player logs in, `WarmupAroundSectorAsync` preloads sectors within `SectorWarmupRadius` around the spawn point.
2. **Movement warmup**: when a player crosses a sector boundary, adjacent sectors are warmed via `WarmupSectorsFireAndForget`.
3. **Lazy load**: when any code accesses a sector that has not been loaded yet, `EnsureSingleSectorLoadedAsync` loads persistent NPCs and ground items from the repository.

Load tasks are deduplicated: if a sector is already being loaded, subsequent requests reuse the in-flight task instead of triggering a second load. This prevents thundering herd issues when multiple systems access the same sector concurrently.

## Entity Tracking

`SpatialEntityIndex` maintains three core data structures under a shared lock:

- `_mapIndices` - map ID to SpatialMapIndex (sector grid per map)
- `_entityLocations` - entity serial to SpatialEntityLocation (reverse lookup: which map/sector is this entity in?)
- `_loadedSectors` - set of sector coordinates that have been loaded from persistence

Key operations:

- `AddOrUpdateItem` / `AddOrUpdateMobile` - indexes an entity into the correct sector
- `MoveItem` / `MoveMobile` - moves an entity, detecting sector boundary crossings
- `RemoveEntity` - removes an entity from all tracking structures
- `GetNearbyItems` / `GetNearbyMobiles` - range queries using sector iteration

## Broadcasting

The spatial system provides three broadcasting strategies, from fine to coarse:

### Tile-based broadcast

```csharp
await _spatialWorldService.BroadcastToPlayersAsync(
    packet, mapId, location, range: 18, excludeSessionId: session.SessionId
);
```

Finds players within a tile range from a point and enqueues the packet to each.

### Sector-based broadcast

```csharp
await _spatialWorldService.BroadcastToPlayersInSectorRangeAsync(
    packet, mapId, centerSectorX, centerSectorY, sectorRadius: 2
);
```

Iterates sectors in a square area around a center sector. Sessions are deduplicated to prevent double delivery in overlapping sectors.

### Update radius broadcast

```csharp
await _spatialWorldService.BroadcastToPlayersInUpdateRadiusAsync(
    packet, mapId, location, excludeSessionId: session.SessionId
);
```

Convenience method for live entity updates. Converts a tile location to sector coordinates and broadcasts using `SectorUpdateBroadcastRadius` (default 3, so 7x7 sectors).

## Events

The spatial system publishes game events when entity positions change:

| Event | Trigger |
|-------|---------|
| `ItemAddedInSectorEvent` | Item first indexed into a sector |
| `MobileAddedInSectorEvent` | Mobile first indexed into a sector |
| `MobileAddedInWorldEvent` | Persistent NPC loaded during sector warmup |
| `MobileSectorChangedEvent` | Mobile crosses a sector boundary |
| `MobilePositionChangedEvent` | Mobile position updated (any movement) |
| `PlayerEnteredRegionEvent` | Player enters a named region |
| `PlayerExitedRegionEvent` | Player exits a named region |

## Region Resolution

`SpatialRegionResolver` provides named region lookup (cities, dungeons, etc.) using a lazy sector-based index. Regions are mapped to the sectors they overlap, so looking up "which region is this point in?" is O(1) per candidate check rather than scanning all regions.

Regions support parent/child hierarchy and are resolved with deterministic ordering: `Priority` descending, then `ChildLevel` for ties.

## Movement Flow

When a mobile moves:

1. `OnMobileMoved` is called with old and new positions.
2. The entity index updates the entity location and checks for sector boundary crossing.
3. If the sector changed, `MobileSectorChangedEvent` is published.
4. For players crossing sector boundaries, warmup is triggered for adjacent sectors.
5. Region entry/exit is detected and `PlayerEnteredRegionEvent` / `PlayerExitedRegionEvent` are published.

## Statistics

`GetStats()` returns `SectorSystemStats` for monitoring:

- Total active sectors across all maps
- Total entities in the spatial index
- Maximum entities in any single sector
- Average entities per sector

Per-sector stats are available via `SectorStats` (map, coordinates, entity/mobile/item/player counts, bounds).

## Performance Notes

- Sector coordinate calculation uses bit-shift (`>> 5`) instead of division for speed.
- Concurrent dictionaries in `MapSector` allow lock-free reads for entity collections.
- Sector-based spatial hashing provides O(1) sector lookup.
- Range queries iterate only the relevant sector square, not all entities.
- The benchmark suite (`SpatialWorldServiceBenchmark`) tests with 500-2000 mobiles for add, query, and cross-sector move operations.

---

**Previous**: [World Generation](world-generation.md) | **Next**: [Event System](events.md)
