# game

Runs work on the game-loop thread and schedules timers. Backed by
`GameLoopModule`.

The game-loop thread is the single-writer boundary for world state, so any code
that touches `item.*`, `mobile.*` or `loot.*` must run there. Use this module to
reach the loop from code that runs off it. Callbacks are invoked through a
wrapper that catches and logs any exception, so a throwing callback never
crashes the loop.

> [!NOTE]
> Inside an `events.on` / `world_ready` handler the body already runs on the
> loop thread, so wrapping it in `game.post` is unnecessary.

## game.post

```lua
game.post(callback)
```

Queues `callback` (a Lua function taking no arguments) to run on the game-loop
thread on the next frame. Returns nothing.

**Example**

```lua
game.post(function()
  local guard = mobile.create("Town Guard", 1, 1420, 1690, 0)
  log.info("spawned guard {0}", guard)
end)
```

## game.schedule

```lua
game.schedule(name, delayMs, callback) -> timer id
```

Runs `callback` **once** after `delayMs` milliseconds, on the game-loop thread.
`name` is a label for the timer. Returns the timer id (a string) that can be
passed to [`game.cancel`](#gamecancel).

**Example**

```lua
local id = game.schedule("greet", 5000, function()
  log.info("five seconds later")
end)
```

## game.schedule_repeating

```lua
game.schedule_repeating(name, intervalMs, callback) -> timer id
```

Runs `callback` **every** `intervalMs` milliseconds, on the game-loop thread,
until cancelled. `name` is a label for the timer. Returns the timer id (a
string).

**Example**

```lua
local id = game.schedule_repeating("heartbeat", 60000, function()
  log.debug("still alive")
end)
```

## game.cancel

```lua
game.cancel(timerId) -> boolean
```

Cancels a scheduled timer by the id returned from
[`game.schedule`](#gameschedule) or
[`game.schedule_repeating`](#gameschedule_repeating). Returns `true` when a
matching timer was found and removed, `false` otherwise.

**Example**

```lua
local id = game.schedule_repeating("heartbeat", 60000, tick)
-- later
if game.cancel(id) then
  log.info("heartbeat stopped")
end
```
