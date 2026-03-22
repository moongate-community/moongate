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
│   ├── idle.lua
│   └── ranged_keep_distance.lua
└── brains/
    ├── guard.lua
    └── utility_npc.lua
```

## Brain Contract

Each brain table can expose:

- `brain_loop(npc_serial)` required for coroutine execution
- `on_event(event_type, from_serial, event_obj)` optional
- `on_in_range(npc_serial, source_serial, event_obj)` optional
- `on_out_range(npc_serial, source_serial, event_obj)` optional
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
- `follow` for melee guards
- `ranged_keep_distance` for archer guards
- `idle`

At each tick:

1. build context (`now_ms`, `min_hold_ms`)
2. ask `utility_runner` for the best behavior
3. execute behavior
4. `coroutine.yield(delay_ms)`

On speech events, the guard can set `follow_target_serial` in blackboard state.
On in-range events, the guard can greet a player once per entry and start combat when a hostile target enters range.
On ranged guard templates, `params.guard_role = "ranged"` switches the brain to a 4-6 tile spacing behavior and a longer hostile-acquisition radius.

Current `in_range` payload fields include:

- `listener_npc_id`
- `source_mobile_id`
- `source_name`
- `source_is_player`
- `source_fame`
- `source_karma`
- `source_notoriety`
- `source_is_enemy`
- `map_id`
- `range`
- `location`

## Built-In Behaviors

Current built-in behavior modules under `moongate_data/scripts/ai/behaviors/` are:

- `evade`
  - Reads `evade_from_serial`, `evade_desired_range`, and `evade_hp_threshold`
  - Moves away from the current threat when the threat is too close or HP is low
- `follow`
  - Reads `follow_target_serial` and `follow_stop_range`
  - Chases the target until it reaches the configured stop distance
  - Reasserts `combat.set_target(...)` while following so the C# combat loop stays armed
  - Clears `follow_target_serial` if the combat target can no longer be armed
- `ranged_keep_distance`
  - Reads `follow_target_serial`, `preferred_min_range`, and `preferred_max_range`
  - If the target is too close, it uses `steering.evade(...)`
  - If the target is too far, it uses `steering.follow(...)`
  - Inside the preferred band, it stops movement and keeps the combat target active
  - Clears `follow_target_serial` if the combat target can no longer be armed
- `idle`
  - Fallback behavior when nothing else scores higher
  - Keeps the brain alive without chasing or retreating

## State (Blackboard)

Behavior state is stored per NPC using `npc_state` module keys, for example:

- `follow_target_serial`
- `follow_stop_range`
- `evade_desired_range`
- `evade_hp_threshold`
- `preferred_min_range`
- `preferred_max_range`
- `guard_seen_<serial>`
- `guard_engaged_<serial>`

This keeps behavior logic stateless and reusable.

The guard brain initializes defaults only when a key is missing. That keeps the scripts KISS while still allowing runtime tuning to override blackboard values without being overwritten every tick.

## Guard Ranges

There are three different ranges involved in guard combat:

1. Acquisition range
   - This is the distance at which the Lua brain receives `in_range` for a hostile target.
   - Melee guards use `3` tiles.
   - Guards with `params.guard_role = "ranged"` use `10` tiles.
2. Preferred movement band
   - Archer guards try to stay between `4` and `6` tiles from the target.
   - Melee guards use `follow_stop_range = 1`.
3. Actual weapon attack range
   - This is resolved by `CombatService` from the equipped weapon profile.
   - The current bow template uses `maxRange = 10`.
   - Melee weapons without explicit range metadata fall back to `1`.

These ranges are intentionally separate:

- acquisition determines when the brain reacts
- preferred band determines where the behavior wants to stand
- weapon range determines whether the attack can actually fire

For guards this means:

- warrior guards notice hostiles at `3`, chase, and attack in melee
- archer guards can notice hostiles earlier, position at `4-6`, and still fire out to the bow maximum range

When a hostile leaves range, the guard brain now clears both `follow_target_serial` and the active combat target. That avoids stale target state lingering after `out_range`.

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
- lethal handoff into `DeathService`, including PvE fame/karma awards for player kills against NPCs
- `npc_state` (typed state variables)
- `time`, `random`, `mobile` (general runtime helpers)

## Best Practices

- Keep each behavior focused on one decision.
- Store tunables in blackboard keys instead of hardcoding in multiple files.
- Use `on_event` for reactive AI (speech, in-range, out-range), and `brain_loop` for tactical polling.
- Return explicit delay values from behaviors to control tick frequency.
- For conversational NPCs, prefer `common.npc_dialogue` so deterministic dialogue can claim speech before `ai_dialogue` fallback.
