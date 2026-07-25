# brain

`brain` creates NPC-brain intent tables. It exists only in a brain file's
isolated environment; it is not available to ordinary `bootstrap.lua`,
`init.lua`, or `main.lua` scripts.

An intent is declarative. Return it from a hook and the server validates and
applies it on the game loop. See the [NPC brains guide](../guides/npc-brains.md)
for the file format, hooks, context, and lifecycle.

## brain.idle

```lua
brain.idle() -> { type = "idle" }
```

Returns the no-op intent table `{ type = "idle" }`.

## brain.say

```lua
brain.say(text) -> { type = "say", text = text }
```

Returns `{ type = "say", text = text }`, passing its argument through without
validation. `text` must be a non-blank string of at most 128 characters for
the intent to be accepted. The server always speaks as the brain owner; this
helper accepts no arbitrary mobile serial.

## brain.patrol

```lua
brain.patrol() -> { type = "patrol" }
```

Returns `{ type = "patrol" }`. On its home map, the owner moves in a random
direction while within eight tiles of `ctx.home.position`; outside that leash
it moves one step toward home instead.

## brain.move_toward

```lua
brain.move_toward(target_id) -> { type = "move_toward", target_id = target_id }
```

Returns `{ type = "move_toward", target_id = target_id }`, passing its
argument through without validation. The target must be a positive integer
serial in `ctx.nearby`, must still exist, and must be on the owner's current
map.

## brain.move_away

```lua
brain.move_away(target_id) -> { type = "move_away", target_id = target_id }
```

Returns `{ type = "move_away", target_id = target_id }`, with the same target
checks as `brain.move_toward`.

## brain.engage

```lua
brain.engage(target_id) -> { type = "engage", target_id = target_id }
```

Returns `{ type = "engage", target_id = target_id }`. In v1 the accepted
result only sets the owner's combat target and enables warmode. It does not
move, attack, or deal damage.

## brain.clear_target

```lua
brain.clear_target() -> { type = "clear_target" }
```

Returns `{ type = "clear_target" }`, an intent that clears the owner's combat
target and disables warmode.

## brain.return_home

```lua
brain.return_home() -> { type = "return_home" }
```

Returns `{ type = "return_home" }`, an intent to take one step toward
`ctx.home.position`. It is accepted only while the owner remains on
`ctx.home.map_id`.

## brain.decision

```lua
brain.decision(next_tick_ms, intents) -> {
  next_tick_ms = next_tick_ms,
  intents = intents,
}
```

Returns this table without validating or reshaping either argument. Its fields
are interpreted as follows:

| Key | Meaning |
|---|---|
| `next_tick_ms` | Requested delay before the next `think`; finite numbers are truncated and clamped to the configured minimum/maximum. `nil`, non-numbers, and non-finite numbers leave the schedule unchanged. |
| `intents` | A Lua array of intent tables. The runtime reads only the consecutive numeric entries from `1` through the array length, up to the configured maximum (8 by default); `nil` or a non-table produces no intents. |

A hook may return one intent directly instead of a decision. Returning `nil`
produces no intents. Any other return type is a hook failure. To represent an
empty decision, pass an empty array: `brain.decision(nil, {})`. Calling
`brain.decision()` leaves both fields `nil`, so it is not recognized as a
decision and is treated as one malformed/unknown intent instead.
