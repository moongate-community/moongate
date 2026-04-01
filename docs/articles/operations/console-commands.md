# Console Commands

Moongate v2 provides an interactive console for server administration. Commands are registered at compile time via source generation.

## Overview

The console system is powered by `ConsoleCommandService` and `CommandSystemService`. Commands are .NET classes decorated with `[RegisterConsoleCommand]` and implementing `ICommandExecutor`.

Commands are entered in the server console at runtime.

## Registration

Commands use source-generated registration:

```csharp
[RegisterConsoleCommand("my_command", "Description of the command")]
public sealed class MyCommand : ICommandExecutor
{
    public Task ExecuteCommandAsync(CommandSystemContext context)
    {
        context.Print("Command executed.");
        return Task.CompletedTask;
    }
}
```

At compile time, `ConsoleCommandRegistrationGenerator` emits `BootstrapConsoleCommandRegistration` which registers all discovered commands.

## Access Levels

Commands can be restricted by context and account level:

- **Console**: available from the server terminal
- **InGame**: available from in-game chat (prefixed with `.`)
- **Regular**: any authenticated account
- **GameMaster**: GM-level accounts only

## Available Commands

### General

#### `help` (alias: `?`)

Displays all registered commands with descriptions. Optionally pass a command name to show detailed help for that specific command.

```
help
help teleport
```

Context: Console and InGame. Access: Regular.

#### `exit` (alias: `shutdown`)

Requests a graceful server shutdown via `IServerLifetimeService.RequestShutdown()`.

Context: Console.

#### `broadcast` (alias: `bc`)

Sends a server message to all active sessions. The message appears prefixed with "SERVER: " in orange.

```
broadcast Server restarting in 5 minutes
```

Returns the count of recipients. Context: Console and InGame.

#### `add_user`

Creates a new user account in the persistence layer.

```
add_user <username> <password> <email> [level]
```

The `level` argument is optional and defaults to `Regular`. Validates against duplicate usernames and checks the level enum value. Context: Console and InGame.

#### `lock` (alias: `*`)

Locks console input immediately. Press `*` to unlock. Useful to prevent accidental input on a running server.

Context: Console.

### Player

#### `teleport` (alias: `tp`)

Teleports the current player to the specified map and coordinates. Updates the character entity, sends a `DrawPlayerPacket`, and publishes `MobilePositionChangedEvent`.

```
.teleport <mapId> <x> <y> <z>
```

All arguments must be valid integers. `mapId` must be >= 0. Context: InGame only. Access: GameMaster.

#### `where`

Prints the current position and map of the player.

```
.where
```

Output example: `You are at X: 1325, Y: 1624, Z: 55 on map Felucca`. Context: InGame only. Access: Regular.

#### `bank`

Opens the bank container for the current player. Resolves the bank item from equipped items (`ItemLayerType.Bank`) and sends a `DrawContainerAndAddItemCombinedPacket` to the client.

Context: InGame only. Access: Regular.

#### `add_item_backpack`

Spawns an item from a template and places it in the player backpack. Supports autocomplete for template IDs and an optional stack amount for stackable items.

```
.add_item_backpack <templateId> [amount]
```

Resolves the backpack either from `BackpackId` or from the equipped `Backpack` layer item. Context: InGame only. Access: GameMaster.

#### `gm`

Opens the in-game GM sidebar menu. The menu currently exposes:

- `Add`: free-search item/NPC templates, item tile-art preview, quantity input, `Add To Backpack`, `Target Ground`, and repeat `Brush`
- `Travel`: embedded curated teleport browser grouped by map and category
- `Spawn`: quick world spawn actions that replace the old `spawn_tools` flow
- `Broadcast`: server-wide broadcast messaging with a fixed `SERVER:` prefix

```
.gm
```

Context: InGame only. Access: GameMaster.

#### `spawn_public_moongates`

Rebuilds the full shard-wide public moongate network from `moongate_data/scripts/moongates/data.lua`.

```
.spawn_public_moongates
```

The command removes existing world `public_moongate` items first, then respawns one gate for each destination in the shared Lua dataset. Context: Console and InGame. Access: GameMaster.

#### `resurrect`

Opens a helpful target cursor and resurrects the selected dead player immediately. This bypasses healer and ankh offer flow and is intended for live GM intervention.

```
.resurrect
```

Only dead player ghosts are valid targets. Context: InGame only. Access: GameMaster.

#### `add_item`

Spawns a hardcoded "brick" test item and adds it to the player backpack at position (1,1). Primarily a development/test command.

Context: InGame only. Access: Regular.

#### Live Item Admin Commands

For the GameMaster live-world item and door commands introduced for shard maintenance, see
[In-Game Item Admin Commands](in-game-item-admin-commands.md).

That page covers:

- `.spawn_item <templateId>`
- `.add_door [wood|metal]`
- `.add_wood_door`
- `.add_metal_door`
- `.remove_item`

#### `mod_name`

Opens a target cursor and renames the selected item or mobile. Persists the updated `Name` field and pushes a refresh packet so nearby clients pick up the new tooltip/display name.

```
.mod_name <new name>
```

Targets both `item` and `mobile` serials. Names are trimmed and limited to 60 characters. Context: InGame only. Access: GameMaster.

#### `lock_door`

Opens a target cursor and locks the selected door. Generates a new shared `lockId`, applies it to the targeted door and its linked double-door partner (if any), then creates a physical `key` item carrying the same `lockId` and drops it into the caller backpack.

```
.lock_door
```

Locked doors can only be opened by characters carrying a matching key in equipped items or anywhere in their backpack tree. Context: InGame only. Access: GameMaster.

#### `unlock_door`

Opens a target cursor and removes lock metadata from the selected door. If the door is linked to a double-door partner, both sides are unlocked together.

```
.unlock_door
```

This clears `door_locked` and `door_lock_id` metadata from the affected door items. Context: InGame only. Access: GameMaster.

#### `add_npc`

Spawns an NPC from a mobile template at a target location. The command opens a target cursor; clicking a location in the world spawns the NPC there.

```
.add_npc <templateId>
```

Has an autocomplete provider that lists all available mobile templates. The NPC is added to the spatial world service at the clicked coordinates on the player current map. Context: InGame only. Access: Regular.

#### `send_target`

Sends a target cursor to the player and prints the callback location when a target is selected. Primarily a debug/test command for the targeting pipeline.

Context: InGame only. Access: Regular.

#### `orion`

Spawns the "orione" NPC (a cat with a Lua brain) at a target location. Opens a target cursor; clicking a location spawns the cat there.

Context: InGame only. Access: Regular.

### Persistence

#### `save_persistence`

Forces an immediate world save. Broadcasts a 5-second countdown to all connected players (in orange text), then saves the snapshot and resets the journal. Reports execution time to both players and console.

```
save_persistence
```

Context: Console and InGame.

### World Generation

All world generation commands run as background jobs to avoid blocking the game loop. They report progress and humanized execution time on completion.

#### `spawn_doors`

Generates doors from door specifications via `IWorldGeneratorBuilderService.GenerateAsync("doors", ...)`. Tracks execution time and prints progress messages.

```
.spawn_doors
```

Context: Console and InGame.

#### `spawn_signs`

Creates sign items from seed data. Iterates through all maps, creating sign items from the "sign" template. Handles both labeled signs (text starting with `#`) and named signs. Reports count and time per map.

```
.spawn_signs
```

Context: Console and InGame.

#### `spawn_decorations`

Places decoration objects from loaded data. Creates items from matching templates (falls back to "static" template if no specific template is found). Applies decoration parameters including hue, facing, name, and custom metadata (booleans, integers, locations, delays). Optionally restricts to a single map.

```
.spawn_decorations [mapId]
```

Context: Console and InGame.

#### `create_spawners`

Creates spawner items from loaded spawn definitions. Each spawner is created from the "spawn" template and tagged with a GUID (`spawner_id` custom parameter). Optionally restricts to a single map.

Note: spawn definitions preserve source mobile names. Runtime spawners still keep `generic_npc` as a safety fallback when a referenced mobile template is missing, but normal ModernUO-derived NPC, animal, and monster entries resolve to dedicated generated templates.

Supported spawn kinds:
- `Spawner` - periodic runtime spawner
- `ProximitySpawner` - enters-range trigger spawner using `homeRange`

```
.create_spawners [mapId]
```

Context: Console and InGame.

#### `initial_spawn`

Forces an immediate spawn attempt for all persisted spawner items in the world, or only for a specific map when `mapId` is provided. The command runs in the background, prints progress every `500` spawners, and is safe to rerun because the underlying runtime spawn service still respects each spawner's count and state.

Note: this still uses `generic_npc` as a runtime fallback if a spawner references a missing mobile template, but imported ModernUO mobile names now normally resolve to dedicated templates.
This command force-triggers both periodic and proximity spawners.

```
.initial_spawn [mapId]
```

Context: Console and InGame.

### Utility

#### `build_item_images`

Generates item art image files into `images/items/` by reading UO art data. Uses `IWorldGeneratorBuilderService` with progress callbacks.

```
.build_item_images
```

Context: Console and InGame.

## HTTP Execution

Commands can also be executed via the HTTP API:

```
POST /api/commands/execute
Content-Type: application/json

{
  "command": "broadcast",
  "args": ["Server restarting in 5 minutes"]
}
```

Requires JWT authentication when JWT is enabled.

## Adding New Commands

1. Create a new class in `Moongate.Server/Commands/` under the appropriate domain subfolder.
2. Decorate with `[RegisterConsoleCommand("name", "description")]`.
3. Implement `ICommandExecutor`.
4. The source generator handles registration automatically.

Beginner follow-ups:

- [Create Your First Lua Admin Command](../scripting/create-your-first-lua-admin-command.md)
- [Create Your First C# Admin Command](../architecture/create-your-first-csharp-admin-command.md)

---

**Previous**: [Stress Test](stress-test.md) | **Next**: [Code Convention](../../CODE_CONVENTION.md)
