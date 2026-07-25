# brain

`brain` creates NPC-brain intent tables. It exists only in a brain file's
isolated environment; it is not available to ordinary `bootstrap.lua`,
`init.lua`, or `main.lua` scripts.

An intent is declarative. Return it from a hook and the server validates and
applies it on the game loop. See the [NPC brains guide](../guides/npc-brains.md)
for the file format, hooks, context, and lifecycle.

## brain.idle

```lua
brain.idle() -> intent
```

Returns a no-op intent.

## brain.say

```lua
brain.say(text) -> intent
```

Returns a regular-speech intent. `text` must be a non-blank string of at most
128 characters to be accepted. The server always speaks as the brain owner;
this helper accepts no arbitrary mobile serial.

## brain.patrol

```lua
brain.patrol() -> intent
```

Returns a patrol intent. On its home map, the owner moves in a random direction
while within eight tiles of `ctx.home.position`; outside that leash it moves one
step toward home instead.

## brain.move_toward

```lua
brain.move_toward(target_id) -> intent
```

Returns an intent to move one eight-way step toward `target_id`. The target must
be a positive serial in `ctx.nearby`, must still exist, and must be on the
owner's current map.

## brain.move_away

```lua
brain.move_away(target_id) -> intent
```

Returns an intent to move one eight-way step away from `target_id`, with the
same target checks as `brain.move_toward`.

## brain.engage

```lua
brain.engage(target_id) -> intent
```

Returns an intent to engage a visible target. In v1 the accepted result only
sets the owner's combat target and enables warmode. It does not move, attack,
or deal damage.

## brain.clear_target

```lua
brain.clear_target() -> intent
```

Returns an intent that clears the owner's combat target and disables warmode.

## brain.return_home

```lua
brain.return_home() -> intent
```

Returns an intent to take one step toward `ctx.home.position`. It is accepted
only while the owner remains on `ctx.home.map_id`.

## brain.decision

```lua
brain.decision(next_tick_ms, intents) -> decision
```

Returns a decision table with these fields:

| Key | Meaning |
|---|---|
| `next_tick_ms` | Requested delay before the next `think`; finite numbers are truncated and clamped to the configured minimum/maximum. Non-numbers leave the schedule unchanged. |
| `intents` | Lua array of intent tables. Only the first configured maximum is read (8 by default). |

A hook may return one intent directly instead of a decision. Returning `nil`
produces no intents. Any other return type is a hook failure.
