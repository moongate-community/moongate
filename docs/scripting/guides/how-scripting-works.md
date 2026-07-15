# How scripting works

This guide walks through the Lua script lifecycle: where scripts live, the order
they load in, the thread they run on, and how to hook the world coming up. By the
end you will have one small `main.lua` that spawns a guard when the world is
ready and prints a heartbeat on a timer.

For the exact signature of every function used here, follow the links into the
[reference](../index.md).

## Where scripts live

Scripts are plain `.lua` files under the **`scripts/`** folder of the server's
root directory. The root directory is resolved at startup, in this order:

1. the `--root-directory` command-line argument, if given;
2. the `MOONGATE_ROOT` environment variable, if set;
3. otherwise `moongate_root/` under the current working directory.

The `scripts/` subfolder is created automatically if it does not exist, so a
fresh install has an empty `scripts/` waiting for your files.

## Load order

At engine start the server executes, in this fixed order, whichever of these
files it finds directly in `scripts/`:

1. `bootstrap.lua`
2. `init.lua`
3. `main.lua`

A file that does not exist is simply skipped — you do not need all three. Most
shards use a single `main.lua`. As your shard grows, split code into extra files
and pull them in with Lua's `require`; the loader searches the `scripts/`
directory (including a `modules/` subfolder), so `require("modules/spawns")`
loads `scripts/modules/spawns.lua`.

The server also watches `scripts/` for changes while running, so edits to your
`.lua` files are picked up without a restart.

## The game-loop thread

Moongate runs the world on a single **game-loop thread**. That thread is the
only writer of world state, so every function that creates or mutates items,
mobiles or loot — everything under `item.*`, `mobile.*` and `loot.*` — must run
on it. See the [thread model](../index.md#thread-model) for the full rules.

In practice you rarely have to think about it:

- Callbacks you register with [`events.on`](../reference/events.md#eventson)
  (including `world_ready`) are dispatched **on the loop thread** already. Their
  bodies can touch world state directly.
- Callbacks from [`game.schedule`](../reference/game.md#gameschedule) and
  [`game.schedule_repeating`](../reference/game.md#gameschedule_repeating) also
  run on the loop.
- Only when you are on some *other* thread (an async continuation, for example)
  do you need [`game.post`](../reference/game.md#gamepost) to hop back onto the
  loop.

## `world_ready`: the bootstrap hook

The one event every shard hooks is
[`world_ready`](../reference/events.md#events). It fires **once**, on the loop
thread, after the world has finished loading. Because the handler runs on the
loop, it is the right place to spawn your starting mobiles and items — no
`game.post` needed. Its payload is an empty table.

```lua
events.on("world_ready", function()
  log.info("world is ready")
end)
```

## Timers

Use the [`game`](../reference/game.md) module to run work later or on a
schedule. All three callbacks run on the loop thread:

- [`game.schedule(name, delayMs, fn)`](../reference/game.md#gameschedule) — run
  once after a delay; returns a timer id.
- [`game.schedule_repeating(name, intervalMs, fn)`](../reference/game.md#gameschedule_repeating)
  — run every interval until cancelled; returns a timer id.
- [`game.cancel(timerId)`](../reference/game.md#gamecancel) — stop a scheduled
  timer.

## Putting it together

Here is a complete `scripts/main.lua`. It waits for the world, spawns a town
guard from a template, and starts a one-minute heartbeat you could later cancel
with the id it keeps.

```lua
-- scripts/main.lua

local heartbeat_id

events.on("world_ready", function()
  -- Runs on the game-loop thread: safe to spawn directly.
  local guard = mobile.create_from_template("warrior_guard_male_npc", 1, 1420, 1690, 0)

  if guard then
    log.info("spawned guard {0}", guard)
  else
    log.warn("guard template not found")
  end

  -- A repeating timer, also on the loop thread.
  heartbeat_id = game.schedule_repeating("heartbeat", 60000, function()
    log.debug("still alive")
  end)
end)
```

From here, the [Items](items.md), [Mobiles](mobiles.md) and [Loot](loot.md)
guides build on the same `world_ready` entry point.
