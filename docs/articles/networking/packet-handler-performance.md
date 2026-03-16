# Packet Handler Performance Guide

This guide explains how to write packet handlers and event listeners that don't block the game loop.

## Architecture Overview

```
Client -> NetworkService -> PacketDispatchService -> IPacketListener (your handler)
                                  |
                           Game Loop Thread
                           (synchronous dispatch)
```

`PacketDispatchService` runs on the **game loop thread** and dispatches packets synchronously. If your handler blocks, the entire game loop stalls - no other packets are processed, no ticks advance.

The outgoing path is separate: `IOutgoingPacketQueue` is a thread-safe queue drained by a dedicated send thread. Enqueuing packets is always non-blocking.

## Rule 1: Never Block the Game Loop

`PacketDispatchService.NotifyListenerSafe` calls your handler synchronously:

```csharp
var task = listener.HandlePacketAsync(session, packet);

if (!task.IsCompletedSuccessfully)
{
    task.GetAwaiter().GetResult(); // blocks game loop until handler completes
}
```

If your `HandleCoreAsync` awaits something slow (DB query, network call), the game loop blocks for the entire duration.

### Bad: Blocking event publish

```csharp
protected override Task<bool> HandleCoreAsync(GameSession session, IGameNetworkPacket packet)
{
    // This publishes an event and BLOCKS until ALL listeners complete.
    // If any listener does a DB query, the game loop stalls.
    _gameEventBusService
        .PublishAsync(gameEvent)
        .AsTask()
        .GetAwaiter()
        .GetResult();

    return Task.FromResult(true);
}
```

### Good: Fire-and-forget event publish

```csharp
protected override Task<bool> HandleCoreAsync(GameSession session, IGameNetworkPacket packet)
{
    // Validate, build response packets, enqueue them...

    // Publish event without blocking the game loop
    PublishEventFireAndForget(new SomeGameEvent(session.SessionId, ...));

    return Task.FromResult(true);
}

private void PublishEventFireAndForget<TEvent>(TEvent gameEvent) where TEvent : IGameEvent
{
    var task = _gameEventBusService.PublishAsync(gameEvent);

    if (!task.IsCompletedSuccessfully)
    {
        task.AsTask().ContinueWith(
            static t => Log.ForContext<MyHandler>()
                           .Error(t.Exception, "Event publish failed for {EventType}", typeof(TEvent).Name),
            TaskContinuationOptions.OnlyOnFaulted
        );
    }
}
```

**Why this works:**
- `IsCompletedSuccessfully` - if the event bus completes synchronously (most single-listener events do), no allocation happens.
- `ContinueWith(OnlyOnFaulted)` - only allocates a continuation if the task actually fails.
- The game loop returns immediately; event listeners run asynchronously.

## Rule 2: Enqueue Packets, Don't Send Directly

Always use the outgoing packet queue. Never write to the socket directly from a handler.

```csharp
// Good: non-blocking enqueue
Enqueue(session, new MoveAcceptPacket(character, sequence));

// Also good: enqueue by session ID
_outgoingPacketQueue.Enqueue(session.SessionId, new SomePacket(...));
```

The queue is drained by `OutboundPacketSender` on a separate thread. Enqueuing is O(1) and lock-free.

## Rule 3: Avoid Lazy-Loading in the Hot Path

Spatial queries like `GetNearbyMobiles()` and `GetNearbyItems()` trigger `EnsureSectorLoaded()` which synchronously loads sector data from persistence if the sector hasn't been warmed up yet.

### Bad: Triggering lazy load during movement validation

```csharp
// This can block for 200ms+ if the sector isn't loaded
var nearby = _spatialWorldService.GetNearbyMobiles(destination, 1, mapId);
```

### Good: Pre-warm sectors proactively

Sectors are warmed at login (`WarmupAroundSectorAsync`) and on sector change (`WarmupSectorsFireAndForget`). If you need spatial queries in a hot path, ensure the sectors are already warmed:

```csharp
// SpatialWorldService automatically warms sectors when:
// 1. Player logs in (SectorWarmupRadius around spawn)
// 2. Player crosses sector boundary (SectorWarmupRadius around new sector)
//
// If your handler runs AFTER these events, sectors will be loaded.
// Don't add new EnsureSectorLoaded calls in packet handlers.
```

## Rule 4: Delta Sync on Sector Change

When a player crosses a sector boundary, don't re-sync all sectors in the radius - only sync the NEW sectors that weren't visible before.

### Bad: Full re-sync every sector crossing

```csharp
// With radius 3, this sends packets for 49 sectors (7x7) on EVERY sector crossing
// Even though 42 of those sectors were already sent last time
for (var x = center.SectorX - radius; x <= center.SectorX + radius; x++)
{
    for (var y = center.SectorY - radius; y <= center.SectorY + radius; y++)
    {
        SyncSectorForPlayer(session, mapId, x, y);
    }
}
```

### Good: Only sync delta sectors

```csharp
for (var sectorX = newCenter.SectorX - radius; sectorX <= newCenter.SectorX + radius; sectorX++)
{
    for (var sectorY = newCenter.SectorY - radius; sectorY <= newCenter.SectorY + radius; sectorY++)
    {
        // Skip sectors that were already in the old sync radius
        if (oldSector is not null &&
            sectorX >= oldCenter.SectorX - radius &&
            sectorX <= oldCenter.SectorX + radius &&
            sectorY >= oldCenter.SectorY - radius &&
            sectorY <= oldCenter.SectorY + radius)
        {
            continue;
        }

        SyncSingleSectorForPlayer(sessionId, mobileEntity, mapId, sectorX, sectorY, z);
    }
}
```

With radius 3, moving one sector in any direction syncs ~13 new sectors instead of 49 (~73% reduction).

## Rule 5: Use Spatial Helpers for Broadcast

Don't iterate all sessions manually. Use the spatial broadcast methods:

```csharp
// Broadcasts to all players within sector radius of a location
await _spatialWorldService.BroadcastToPlayersInUpdateRadiusAsync(
    packet, mapId, location, excludeSessionId: session.SessionId
);

// Broadcasts to players within a specific range (tile-based)
await _spatialWorldService.BroadcastToPlayersAsync(
    packet, mapId, location, range: 18, excludeSessionId: session.SessionId
);
```

These methods resolve sessions from the spatial index instead of scanning all connected players.

## Cross-Map Teleport Sync

We hit a concrete regression on player teleports across maps:

- the client stayed on the old facet until the player moved
- `GumpMenuSelectionPacket` and `MoveRequestPacket` could log slow ticks even with one connected player
- cross-map teleport into a cold destination could stall on lazy sector loading and repeated snapshot lookups

### Root Cause

The issue came from three things compounding:

- teleport-triggered work was allowed to drift behind inbound packet processing instead of being applied immediately in the player sync path
- `MobileHandler` re-queried `GetSectorByLocation()` for every snapshot sector during teleport bootstrap, which amplified lazy loading on cold destinations
- `SpatialWorldService.GetPlayersInRange()` previously depended on nearby-mobile spatial queries, so even simple player broadcast resolution could trigger cold-sector loads

### Fix

The runtime path was tightened so cross-map teleport behaves like an immediate mini re-sync:

- player map-change packets are sent before old-range cleanup work
- `MobileHandler` reuses already-loaded sectors for snapshot sync instead of repeatedly resolving them through spatial lazy-load
- `SpatialWorldService.GetPlayersInRange()` now resolves online player sessions directly from runtime sessions, filtering by `mapId` and distance, without forcing spatial loads
- a dedicated benchmark was added for the cold cross-map case

### Benchmarks

Benchmark names:

```bash
TeleportMapChangeBenchmark.HandleCrossMapTeleport_ColdDestination
TeleportMapChangeBenchmark.HandleSameMapTeleport_ColdDestination_WithSelfRefresh
```

Run it with:

```bash
dotnet run --project benchmarks/Moongate.Benchmarks/Moongate.Benchmarks.csproj -c Release -- --filter "*TeleportMapChangeBenchmark*" --job Dry
```

Latest measured dry-run values on Apple M4 Max / .NET 10:

- cross-map cold destination
  - median: `2.696 ms`
  - mean: `3.964 ms`
  - max first-iteration outlier: `17.800 ms`
  - allocated: `1.77 MB`
- same-map cold destination with self refresh
  - median: `1.684 ms`
  - mean: `2.536 ms`
  - max first-iteration outlier: `11.828 ms`
  - allocated: `1.17 MB`

The first-iteration spikes are expected for cold paths. The steady-state samples clustered around `2.6-2.8 ms` for cross-map and `1.64-1.72 ms` for same-map.

## Login World Sync

We also hit a concrete login stall on cold sectors:

- `LoginCharacterPacket` originally waited for `CharacterHandler`
- `CharacterHandler` published `PlayerCharacterLoggedInEvent`
- login world sync then ran as part of the generic `MobileHandler` path

That made the `0x5D` login packet inherit cold sector load and broad visibility sync cost.

The runtime path is now narrower:

- `CharacterHandler` keeps the packet-critical bootstrap lean
- `PlayerCharacterLoggedInEvent` is deferred off the `0x5D` critical path
- `PlayerLoginWorldSyncHandler` and `PlayerLoginWorldSyncService` own the login-specific mini snapshot plus visible-range refill
- bulk mobile equipment hydration and smaller lazy-load defaults reduce cold-sector cost before the refill runs

This keeps login-specific world sync policy separate from generic movement and teleport orchestration, which makes the path easier to reason about and cheaper to profile.

## Item Handler Split

`ItemHandler` also grew into a broad packet entry point for unrelated behaviors:

- books
- click/use interaction
- pickup/drop/equip manipulation
- item event refresh fan-out

That boundary has now been narrowed without changing packet ownership:

- `ItemHandler` remains the packet/event router
- `ItemBookService` owns book read/write flows
- `ItemInteractionService` owns single-click and double-click interaction flows
- `ItemManipulationService` owns pickup, drop, equip, and wear-refresh orchestration

This keeps protocol wiring stable while moving behavior-heavy item logic into smaller units that are easier to test and profile.

## Event Listener Pattern

Event listeners implement `IGameEventListener<TEvent>` and are registered with `[RegisterGameEventListener]`:

```csharp
[RegisterGameEventListener]
public class MyHandler : IGameEventListener<SomeGameEvent>
{
    public Task HandleAsync(SomeGameEvent gameEvent, CancellationToken cancellationToken = default)
    {
        // This runs asynchronously from the game loop (if publisher used fire-and-forget).
        // You CAN await async operations here without blocking the game loop.
        // But keep it fast - other listeners for the same event type run in parallel.

        return Task.CompletedTask;
    }
}
```

**Key difference from packet handlers:**
- Packet handlers run ON the game loop thread (must not block).
- Event listeners run asynchronously IF the publisher uses fire-and-forget.
- Event listeners for the same event run in parallel via `Task.WhenAll`.

## Quick Reference

| Operation | Blocking? | Where to use |
|-----------|-----------|--------------|
| `Enqueue(session, packet)` | No | Anywhere |
| `_gameEventBusService.PublishAsync(e).AsTask().GetAwaiter().GetResult()` | **YES** | Never in packet handlers |
| `PublishEventFireAndForget(e)` | No | Packet handlers |
| `await _gameEventBusService.PublishAsync(e)` | Awaits | Event listeners only |
| `GetNearbyMobiles()` on warmed sector | No | Anywhere |
| `GetNearbyMobiles()` on cold sector | **YES** (lazy load) | Avoid in hot paths |
| `BroadcastToPlayersAsync()` | No (enqueue only) | Anywhere |

## Monitoring

`PacketDispatchService` logs slow handlers automatically:

```
[WRN] Slow packet listener opcode=0x02 listener=MovementHandler elapsed=257ms
```

Threshold: 50ms per listener, 100ms per opcode total. If you see these warnings, your handler is blocking the game loop.

---

**Previous**: [Packet System](packets.md)
