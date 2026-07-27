# ai

`ai` performs the current NPC brain's actions. Every function acts on the
mobile of the brain tick that is running now — there is no serial argument, and
the brain always acts as itself. Calling any `ai` function outside a brain hook
raises a Lua error.

Unlike ordinary Lua modules (`mobile`, `item`, `chat`, …), `ai` is imperative:
you call it for its effect instead of returning a value. Each function performs
one bounded, validated world action and returns a boolean that is `true` when
the action was applied. See the [NPC brains guide](../guides/npc-brains.md) for
the file format, hooks, context, and lifecycle.

## ai.say

```lua
ai.say(text) -> boolean
```

Speaks as the brain owner with regular speech, the default hue, and range 15.
Returns `false` when `text` is blank or longer than 128 characters. It takes no
serial, so a brain cannot choose an arbitrary speaker.

## ai.patrol

```lua
ai.patrol() -> boolean
```

On the owner's home map, steps in a random direction while within eight tiles
of `ctx.home.position`; outside that leash it steps one tile toward home
instead. Returns `false` when the owner is not on its home map.

## ai.return_home

```lua
ai.return_home() -> boolean
```

Steps one tile toward `ctx.home.position`. Returns `false` unless the owner is
on `ctx.home.map_id`.

## ai.move_toward

```lua
ai.move_toward(target_id) -> boolean
```

Steps one tile toward the target. The target must be a positive integer serial
present in `ctx.nearby`, must still exist, and must be on the owner's current
map; otherwise it returns `false`.

## ai.move_away

```lua
ai.move_away(target_id) -> boolean
```

Steps one tile away from the target, with the same target checks as
`ai.move_toward`.

## ai.engage

```lua
ai.engage(target_id) -> boolean
```

Sets the owner's combat target and enables warmode against a perceived target.
It does not move, attack, or deal damage. Returns `false` when the target is
not perceivable.

## ai.clear_target

```lua
ai.clear_target() -> boolean
```

Clears the owner's combat target and disables warmode. Returns `true`.
