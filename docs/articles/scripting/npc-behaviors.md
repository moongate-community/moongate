# NPC Behaviors

This page explains the behavior-oriented Lua AI model used by Moongate v2 NPC brains.

## Goal

Keep NPC AI maintainable by separating:

- **brain orchestration** (`brain_loop`, event routing, priorities)
- **behaviors** (small focused units like follow, evade, idle)
- **runtime state** (blackboard values stored per NPC)

## Directory Layout

```text
moongate_data/scripts/ai/
├── behavior.lua                 # behavior registry
├── runners/
│   └── utility_runner.lua       # utility/priority behavior runner
├── behaviors/
│   ├── init.lua
│   ├── evade.lua
│   ├── follow.lua
│   └── idle.lua
└── brains/
    ├── guard.lua
    └── utility_npc.lua
```

## Brain Contract

Each brain table can expose:

- `brain_loop(npc_serial)` required for coroutine execution
- `on_event(event_type, from_serial, event_obj)` optional
- `on_speech(npc_id, speaker_id, text, speech_type, map_id, x, y, z)` optional
- `on_death(by_character, context)` optional

In templates:

```json
{
  "id": "city_guard",
  "brain": "guard"
}
```

`"brain": "guard"` resolves to Lua table `guard`.

## Behavior Pattern

Behaviors are isolated modules registered by ID.  
A behavior usually exposes:

- `score(npc_serial, ctx)` to compute utility
- `run(npc_serial, ctx)` to execute action and return next delay (ms)
- `on_event(npc_serial, ctx, event_type, from_serial, event_obj)` optional

The utility runner selects the highest score, applies anti-jitter hold (`min_hold_ms`), then executes `run`.

## Guard Brain Example

`guard.lua` uses three isolated behaviors:

- `evade`
- `follow`
- `idle`

At each tick:

1. build context (`now_ms`, `min_hold_ms`)
2. ask `utility_runner` for the best behavior
3. execute behavior
4. `coroutine.yield(delay_ms)`

On speech events, the guard can set `follow_target_serial` in blackboard state.

## State (Blackboard)

Behavior state is stored per NPC using `npc_state` module keys, for example:

- `follow_target_serial`
- `follow_stop_range`
- `evade_desired_range`
- `evade_hp_threshold`

This keeps behavior logic stateless and reusable.

## Runtime Modules Used by Behaviors

Current core modules for behavior scripts:

- `perception` (distance, nearby friend/enemy lookup, range checks)
- `steering` (follow/evade/wander/stop movement primitives)
- `combat` (target selection into the server combat loop; `set_target` / `clear_target`)

`combat.set_target(...)` does not calculate hit or damage in Lua.
It delegates to `CombatService`, which owns:

- warmode and `CombatantId`
- swing scheduling through `TimerWheelService`
- melee hit/damage resolution
- region/map harmful-action gate on actual attack attempt
- `npc_state` (typed state variables)
- `time`, `random`, `mobile` (general runtime helpers)

## Best Practices

- Keep each behavior focused on one decision.
- Store tunables in blackboard keys instead of hardcoding in multiple files.
- Use `on_event` for reactive AI (speech, in-range, out-range), and `brain_loop` for tactical polling.
- Return explicit delay values from behaviors to control tick frequency.
- For conversational NPCs, prefer `common.npc_dialogue` so deterministic dialogue can claim speech before `ai_dialogue` fallback.
