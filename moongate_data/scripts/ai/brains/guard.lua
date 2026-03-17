-- guard.lua
-- Utility/priority brain with isolated behaviors:
-- - "evade": run away when a threat is near or HP is low
-- - "follow": follow a target set in the blackboard
-- - "idle": fallback (wander)

local utility_runner = require("ai.runners.utility_runner")

guard = {}

-- Logical behavior order. The winner is selected by score.
local BEHAVIORS = {
    "evade",
    "follow",
    "idle",
}

-- Initialize minimal blackboard values for behaviors.
local function initialize_defaults(npc_serial)
    -- Minimum distance while following a target
    npc_state.set_var(npc_serial, "follow_stop_range", 1)

    -- Desired distance while evading
    npc_state.set_var(npc_serial, "evade_desired_range", 7)

    -- Below this HP threshold, evade gets a score bonus
    npc_state.set_var(npc_serial, "evade_hp_threshold", 0.40)
end

function guard.brain_loop(npc_serial)
    initialize_defaults(npc_serial)

    while true do
        -- Context object available to behaviors
        local ctx = {
            now_ms = time.now_ms(),

            -- Prevent rapid behavior flipping for a short window
            min_hold_ms = 600,
        }

        -- Select best-score behavior and execute run()
        local delay_ms = utility_runner.tick(npc_serial, ctx, BEHAVIORS)

        -- The brain decides the next tick interval
        coroutine.yield(delay_ms)
    end
end

function guard.on_event(event_type, from_serial, event_obj)
    local npc_serial = 0

    -- Standard event payload: listener_npc_id for speech/in_range/out_range.
    if type(event_obj) == "table" then
        npc_serial = tonumber(event_obj.listener_npc_id or event_obj.mobile_id or 0) or 0
    end

    if npc_serial <= 0 then
        return
    end

    -- Practical example:
    -- if someone speaks near the guard, set them as follow target.
    if event_type == "speech_heard" then
        local target_serial = tonumber(from_serial) or 0
        if target_serial > 0 then
            npc_state.set_var(npc_serial, "follow_target_serial", target_serial)
        end
    end

    -- Forward the event to the active behavior (if it implements on_event)
    utility_runner.on_event(npc_serial, {}, event_type, from_serial, event_obj)
end

function guard.on_death(by_character, context)
    -- Optional hook: keep empty for now.
    -- Future example:
    -- log.info("Guard died. killer={0}", tostring(by_character))
end
