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

Speaks as the brain owner. It takes no serial, so a brain cannot choose an
arbitrary speaker.

The rules are the same ones [`chat.say`](chat.md) applies, because both go
through the same service: `false` when the text is blank, longer than 128
characters, or a command rather than speech. The leading character decides how
it is spoken and at what range — see [chat](chat.md) for the table. Ordinary
text is regular speech in the default hue, heard 15 tiles away.

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

## ai.step

```lua
ai.step(direction) -> boolean
```

Steps one tile in a compass direction. `direction` is a case-insensitive
string — a full name or its short alias: `north`/`n`, `northeast`/`ne`,
`east`/`e`, `southeast`/`se`, `south`/`s`, `southwest`/`sw`, `west`/`w`,
`northwest`/`nw`. Returns `false` on an unknown direction or a blocked step.
The step is validated like any NPC move; there is no obstacle avoidance beyond
that single-tile check, so the brain composes its own path.

## ai.move_to

```lua
ai.move_to(x, y) -> boolean
```

Steps one tile toward `(x, y)` at the owner's current z, choosing the greedy
eight-way direction. Returns `false` when the step is blocked; the owner
"arrives" once `x, y` are reached. Like `ai.return_home`, this is greedy with no
obstacle avoidance — it can stall against a wall.
