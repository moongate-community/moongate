# NPC brains

NPC brains are small Lua strategies for non-player mobiles. A mobile template
binds one with [`BrainScript`](../data/mobile-templates.md#top-level-keys); the
value `guard`, for example, loads `scripts/brains/guard.lua` from the server
root.

Brains observe a snapshot of the world and act on it. They run in the same Lua
runtime as every other script, so a brain can use the ordinary modules
(`mobile`, `item`, `chat`, `events`, `log`, …) and, most importantly, the
imperative [`ai`](../reference/ai.md) module, which performs the brain's own
bounded, validated actions (speak, move, engage). Every `ai` call acts on the
mobile of the current tick; the brain always acts as itself.

## Write a brain

Create exactly one file at `scripts/brains/<id>.lua`. Its filename and the
returned table's `id` must match exactly. Brain ids use lowercase letters,
digits, `_`, and `-`; they must begin with a letter or digit.

The file returns one table. It does not register a global. The returned
definition and its Lua environment are shared by every mobile using that brain
id, so treat both as stateless strategy data. Put every per-mobile value in the
`state` table passed to the hooks.

This is the shipped `guard` brain shape:

```lua
-- scripts/brains/guard.lua
local guard = {
    id = "guard",
    default_tick_ms = 1000,
    perception_range = 12,
    hearing_range = 15,
}

function guard.on_speech_heard(ctx, state, event)
    if event == nil or event.speaker_is_player ~= true then
        return
    end

    state.seen_players = state.seen_players or {}

    local speaker_id = event.speaker_id
    local known = state.seen_players[speaker_id]

    if known == nil then
        state.seen_players[speaker_id] = {
            first_seen_at = ctx.now_ms,
            last_seen_at = ctx.now_ms,
            conversations = 1,
        }

        ai.say("I haven't seen you before.")
        return
    end

    known.last_seen_at = ctx.now_ms
    known.conversations = known.conversations + 1

    ai.say("Welcome back.")
end

function guard.think()
    -- idle: no action
end

return guard
```

The five required fields are:

| Field | Type | Rules |
|---|---|---|
| `id` | string | Must exactly equal the filename without `.lua`. |
| `default_tick_ms` | integer | Default delay before the next `think`; must be within the configured minimum and maximum. |
| `perception_range` | integer | `0` through `64`; controls `ctx.nearby` and range events. |
| `hearing_range` | integer | `0` through `64`; controls speech events. |
| `think` | function | Required periodic hook. |

Each hook is called as `hook(ctx, state, event)`. `think` receives `nil` for
`event`. A hook acts by calling `ai.*` (and any other module) directly; its
return value is optional. Returning a **number** overrides the delay before the
next `think` (finite values are truncated and clamped to the configured
minimum/maximum); any other return value leaves the schedule unchanged. Hooks
may not yield.

## Context, state, and events

`ctx` is a fresh snapshot-shaped Lua table for each invocation. Changing that
table changes only the Lua value for the current call, never world state:

| Key | Shape |
|---|---|
| `now_ms` | Unix time in milliseconds. |
| `self` | Mobile snapshot: `id`, `name`, `is_player`, `map_id`, `position`, `hits`, `hits_max`, `hits_percent`, `warmode`, `combatant_id`, `criminal`, `kills`. |
| `home` | `{ map_id, position }`, where `position` has `x`, `y`, `z`. |
| `nearby` | Array of the same mobile snapshots, excluding `self`, within `perception_range`. |

`combatant_id` is `nil` when no combatant is set. Every `position` table has
`x`, `y`, and `z`.

`state` is a private Lua table for one mobile, initially empty. It is retained
when that mobile's brain sleeps and wakes, and it survives a successful reload
of the same brain id. It is not persisted: a server restart starts with a new
state table. Changing a mobile to a different brain id replaces its state table
with a new one. The scheduler also clears state after four consecutive brain
failures before retrying.

`ctx.home` is the mobile's map and position when its current brain binding is
created. It remains that bind-time origin through sleep/wake and a successful
same-id reload. A brain-id change captures a new home, as does a server
restart.

### State vs memory

`state` is **transient**: in-memory, per-mobile, lost on restart and cleared on a
brain-id change or repeated failures. Use it for scratch within a tick or
session. For facts an NPC must remember long-term, use the durable
[`memory`](../reference/memory.md) module — persisted per NPC, scalar-valued and
bounded, surviving restarts and brain changes:

```lua
function guard.on_speech_heard(ctx, state, event)
  if event.speaker_is_player ~= true then return end
  local seen = (memory.get("seen:" .. event.speaker_id) or 0) + 1
  memory.set("seen:" .. event.speaker_id, seen)
  if seen == 1 then ai.say("I haven't seen you before.") else ai.say("Welcome back.") end
end
```

## Hooks and event payloads

`think(ctx, state, nil)` is required. All other hooks are optional; omit them
or set them to `nil` when unused. Every delivered event table has a lowercase
`type` key.

| Hook | `event` keys |
|---|---|
| `on_activate` | `type = "activate"` |
| `on_deactivate` | `type = "deactivate"` |
| `on_speech_heard` | `type = "speechheard"`, `speaker_id`, `speaker_name`, `speaker_is_player`, `speech_type`, `text` |
| `on_mobile_entered_range` | `type = "mobileenteredrange"`, `mobile_id`, `mobile_name`, `mobile_is_player`, `from_position`, `to_position` |
| `on_mobile_left_range` | `type = "mobileleftrange"`, `mobile_id`, `mobile_name`, `mobile_is_player`, `from_position`, `to_position` |
| `on_mobile_moved` | `type = "mobilemoved"`, `mobile_id`, `mobile_name`, `mobile_is_player`, `from_position`, `to_position` |
| `on_attacked` | `type = "attacked"`, `attacker_id` |
| `on_damage` | `type = "damage"`, `attacker_id`, `amount` |
| `on_death` | `type = "death"`, `killer_id` |

Subject ids and associated fields are `nil` when the source mobile cannot be
resolved. `speech_type` and `text` are also `nil` when unavailable. Position
keys are `nil` when the source event has no position; otherwise they are
`{ x, y, z }`. `amount` is the reported damage amount.

Prefer these hooks over `events.subscribe` for reacting to the world. A brain
that subscribes registers a closure that outlives the mobile, runs outside the
brain's instruction budget, and is never unbound when the mobile despawns.

## Runtime behavior

Brain execution is activated by **players only**. Each online player keeps the
3×3 sector square centered on that player active. When the last player leaves a
sector, it stays active for the configured grace period, 60 seconds by default,
then brain execution there deactivates. This affects Lua AI only: it does not
sleep the mobile entity or unrelated server systems.

Activation queues `on_activate`; deactivation queues `on_deactivate`, runs that
hook once, then sleeps the brain and clears its queued events. While active, the
scheduler processes events before due `think` work. It gives one brain at most
one wake per scheduler loop.

The brain directory is seeded with missing built-in files (`guard.lua`,
`orion.lua`, and `vega.lua`) without replacing an existing local file. A
dedicated watcher observes direct `*.lua` changes in `scripts/brains/`,
debounces them, and validates the candidate before installing it. Invalid Lua,
metadata, or load results leave the last known-good definition in use. A
successful reload updates the shared strategy and metadata while preserving
each same-id binding's private state and home.

Brains are loaded by the shared Lua engine with full access to the registered
modules — there is no separate authoring sandbox. The single guard against a
runaway brain is the **instruction budget**: definition load and each hook run
as an instruction-counted coroutine, and a hook that exceeds the budget (an
infinite loop, for example) is force-suspended and reported as a failure rather
than blocking the game loop. Because there is no sandbox, a brain has the same
reach — and the same responsibility — as any other script; keep hooks small and
deterministic.

## Actions and limits

Use the [`ai`](../reference/ai.md) module for the brain's own actions. Each
call is the mutation boundary: the action service resolves the mobile again,
validates target ids against the current perception snapshot and map, and may
reject the action, returning `false`.

In particular, `ai.engage(target_id)` validates a perceived target and sets the
owner's combat target and warmode; it does **not** attack or deal damage.
`ai.say("...")` speaks only as the brain's owner (regular speech, range 15); it
takes no serial, so a brain cannot choose an arbitrary speaker. For free
movement, `ai.step(direction)` steps one tile in a compass direction and
`ai.move_to(x, y)` steps greedily toward a coordinate; both are single validated
steps with no obstacle avoidance, so the brain composes multi-tile paths across
ticks.

The complete default `moongate.npcAi.advanced` limits are:

| Setting | Default | Effect |
|---|---:|---|
| `sectorIdleGraceSeconds` | 60 | Grace before an uncovered player sector deactivates. |
| `minTickMilliseconds` | 100 | Minimum hook-selected or default `think` delay. |
| `maxTickMilliseconds` | 60,000 | Maximum hook-selected or default `think` delay. |
| `maxBrainsPerLoop` | 100 | Brains the scheduler may wake in one loop. |
| `maxEventsPerBrainWake` | 16 | Mailbox events delivered before `think` in one wake. |
| `maxMailboxEvents` | 64 | Maximum queued mailbox events per brain. |
| `instructionBudget` | 50,000 | Lua instruction budget for definition load and each hook. |

All values must be positive; the minimum tick cannot exceed the maximum, and
`maxMailboxEvents` cannot be smaller than `maxEventsPerBrainWake`. Brain files
are limited to 256 KiB and cannot be symbolic links or reparse points. When a
mailbox is full, lower-priority events can be dropped; movement events are
coalesced.
