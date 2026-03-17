# Tick Helper

`common.tick` is a small Lua helper for NPC brains and other coroutine-based scripts that need
simple recurring cadences without repeating `last_*_ms` boilerplate.

It is a good fit for:

- periodic movement
- ambient speech
- idle sounds
- simple state progression timers

It is not meant for:

- persistent world timers
- background work
- gameplay state that must survive reloads

## Why Use It

Without `common.tick`, Lua brains usually drift toward this pattern:

```lua
local MOVE_INTERVAL_MS = 5000
local last_move_ms = 0

local now = time.now_ms()
if now - last_move_ms >= MOVE_INTERVAL_MS then
    npc:move(random.direction())
    last_move_ms = now
end
```

That works, but once a brain has multiple cadences it becomes noisy quickly.

`common.tick` keeps the same behavior while moving the timing bookkeeping into one shared helper.

## API

```lua
local tick = require("common.tick")
```

### `tick.state(intervals, start_ms?)`

Creates cadence state for one or more named intervals.

```lua
local cadence = tick.state({
    move = 5000,
    speech = 2000,
    sound = 3000,
})
```

### `tick.ready(state, key, now_ms)`

Returns `true` when the named cadence is ready to fire.

### `tick.run(state, key, now_ms, action?)`

Runs the cadence if ready, updates the internal timestamp, and executes `action`.
Returns `true` when the cadence fired.

### `tick.reset(state, key, now_ms, interval_ms?)`

Resets a cadence timer and optionally changes its interval.
Useful for state machines where the next wait duration depends on the current state.

## Use Case: Ambient NPC Brain

[orion.lua](/Users/squid/projects/personal/moongatev2/moongate_data/scripts/ai/npcs/orion.lua) uses three
independent cadences:

```lua
local tick = require("common.tick")

local state = {
    cadence = tick.state({
        move = 5000,
        speech = 2000,
        sound = 3000,
    }),
}

function orion.brain_loop(npc_id)
    while true do
        local npc = mobile.get(npc_id)

        if npc ~= nil then
            local now = time.now_ms()

            tick.run(state.cadence, "move", now, function()
                npc:move(random.direction())
            end)

            tick.run(state.cadence, "speech", now, function()
                local message = random.element(MESSAGES)
                if message ~= nil then
                    npc:say(message)
                end
            end)

            tick.run(state.cadence, "sound", now, function()
                local sound_id = random.element(SOUNDS)
                if sound_id ~= nil then
                    npc:play_sound(sound_id)
                end
            end)
        end

        coroutine.yield(250)
    end
end
```

This keeps the brain focused on behavior, not timer bookkeeping.

## Use Case: State Machine Durations

[test_state_brain.lua](/Users/squid/projects/personal/moongatev2/moongate_data/scripts/ai/brains/test_state_brain.lua)
combines `libs.statemachine` with `common.tick`.

Instead of storing `entered_at_ms` and manually checking each state's elapsed time, it uses one
named cadence:

```lua
local state = {
    machine = machine,
    cadence = tick.state({
        advance = IDLE_DURATION_MS,
    }, time.now_ms()),
}

local function enter_state(state, duration_ms)
    tick.reset(state.cadence, "advance", time.now_ms(), duration_ms)
end
```

Then the loop only asks whether the state should advance:

```lua
tick.run(state.cadence, "advance", now, function()
    if machine:is("idle") then
        if machine:start_wander() then
            enter_state(state, WANDER_DURATION_MS)
            npc:move(random.direction())
        end

        return
    end

    if machine:is("wander") then
        if machine:start_speak() then
            enter_state(state, SPEAK_DURATION_MS)
            local message = random.element(MESSAGES)
            if message ~= nil then
                npc:say(message)
            end
        end

        return
    end

    if machine:is("speak") and machine:finish_speak() then
        enter_state(state, IDLE_DURATION_MS)
    end
end)
```

That is still explicit, but it removes repetitive `elapsed >= X` checks and keeps state duration
changes in one place.
