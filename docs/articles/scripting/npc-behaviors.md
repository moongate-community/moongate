# NPC Behaviors

This page explains the behavior-oriented Lua AI model used by Moongate v2 NPC brains.

## Goal

Keep NPC AI maintainable by separating:

- **brain orchestration** (`on_think`, event routing, priorities)
- **behaviors** (small focused units like follow, evade, hold_position)
- **runtime state** (blackboard values stored per NPC)

## Directory Layout

```text
moongate_data/scripts/ai/
├── behavior.lua                 # behavior registry
├── modernuo/
│   ├── fsm.lua                  # shared phase-1 ModernUO state helpers
│   ├── movement.lua             # shared movement intentions
│   └── targeting.lua            # shared fight-mode and targeting helpers
├── runners/
│   └── utility_runner.lua       # utility/priority behavior runner
├── behaviors/
│   ├── init.lua
│   ├── evade.lua
│   ├── follow.lua
│   ├── hold_position.lua
│   ├── idle.lua
│   ├── leash.lua
│   ├── ranged_keep_distance.lua
│   ├── self_bandage.lua
│   └── return_home.lua
└── brains/
    ├── ai_melee.lua
    ├── ai_archer.lua
    ├── ai_animal.lua
    ├── ai_vendor.lua
    ├── ai_berserk.lua
    ├── guard.lua
    ├── undead_melee.lua
    └── utility_npc.lua
```

## Brain Contract

Mobile templates bind brains through the canonical `ai` object:

```json
{
  "id": "city_guard",
  "ai": {
    "brain": "guard"
  }
}
```

ModernUO-aligned standard brains use the same shape with extra AI parameters:

```json
{
  "id": "juka_lord_npc",
  "ai": {
    "brain": "ai_archer",
    "fightMode": "closest",
    "rangePerception": 10,
    "rangeFight": 3
  }
}
```

Custom shard-authored brains continue to use the same field, for example `ai.brain = "orion"`.

Each Lua brain table can expose:

- `on_think(npc_serial)` required for coroutine execution
- `on_event(event_type, from_serial, event_obj)` optional
- `on_in_range(npc_serial, source_serial, event_obj)` optional
- `on_out_range(npc_serial, source_serial, event_obj)` optional
- `on_speech(npc_id, speaker_id, text, speech_type, map_id, x, y, z)` optional
- `on_death(by_character, context)` optional

`ai.brain = "guard"` resolves to Lua table `guard`.

## Canonical AI Fields

The canonical mobile AI contract is:

- `ai.brain`
- `ai.fightMode`
- `ai.rangePerception`
- `ai.rangeFight`

Standard normalized brain ids currently used by ModernUO-derived templates are:

- `ai_melee`
- `ai_archer`
- `ai_animal`
- `ai_vendor`
- `ai_berserk`
- `ai_mage`
- `ai_healer`
- `ai_thief`

Phase 1 ships full shared support for:

- `ai_melee`
- `ai_archer`
- `ai_animal`
- `ai_vendor`
- `ai_berserk`

`ai_mage`, `ai_healer`, and `ai_thief` are currently compatibility aliases to legacy brains while the engine primitives
for full parity are still incomplete.

## Behavior Pattern

Behaviors are isolated modules registered by ID.  
A behavior usually exposes:

- `score(npc_serial, ctx)` to compute utility
- `run(npc_serial, ctx)` to execute action and return next delay (ms)
- `on_event(npc_serial, ctx, event_type, from_serial, event_obj)` optional

The utility runner selects the highest score, applies anti-jitter hold (`min_hold_ms`), then executes `run`.

## Guard Brain Example

`guard.lua` uses these isolated behaviors:

- `leash`
- `self_bandage`
- `leash`
- `evade`
- `follow` for melee guards
- `ranged_keep_distance` for archer guards
- `return_home`
- `hold_position`

At each tick:

1. build context (`now_ms`, `min_hold_ms`)
2. ask `utility_runner` for the best behavior
3. execute behavior
4. `coroutine.yield(delay_ms)`

Speech events are ignored by the guard brain for targeting decisions.
On in-range events, the guard can greet a player once per source mobile and start combat only when a hostile target enters range.
On ranged guard templates, `params.guard_role = "ranged"` switches the brain to a 4-6 tile spacing behavior and a longer hostile-acquisition radius.

`undead_melee.lua` is a simpler fixed-loop brain:

- ticks every `2000ms`
- calls `perception.find_nearest_player_enemy(npc_serial, 10)`
- arms combat immediately against the nearest hostile player
- uses `steering.follow(..., 1)` so the zombie closes into melee instead of standing still
- clears the active combat target and wanders when no hostile is found

Current `in_range` payload fields include:

- `listener_npc_id`
- `source_mobile_id`
- `source_name`
- `source_is_player`
- `source_fame`
- `source_karma`
- `source_notoriety`
- `source_relation`
- `source_is_enemy`
- `map_id`
- `range`
- `location`

`source_notoriety` is viewer-relative: it is resolved from the listener NPC toward the source mobile, not copied raw from the source entity. `source_relation` is the coarse AI-friendly classification used by brains:

- `Friendly`
- `Neutral`
- `Hostile`

`source_relation` also includes faction hostility:

- same-faction mobiles resolve as `Friendly`
- declared enemy factions resolve as `Hostile`
- innocent non-faction players remain `Neutral` for guard-style NPCs

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
- `leash`
  - Reads `follow_target_serial`, `home_x`, `home_y`, and `leash_radius`
  - Drops the current target when the guard has been pulled too far from home
  - Clears the active combat target before the brain transitions into return-home logic
- `ranged_keep_distance`
  - Reads `follow_target_serial`, `preferred_min_range`, and `preferred_max_range`
  - If the target is too close, it uses `steering.evade(...)`
  - If the target is too far, it uses `steering.follow(...)`
  - Inside the preferred band, it stops movement and keeps the combat target active
  - Clears `follow_target_serial` if the combat target can no longer be armed
- `return_home`
  - Reads `home_x`, `home_y`, `home_z`, and `hold_radius`
  - Walks the guard back to its captured home point when no target is active
- `hold_position`
  - Reads `home_x`, `home_y`, `home_z`, and `hold_radius`
  - Replaces random wandering for guards that are already back near home
- `self_bandage`
  - Reads `self_bandage_hp_threshold` and `self_bandage_score_bonus`
  - Requires a real `bandage` stack in the NPC backpack
  - Starts a delayed self-heal through the `healing` module
  - Keeps a high score while the bandage timer is in flight so the brain does not thrash
- `idle`
  - Fallback behavior when nothing else scores higher
  - Keeps the brain alive without chasing or retreating

## State (Blackboard)

Behavior state is stored per NPC using `npc_state` module keys, for example:

- `follow_target_serial`
- `home_x`
- `home_y`
- `home_z`
- `home_map_id`
- `hold_radius`
- `leash_radius`
- `follow_stop_range`
- `evade_desired_range`
- `evade_hp_threshold`
- `preferred_min_range`
- `preferred_max_range`
- `self_bandage_hp_threshold`
- `self_bandage_score_bonus`
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

Guards also capture a home point once, then use two simple rules:

- if they have no target and drift outside `hold_radius`, `return_home` pulls them back
- if they are dragged beyond `leash_radius` while chasing, `leash` drops the target so they can reset

## Runtime Modules Used by Behaviors

Current core modules for behavior scripts:

- `perception` (distance, nearby friend/enemy lookup, range checks)
- `steering` (follow/evade/move_to/wander/stop movement primitives)
- `combat` (target selection into the server combat loop; `set_target` / `clear_target`)
- `healing` (self-bandage start/status helpers)

`perception.find_nearest_enemy(...)` considers:

- viewer-relative hostility, not just raw target notoriety
- recent aggression
- faction hostility
- players and NPCs that resolve as `Hostile` to the querying NPC

`combat.set_target(...)` does not calculate hit or damage in Lua.
It delegates to `CombatService`, which owns:

- warmode and `CombatantId`
- swing scheduling through `TimerWheelService`
- melee hit/damage resolution
- region/map harmful-action gate on actual attack attempt
- lethal handoff into `DeathService`, including PvE fame/karma awards for player kills against NPCs
- delayed self-bandage completion through `BandageService`
- `npc_state` (typed state variables)
- `time`, `random`, `mobile` (general runtime helpers)

## Self Bandage Notes

`self_bandage` is intentionally narrow in v1:

- only self-heal, never target another mobile
- consumes `1` `bandage` immediately when the timer starts
- heals after a short fixed delay in C#
- does not cure poison, resurrect, or create dirty bandages

The behavior does nothing unless the NPC already has a backpack and at least one `bandage` item inside it.

## Best Practices

- Keep each behavior focused on one decision.
- Store tunables in blackboard keys instead of hardcoding in multiple files.
- Use `on_event` for reactive AI (speech, in-range, out-range), and `on_think` for tactical polling.
- Return explicit delay values from behaviors to control tick frequency.
- For conversational NPCs, prefer `common.npc_dialogue` so deterministic dialogue can claim speech before `ai_dialogue` fallback.
