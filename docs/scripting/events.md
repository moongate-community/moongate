# Events and timers

This page is part of [Writing Lua scripts](../scripting.md). The functions are listed in the reference:
[`events`](https://moongate.sh/lua/events/) and [`timer`](https://moongate.sh/lua/timer/).

## Events

Scripts react to server events with the built-in `events` module:

```lua
local handle = events.on("character_created", function(e)
    log.info("New character {Name}", e.name)
end)

events.off(handle) -- returns false when the handle is unknown
```

- Each handler runs on the game loop as a coroutine, so it may call `wait()`.
- Handlers of one event run in the order they subscribed. Subscribing or
  unsubscribing inside a handler takes effect from the next event.
- Every handler receives its own table; changing it does not affect other handlers.
- Events are notifications: a handler cannot cancel or change what happened. An
  error in a handler is reported like any script error, and the other handlers
  still run.
- Subscriptions belong to the file that made them. Reloading or invalidating the
  file removes them, like its timers; stopping the engine removes all of them.
- An unknown event name raises an error in `events.on`. The generated
  `definitions.lua` lists the valid names as the `EventName` alias, so editors
  complete them.

### Available events

| Event | Fields |
| --- | --- |
| `character_created` | `serial`, `account_id`, `name`, `race` and `gender` (numbers of the race and the gender; scripts get no enum tables for them), `map`, `x`, `y`, `z`. Raised after a new character and its starting items are saved. |
| `character_deletion_requested` | `serial`, `account_id`, `name`. Raised after a player asks to delete a character; it stays restorable until removed. |
| `character_entered_world` | `serial`, `account_id`, `name`, `map`, `x`, `y`, `z`. Raised after a character entered the world and the client's login completed. |
| `player_say` | `serial`, `name`, `text`, `type`. Raised after a player's character said something and the players and NPCs around heard it; `text` is what they heard and `type` how it was said, a number such as `SpeechType.Yell`. A command (text starting with a dot) raises nothing. |
| `character_left_world` | `serial`, `account_id`, `name`, `map`, `x`, `y`, `z`. Raised after a character left the world because its session closed, once its save was attempted. |
| `player_region_changed` | `serial`, `name`, `previous`, `current`, `map`, `x`, `y`, `z`. Raised when a player's character walks or is teleported from a region into another, or changes map; `previous` and `current` are the regions' names, `nil` outside every region, and `previous` is also `nil` when the character just entered the world |

### Publishing an event from C#

Hosts and plugins publish a bus event to Lua with an explicit registration,
before the engine starts:

```csharp
container.AddScriptEvent<MyEvent>(
    "my_event",
    e => new Dictionary<string, object?> { ["name"] = e.Name, ["amount"] = e.Amount });
```

The name must be snake_case and unique, and each event type is published once.
The mapping runs on the publishing thread and must only read the event. It may
return strings, booleans, numbers, enums (sent as numbers) or null. A mapping
that fails is logged, and the event is skipped for Lua only. Events published
while no script is subscribed cost one lookup and are not queued.

## Timers and wait

`timer.after` runs a function once, `timer.every` repeats it and `timer.cancel` stops either; `wait` parks
the coroutine that calls it.

Call `wait` from a scheduled coroutine, such as a timer callback, not at the top
level of `init.lua` or a required module. It needs a positive, finite delay that
fits the timer range. It yields the coroutine rather than blocking the thread.
Each repeating timer occurrence starts a coroutine: if one calls `wait` for longer
than the repeat interval, multiple suspended invocations can coexist. Cancelling
the timer prevents later starts; it does not cancel an already-started coroutine.
For sequences that must not overlap, use a one-shot callback that schedules its
next run only after its work finishes.
