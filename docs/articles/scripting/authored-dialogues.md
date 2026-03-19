# Authored Dialogues

Moongate supports deterministic NPC dialogue trees authored in Lua.

This feature is fully usable without OpenAI.

Use this when you want:

- quest or vendor conversations with fixed outcomes
- reusable topic routing from nearby player speech
- persistent per-NPC, per-player memory without calling OpenAI
- deterministic dialogue with no OpenAI dependency at all

## Files And Layout

Recommended layout:

```text
moongate_data/scripts/
├── common/
│   ├── dialogue.lua
│   └── npc_dialogue.lua
└── dialogs/
    └── innkeeper.lua
```

NPC brains then bind and use those conversations from `scripts/ai/npcs/**`.

## Conversation DSL

Use `common.dialogue` to register a conversation table.

```lua
local dialogue = require("common.dialogue")

return dialogue.conversation("innkeeper", {
    start = "start",

    topics = {
        room = { "room", "stanza", "letto", "sleep" },
        rumor = { "rumor", "gossip", "news" },
    },

    topic_routes = {
        room = "room_offer",
        rumor = "rumors",
    },

    nodes = {
        start = dialogue.node {
            text = "Benvenuto alla Locanda del Cervo Rosso. Cosa ti serve?",
            options = {
                dialogue.option { text = "Una stanza", goto_ = "room_offer" },
                dialogue.option { text = "Voci di corridoio", goto_ = "rumors" },
            }
        },

        room_offer = dialogue.node {
            text = "Una stanza costa 15 monete d'oro.",
            options = {
                dialogue.option { text = "Accetto", goto_ = "room_done" },
                dialogue.option { text = "No grazie", goto_ = "bye" },
            }
        },

        room_done = dialogue.node {
            text = "La stanza al piano di sopra e' tua per la notte.",
            options = {
                dialogue.option { text = "Grazie", goto_ = "bye" },
            }
        },

        rumors = dialogue.node {
            text = "Dicono che vecchie miniere a nord siano di nuovo abitate.",
            options = {
                dialogue.option { text = "Interessante", goto_ = "bye" },
            }
        },

        bye = dialogue.node {
            text = "Buona permanenza.",
            options = {}
        }
    }
})
```

Notes:

- `goto_` is accepted because `goto` is a Lua keyword
- `topics` defines keyword groups
- `topic_routes` maps matched topic ids to destination nodes
- `nodes` is the real dialogue graph

## Conditions And Effects

Options can define:

- `condition(ctx)` to decide visibility
- `effects(ctx)` to mutate state before moving to the next node

```lua
dialogue.option {
    text = "Accetto",
    condition = function(ctx)
        return ctx:has_item("gold_coin", 15)
    end,
    effects = function(ctx)
        ctx:remove_item("gold_coin", 15)
        ctx:set_memory_flag("has_rented_room", true)
        ctx:add_memory_number("rooms_rented", 1)
    end,
    goto_ = "room_done"
}
```

## Context API

`DialogueContext` exposes both short-lived session state and persistent memory.

Actors:

- `ctx.speaker`
- `ctx.listener`
- `ctx.conversation_id`
- `ctx.node_id`

Session state:

- `ctx:get_flag(key)`
- `ctx:set_flag(key, value)`

Persistent memory:

- `ctx:get_memory_flag(key)`
- `ctx:set_memory_flag(key, value)`
- `ctx:get_memory_number(key)`
- `ctx:set_memory_number(key, value)`
- `ctx:add_memory_number(key, delta)`
- `ctx:get_memory_text(key)`
- `ctx:set_memory_text(key, value)`

Speech and flow:

- `ctx:say(text)`
- `ctx:emote(text)`
- `ctx:yell(text)`
- `ctx:whisper(text)`
- `ctx:end_conversation()`

## Persistent Memory

Dialogue memory is AOT-safe and typed. Each NPC stores entries keyed by the other mobile serial.

Stored data is limited to:

- `flags: Dictionary<string, bool>`
- `numbers: Dictionary<string, long>`
- `texts: Dictionary<string, string>`
- `last_node`
- `last_topic`
- `last_interaction_utc`

Runtime files live under:

- `moongate_data/runtime/dialogue_memory/<npc_serial>.json`

## Relationship To OpenAI Dialogue

`dialogue` and `ai_dialogue` are separate features:

- `dialogue`
  - deterministic
  - authored in Lua
  - no OpenAI required
- `ai_dialogue`
  - generative
  - optional
  - requires LLM configuration

You can run authored dialogue alone by binding only a `conversation_id`.

## Authored Dialogue And OpenAI Together

Use `common.npc_dialogue` when you want deterministic dialogue first and OpenAI as fallback.

```lua
local npc_dialogue = require("common.npc_dialogue")

local DIALOGUE_CONFIG = {
    conversation_id = "innkeeper",
    prompt_file = "innkeeper.txt",
}

function innkeeper.on_spawn(npc_id, _ctx)
    local npc = mobile.get(npc_id)
    if npc == nil then
        return
    end

    npc_dialogue.init(npc, DIALOGUE_CONFIG)
end

function innkeeper.on_speech(npc_id, speaker_id, text, _speech_type, _map_id, _x, _y, _z)
    local npc = mobile.get(npc_id)
    local speaker = mobile.get(speaker_id)

    if npc == nil or speaker == nil then
        return
    end

    if npc_dialogue.listener(npc, speaker, text, DIALOGUE_CONFIG) then
        return
    end

    npc:say("Posso aiutarti in altro?")
end
```

Resolution order:

1. active authored dialogue session and numeric option choice
2. authored topic match
3. `ai_dialogue.listener(...)` fallback if configured

`common.npc_dialogue` is optional. If you do not want OpenAI at all, use only `dialogue.init(...)` and `dialogue.listener(...)`.

## Example Asset

The repository ships an example conversation here:

- `moongate_data/scripts/dialogs/innkeeper.lua`

Use it as the reference pattern for new authored dialogues.
