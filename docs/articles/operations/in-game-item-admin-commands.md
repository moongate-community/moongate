# In-Game Item Admin Commands

Moongate includes a small set of in-game GameMaster commands for live item and door administration. These commands are entered in chat with the leading `.` prefix and only work from an in-game session.

## Shared Workflow

- Type the command in-game, usually with the leading `.` prefix.
- If the command accepts arguments, enter them before the target cursor opens.
- After the command validates its arguments, it will usually open a cursor:
  - `SelectLocation` for spawning items or doors into the world
  - `SelectObject` for removing an existing item or NPC
- Click the target tile or object to complete the action.

These commands are meant for live shard maintenance, map fixing, and controlled test placement. There is no undo step.

## `spawn_item`

Spawns an item template at a targeted world location.

```
.spawn_item <templateId>
```

Example:

```
.spawn_item brick
```

Workflow:

1. Enter a valid item template id.
2. The server opens a location target cursor.
3. Click the tile where the item should appear.
4. The command creates the item, places it on the current map, and adds it to the spatial world.

Behavior notes:

- The template id must resolve to a known item template.
- The command always spawns one item, not a stack quantity.
- The item is placed at the clicked `X/Y/Z` location on the player's active map.
- If the session cannot be resolved to a live in-game character, the command fails before spawning.

Operator caveats:

- Use this for one-off corrections and live testing, not bulk world building.
- The clicked location is the final placement point, so choose the exact tile you want the item to occupy.
- If the template is unknown, the command returns `Unknown item template: <id>`.

Context: InGame only. Access: GameMaster.

## `add_door`

Spawns a door at a targeted location and chooses the door facing from nearby wall-like geometry.

```
.add_door [wood|metal]
```

Aliases:

```
.add_wood_door
.add_metal_door
```

Examples:

```
.add_door
.add_door metal
.add_wood_door
.add_metal_door
```

Workflow:

1. Optionally choose `wood` or `metal`.
2. The server opens a location target cursor.
3. Click the tile where the door should be installed.
4. The command infers the door facing from nearby walls, impassable tiles, windows, doors, and nearby placed items.
5. The door is created on the current map and added to the spatial world.

Behavior notes:

- If no argument is provided, the command defaults to a wood door.
- `wood` uses the `light_wood_door` template.
- `metal` uses the `metal_door` template.
- The alias form also determines the door type, so `.add_metal_door` spawns a metal door even without arguments.
- The final item id and direction are adjusted to match the resolved facing.

Operator caveats:

- Target the doorway tile, not the neighboring wall tile, and make sure there is enough surrounding geometry for the facing logic to infer a sensible orientation.
- The command is intended for wall openings and door fixes, not arbitrary decoration placement.
- If you need a specific door type, use the explicit `wood` or `metal` argument or the corresponding alias.
- Invalid arguments return `Usage: .add_door [wood|metal]`.

Context: InGame only. Access: GameMaster.

## `remove_item`

Opens a target cursor and removes the selected item or NPC.

```
.remove_item
```

Workflow:

1. Run the command with no arguments.
2. The server opens an object target cursor.
3. Click the item or NPC you want to remove.
4. The command deletes the target and reports what was removed.

Behavior notes:

- Items are deleted through the item service.
- NPCs are deleted through the mobile service and removed from the spatial world.
- Player characters are protected and cannot be removed.
- The command resolves names from the target's `Name` field when present, otherwise it falls back to the target serial.

Operator caveats:

- This is an immediate delete, not a temporary hide or despawn.
- Double-check the target before clicking. The cursor can select either items or mobiles, so it is easy to remove the wrong object in a busy area.
- If the target is a player character, the command returns `Cannot remove player characters.`
- If the target is not a valid item or mobile, the command rejects it instead of guessing.
- If the server cannot resolve an active in-game session, the command fails before the cursor is sent.

Context: InGame only. Access: GameMaster.
