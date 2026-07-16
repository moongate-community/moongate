# events

Subscribes Lua callbacks to named server events. Backed by the SquidStd
`EventsModule`, bridged to Moongate's event bus.

> [!IMPORTANT]
> Event callbacks are dispatched on the **game-loop thread**. If the publisher
> is already on the loop the handler runs inline; otherwise it is posted onto
> the loop. Either way the handler can mutate world state directly — you do not
> need to wrap its body in `game.post`.

A handler is a Lua function that receives the event as a single table argument.
The table's fields depend on the event; an event with no data (such as
`world_ready`) passes an empty table.

## events.on

```lua
events.on(eventName, callback)
```

Registers `callback` for the server event named `eventName`. Returns nothing.
The same event may have multiple handlers.

**Example**

```lua
events.on("world_ready", function()
  -- Runs on the loop once the world is loaded — safe to spawn directly.
  mobile.create_from_template("warrior_guard_male_npc", 1, 1420, 1690, 0)
end)
```

## events.subscribe

```lua
events.subscribe(eventName, callback)
```

Alias for [`events.on`](#eventson) — identical behavior. Registers `callback`
for the named event.

**Example**

```lua
events.subscribe("world_ready", function(e)
  log.info("world is ready")
end)
```

## Events

| Event | Fired | Payload |
|---|---|---|
| `world_ready` | Once, on the game-loop thread, after the world is loaded and ready. | none (empty table) |

`world_ready` is the recommended hook for bootstrap spawns: the handler runs on
the loop, so it can create and place mobiles and items directly without
`game.post`.
