# NPC brains

NPC brains are small Lua strategies for non-player mobiles. A mobile template
binds one with [`BrainScript`](../data/mobile-templates.md#top-level-keys); the
value `guard`, for example, loads `scripts/brains/guard.lua` from the server
root.

Brains observe a snapshot of the world and return **intents**. They never
receive a `MobileEntity` and cannot mutate the world directly. The server
validates and executes accepted intents on the game loop. See the [`brain`
reference](../reference/brain.md) for the helper signatures.

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
        return nil
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

        return brain.say("Non ti avevo mai visto prima.")
    end

    known.last_seen_at = ctx.now_ms
    known.conversations = known.conversations + 1

    return brain.say("Bentornato.")
end

function guard.think()
    return brain.idle()
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
`event`. A hook returns `nil`, one intent, or `brain.decision(next_tick_ms,
intents)`. Hooks may not yield.

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

Brain files are loaded with an allowlisted authoring environment: the `brain`
helpers, selected base functions (`assert`, `error`, `ipairs`, `next`, `pairs`,
`pcall`, `select`, `tonumber`, `tostring`, `type`, `xpcall`), and bounded
subsets of the `math`, `string`, and `table` libraries. Native callbacks whose
work scales with their input are capped at 4,096 characters or 1,024 values.
Pattern matching, formatting, function dumping, and `table.sort` are
unavailable because MoonSharp executes those operations in native callbacks
outside the Lua instruction counter. General world-mutating modules are not
exposed to a brain file. This is the supported authoring contract; brains are
loaded by the shared Lua engine through `Script.LoadFile`, so do not treat it
as a stronger security sandbox than that implementation provides.

## Intents and limits

Use [`brain`](../reference/brain.md) helpers to return intent tables. Intent
execution is the only mutation boundary. The executor resolves the mobile
again, validates target ids against the current perception snapshot and map,
and may reject an intent; a returned intent is not a direct command.

In particular, `brain.engage(target_id)` validates a visible target and, in v1,
sets the owner's combat target and warmode. It does **not** attack or deal
damage. `brain.say("...")` speaks only as the brain's owner (regular speech,
range 15); it takes no serial, so a brain cannot choose an arbitrary speaker.

The complete default `moongate.npcAi.advanced` limits are:

| Setting | Default | Effect |
|---|---:|---|
| `sectorIdleGraceSeconds` | 60 | Grace before an uncovered player sector deactivates. |
| `minTickMilliseconds` | 100 | Minimum hook-selected or default `think` delay. |
| `maxTickMilliseconds` | 60,000 | Maximum hook-selected or default `think` delay. |
| `maxBrainsPerLoop` | 100 | Brains the scheduler may wake in one loop. |
| `maxEventsPerBrainWake` | 16 | Mailbox events delivered before `think` in one wake. |
| `maxMailboxEvents` | 64 | Maximum queued mailbox events per brain. |
| `maxIntentsPerDecision` | 8 | Intent tables read from one `brain.decision`. |
| `instructionBudget` | 50,000 | Lua instruction budget for definition load and each hook. |

All values must be positive; the minimum tick cannot exceed the maximum, and
`maxMailboxEvents` cannot be smaller than `maxEventsPerBrainWake`. Brain files
are limited to 256 KiB and cannot be symbolic links or reparse points. When a
mailbox is full, lower-priority events can be dropped; movement events are
coalesced. Invalid, unknown, malformed, or excess intents are ignored or
rejected rather than granting extra capabilities.
