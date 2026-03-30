# GM Menu

The GM menu is the primary in-game operations surface for GameMasters. It collects the most common live shard tools into one gump instead of requiring several separate commands or ad-hoc scripts.

Open it in-game with:

```text
.gm
```

Context: InGame only. Access: GameMaster.

## Current Tabs

The shipped GM menu currently exposes four tabs:

- `Add`
- `Travel`
- `Spawn`
- `Broadcast`

These tabs all run inside the same GM gump shell, so switching between them keeps the staff workflow in one place.

## Add

The `Add` tab is the item and mobile placement surface for staff. It is meant for live map fixes, one-off content placement, and quick operational testing.

The tab is designed around template-driven content, not raw art ids. That means staff can search and place authored content using the same template contracts that shard data uses everywhere else.

Use `Add` when you need to:

- place a known item template
- place a known mobile template
- search template ids quickly during live maintenance

## Travel

The `Travel` tab centralizes GM movement shortcuts. Use it for fast repositioning during debugging, event support, or live world inspection.

This tab is meant to remove the need for bouncing between separate travel commands while staff are actively working in-game.

## Spawn

The `Spawn` tab ports the curated actions from the legacy `spawn_tools` gump into the new GM menu.

The current actions are:

- `Spawn Doors`
- `Spawn Signs`
- `Spawn Decorations`
- `Create Spawners`

Each action maps to an existing server command and executes a controlled world-generation workflow. The GM menu does not expose arbitrary command execution here on purpose.

Operationally, this tab is for:

- rebuilding or refreshing authored world geometry
- spawning map decorations after data changes
- creating spawners from configured world data

## Broadcast

The `Broadcast` tab sends a server-wide message to all connected players.

Behavior:

- the operator enters only the message body
- the server automatically prefixes the final message with `SERVER:`
- empty submissions are rejected

Example result:

```text
SERVER: Maintenance starts in 5 minutes.
```

Use this tab for:

- restart warnings
- event announcements
- operational notices during live shard management

## Notes

- The GM menu is a staff tool, not a player-facing system.
- It complements the standalone console and in-game commands; it does not replace every admin command in the repo.
- The `Spawn` and `Broadcast` tabs are intentionally constrained to the built-in workflows they expose today.
