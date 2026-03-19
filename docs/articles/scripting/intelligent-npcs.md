# Intelligent NPC Dialogue

Moongate can optionally use OpenAI to let selected NPC brains:

- reply to nearby player speech
- speak on their own when players are nearby
- keep a compact long-term memory per NPC

This is wired through the Lua module `ai_dialogue`.

This feature is separate from authored dialogue:

- use `dialogue` for deterministic Lua conversation trees
- use `ai_dialogue` for OpenAI-backed generative replies
- use `common.npc_dialogue` only if you want deterministic dialogue first and OpenAI as fallback

## Configuration

Enable it in `moongate.json` under `Llm`:

```json
{
  "Llm": {
    "IsEnabled": true,
    "Model": "gpt-5-mini"
  }
}
```

API key options:

- recommended: `OPENAI_API_KEY` environment variable
- alternative: `Llm.ApiKey` in `moongate.json`
- precedence: `Llm.ApiKey` wins if both are set

## Prompt Files

Static NPC persona prompts live under:

- `moongate_data/templates/npc_ai_prompts/*.txt`

Example:

- `moongate_data/templates/npc_ai_prompts/lilly.txt`

These files should describe:

- identity
- tone and personality
- world knowledge
- roleplay rules

## Memory Files

Persistent NPC memories live under:

- `moongate_data/runtime/npc_memories/<npcSerial>.txt`

Example:

- `moongate_data/runtime/npc_memories/0x012314.txt`

The runtime keeps these as compact summaries, not raw full chat logs.

## Lua API

The module exposes:

- `ai_dialogue.init(npc, "lilly.txt")`
- `ai_dialogue.listener(npc, sender, text)`
- `ai_dialogue.idle(npc)`

Typical pattern:

```lua
function lilly.on_spawn(npc_id, _ctx)
    local npc = mobile.get(npc_id)
    if npc == nil then
        return
    end

    ai_dialogue.init(npc, "lilly.txt")
end

function lilly.on_speech(npc_id, speaker_id, text, _speech_type, _map_id, _x, _y, _z)
    local npc = mobile.get(npc_id)
    local speaker = mobile.get(speaker_id)

    if npc == nil or speaker == nil then
        return
    end

    if ai_dialogue.listener(npc, speaker, text) then
        return
    end

    npc:say("hello to you, " .. speaker.name .. "!")
end

function lilly.brain_loop(npc_id)
    while true do
        local npc = mobile.get(npc_id)
        if npc ~= nil then
            ai_dialogue.idle(npc)
        end

        coroutine.yield(1000)
    end
end
```

Recommended hybrid pattern:

```lua
local npc_dialogue = require("common.npc_dialogue")

local config = {
    conversation_id = "innkeeper",
    prompt_file = "innkeeper.txt",
}

function innkeeper.on_spawn(npc_id, _ctx)
    local npc = mobile.get(npc_id)
    if npc ~= nil then
        npc_dialogue.init(npc, config)
    end
end

function innkeeper.on_speech(npc_id, speaker_id, text, _speech_type, _map_id, _x, _y, _z)
    local npc = mobile.get(npc_id)
    local speaker = mobile.get(speaker_id)

    if npc == nil or speaker == nil then
        return
    end

    if npc_dialogue.listener(npc, speaker, text, config) then
        return
    end

    npc:say("Posso aiutarti in altro?")
end
```

## How It Works

At runtime, an intelligent NPC goes through this flow:

1. A Lua brain decides when to trigger AI dialogue.
2. The brain calls `ai_dialogue.listener(...)` or `ai_dialogue.idle(...)`.
3. Moongate resolves the NPC prompt file and current memory summary.
4. A dialogue request is built with:
   - NPC name
   - prompt text
   - compact memory
   - nearby player names
   - player speech text for listener triggers
5. The expensive OpenAI request is scheduled off the game loop.
6. The model returns structured JSON:
   - `should_speak`
   - `speech_text`
   - `memory_summary`
   - `mood`
7. Back on the game loop, Moongate:
   - saves updated memory if present
   - makes the NPC speak if `should_speak` is true

This keeps the shard responsive while still letting the NPC behave intelligently.

## Runtime Components

The main pieces are:

- Lua brain script
  - decides when to ask for AI dialogue
- `ai_dialogue` Lua module
  - entry point exposed to scripts
- `NpcDialogueService`
  - builds request context and applies final response
- `OpenAiNpcDialogueClient`
  - talks to `openai-dotnet`
- `NpcAiPromptService`
  - loads persona prompt files from disk
- `NpcAiMemoryService`
  - loads and saves compact NPC memory summaries
- `IAsyncWorkSchedulerService`
  - runs slow LLM work in background and posts completion back to the game loop

## Listener vs Idle

`ai_dialogue.listener(npc, sender, text)` is for reactive speech:

- a nearby player says something
- the NPC may answer that specific speech
- the sender name and heard text are included in the request

`ai_dialogue.idle(npc)` is for autonomous chatter:

- used from a brain loop or timer
- only runs when players are nearby
- the NPC may say a short in-character line even if nobody just spoke

## Prompt and Memory Roles

Prompt files and memory files do different jobs:

- prompt file
  - static persona and roleplay rules
  - checked into the repo
- memory file
  - compact evolving summary for one NPC instance
  - updated at runtime

In practice:

- `lilly.txt` defines who Lilly is
- `0x0016A5.txt` stores what that spawned Lilly currently remembers

This separation keeps the persona stable while still letting the NPC learn and remember.

## Runtime Behavior

- If `Llm.IsEnabled` is `false`, AI dialogue stays silent.
- If the prompt file is missing, the NPC will not call OpenAI.
- If OpenAI fails or returns no speech, the NPC stays silent unless your Lua brain provides a fallback.
- Idle chatter only runs when players are nearby and cooldown allows it.

## Async Execution Model

`ai_dialogue.listener(...)` and `ai_dialogue.idle(...)` do not call OpenAI inline on the Lua brain tick.

Instead, Moongate uses a two-stage async flow:

1. The Lua brain schedules dialogue generation work.
2. The OpenAI request runs on a background worker.
3. The final result is posted back onto the game loop.
4. Only the compact world-state changes happen on the game loop:
   - save updated NPC memory
   - make the NPC speak

This matters because slow LLM calls would otherwise block the timer phase and stall the whole server.

Current behavior:

- one in-flight AI request per NPC
- listener speech is queued per NPC and processed in order
- idle requests still respect the single in-flight guard
- replies can arrive slightly later, which is expected and preferred over blocking the shard

Under the hood this is powered by `IAsyncWorkSchedulerService`, a reusable queue abstraction built on top of the background job service.

### Benchmark

The benchmark suite includes `NpcDialogueSchedulingBenchmark` to measure the cost of the async dialogue scheduling path without putting OpenAI latency on the game loop.

Run it with:

```bash
dotnet run --project benchmarks/Moongate.Benchmarks/Moongate.Benchmarks.csproj -c Release -- --filter "*NpcDialogueSchedulingBenchmark*" --job Dry
```

Latest measured dry-run values on Apple M4 Max / .NET 10:

- `QueueListener_EnqueueOnly`
  - median: `2.729 us`
  - mean: `183.0 us`
  - max first-iteration outlier: `2.133 ms`
  - allocated: `592 B`
- `ScheduleAndComplete_SingleNpc`
  - median: `1.170 ms`
  - mean: `1.258 ms`
  - max first-iteration outlier: `2.169 ms`
  - allocated: `1552 B`
- `RejectDuplicate_InFlight`
  - median: `1.270 ms`
  - mean: `1.073 ms`
  - max first-iteration outlier: `2.696 ms`
  - allocated: `1288 B`

These `Dry` results are intentionally cold-start heavy, so the median is the more useful value for the steady-state scheduling path.

## Current Scope

The current v1 integration is focused on dialogue only:

- no autonomous gameplay actions
- no combat decisions
- no world mutation from model output

That keeps the system debuggable and safe while still making NPCs feel more alive.
