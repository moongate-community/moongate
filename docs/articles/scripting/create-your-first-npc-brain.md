# Create Your First NPC Brain

This guide creates a small Lua brain that makes an NPC talk on its own and answer when a nearby player says `hello`.

By the end, you will:

- add one Lua file under `~/moongate/scripts/ai/npcs/`
- register it in `~/moongate/scripts/ai/init.lua`
- bind it from an NPC template in the next guide

## Before You Start

- your shard root is `~/moongate`
- your server is configured to run against `~/moongate`
- your shard already has `~/moongate/scripts/ai/init.lua`
- if the server is running while you edit these files, use a full restart for this tutorial path

## Step 1: Create The Brain File

Create this file:

```text
~/moongate/scripts/ai/npcs/tutorial_cat_brain.lua
```

Paste this content:

```lua
local tick = require("common.tick")

tutorial_cat_brain = {}

local state_by_npc = {}

local function get_state(npc_id)
    local state = state_by_npc[npc_id]
    if state ~= nil then
        return state
    end

    state = {
        cadence = tick.state({
            speech = 4000,
        }, time.now_ms()),
    }

    state_by_npc[npc_id] = state
    return state
end

function tutorial_cat_brain.on_think(npc_id)
    while true do
        local npc = mobile.get(npc_id)

        if npc ~= nil then
            local state = get_state(npc_id)
            local now = time.now_ms()

            tick.run(state.cadence, "speech", now, function()
                npc:say("Hello! My first Moongate brain is running.")
            end)
        end

        coroutine.yield(250)
    end
end

function tutorial_cat_brain.on_speech(npc_id, _speaker_id, text, _speech_type, _map_id, _x, _y, _z)
    local npc = mobile.get(npc_id)
    if npc == nil or text == nil then
        return
    end

    if string.find(string.lower(text), "hello", 1, true) then
        npc:say("Hello back to you.")
    end
end
```

What this brain does:

- `on_think` runs as the NPC coroutine loop
- `get_state` creates one cadence state per spawned NPC
- every 4 seconds it says a line overhead
- `on_speech` reacts to nearby speech and looks for the word `hello`

Why the `state_by_npc` table matters:

- if you spawn more than one copy of this NPC, each one keeps its own timer
- this matches the safer pattern already used by more advanced brains in the repo

Why there is only one `require(...)`:

- `common.tick` is a Lua helper script, so you load it with `require`
- `mobile`, `time`, `coroutine`, and `string` are already available when the Moongate Lua runtime starts

## Step 2: Register The Brain In `ai/init.lua`

Open:

```text
~/moongate/scripts/ai/init.lua
```

Add this line with the other brain or NPC requires:

```lua
require("ai.npcs.tutorial_cat_brain")
```

Why this step matters:

- creating the file alone is not enough
- `ai/init.lua` is the place that loads the Lua brain modules used by the server

## Step 3: Restart The Server

Restart the server after editing both files.

This is the safest beginner path because it guarantees the new brain file and the updated `ai/init.lua` are loaded
together.

## Step 4: Bind It From An NPC Template

The brain is ready, but nothing will use it until an NPC template points to:

```json
"brain": "tutorial_cat_brain"
```

Do that in the next guide:

- [Create Your First NPC Template](create-your-first-npc-template.md)

## Common Mistakes

- Naming the Lua table differently from the `ai.brain` value you plan to use
- Creating the file but forgetting the `require("ai.npcs.tutorial_cat_brain")` line
- Reusing one shared timer table for every spawned NPC instead of keeping state per NPC
- Editing the repo copy under `moongate_data/` instead of your shard root under `~/moongate/scripts/`

## Next Step

Continue with [Create Your First NPC Template](create-your-first-npc-template.md).
