# Quests

Moongate quest authoring is Lua-first, but runtime execution stays server-owned.

- Quest definitions live under `moongate_data/scripts/quests/**/*.lua`
- Authors declare quests with the `quest` DSL
- The server compiles and validates those files at startup and during hot reload
- Accepted quest progress is persisted on the player entity, not in Lua tables

This keeps authoring ergonomic without moving quest rules or persistence into ad-hoc script state.

## Runtime Flow

The shipped quest experience uses two shared Lua gumps:

- `moongate_data/scripts/gumps/quests/quest_dialog.lua`
- `moongate_data/scripts/gumps/quests/quest_journal.lua`

They are wired through:

- `moongate_data/scripts/interaction/quests.lua`
- the `quests` runtime module

Player-facing behavior:

- the NPC quest context menu opens the shared quest dialog
- the client `Quests` button opens the shared quest journal
- accepting and completing quests goes through server validation

## Quest DSL

Quest scripts are declarative. A typical quest looks like this:

```lua
quest.define({
    id = "new_haven.rat_hunt",
    name = "Rat Hunt",
    category = "starter",
    description = "Cull the rat infestation near the mill.",
    quest_givers = { "farmer_npc" },
    completion_npcs = { "farmer_npc" },
    repeatable = false,
    max_active_per_character = 1,
    objectives = {
        quest.kill({ mobiles = { "sewer_rat", "giant_rat" }, amount = 10 }),
        quest.collect({ item_template_id = "rat_tail", amount = 10 }),
        quest.deliver({ item_template_id = "rat_tail", amount = 10 })
    },
    rewards = {
        quest.gold(150),
        quest.item("bandage", 10)
    }
})
```

### Top-Level Fields

- `id`: stable quest identifier
- `name`: player-facing title
- `category`: free-form category label used by UI
- `description`: player-facing description
- `quest_givers`: quest giver NPC template ids
- `completion_npcs`: NPC template ids allowed to turn the quest in
- `repeatable`: `true` or `false`
- `max_active_per_character`: current V1 value must be `1`
- `objectives`: ordered list of objective definitions
- `rewards`: optional ordered list of reward definitions

Use stable `templateId` values for NPC and item references. Do not key quest behavior by display names.

## Objective Helpers

Supported objective helpers:

```lua
quest.kill({ mobiles = { "sewer_rat" }, amount = 5 })
quest.collect({ item_template_id = "rat_tail", amount = 5 })
quest.deliver({ item_template_id = "rat_tail", amount = 5 })
```

Rules:

- `quest.kill(...)` requires `mobiles` and `amount`
- `quest.collect(...)` requires `item_template_id` and `amount`
- `quest.deliver(...)` requires `item_template_id` and `amount`
- `amount` must be a positive integer

Objective progress is tracked by compiled objective identity, not by Lua table position alone.

## Reward Helpers

Supported reward helpers:

```lua
quest.gold(150)
quest.item("bandage", 10)
```

Rules:

- `quest.gold(amount)` adds gold to the reward bundle
- `quest.item(item_template_id, amount)` grants item rewards by template id
- reward amounts must be positive integers

## Hot Reload

Quest scripts are not treated like generic lazy Lua chunks.

When `Scripting.EnableFileWatcher` is enabled:

- a changed file under `scripts/quests/**/*.lua` is recompiled and revalidated through the quest file loader
- a broken reload does not replace the last valid quest snapshot
- explicit quest file deletes remove the deleted quest from the compiled registry

This means quest authoring gets validation-oriented hot reload instead of “load later on next require”.

## `quests` Runtime Module

The shared quest UI scripts use the `quests` runtime module:

```lua
quests.open(session_id, character_id, npc_serial)
quests.open_journal(session_id, character_id)
quests.get_available(session_id, character_id, npc_serial)
quests.get_active(session_id, character_id, npc_serial)
quests.get_journal(session_id, character_id)
quests.accept(session_id, character_id, npc_serial, quest_id)
quests.complete(session_id, character_id, npc_serial, quest_id)
```

Use it for dialog and journal presentation, not for defining quest templates.

## Current V1 Scope

The first quest slice is intentionally narrow:

- supported objectives: `kill`, `collect`, `deliver`
- no branching quest graphs
- no timers
- no scripted per-quest callbacks
- no multiple simultaneous instances of the same quest on one character

That keeps the authoring model simple while still covering classic quest-giver gameplay.
