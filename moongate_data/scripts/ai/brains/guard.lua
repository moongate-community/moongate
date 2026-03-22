-- guard.lua
-- Utility/priority brain with isolated behaviors:
-- - "evade": run away when a threat is near or HP is low
-- - "follow": follow a target set in the blackboard
-- - "ranged_keep_distance": maintain a 4-6 tile firing band for ranged guards
-- - "idle": fallback (wander)

local utility_runner = require("ai.runners.utility_runner")

guard = {}

local function get_seen_key(source_serial)
    return "guard_seen_" .. tostring(source_serial)
end

local function get_engaged_key(source_serial)
    return "guard_engaged_" .. tostring(source_serial)
end

-- Logical behavior order. The winner is selected by score.
local MELEE_BEHAVIORS = {
    "self_bandage",
    "evade",
    "follow",
    "idle",
}

local RANGED_BEHAVIORS = {
    "self_bandage",
    "evade",
    "ranged_keep_distance",
    "idle",
}

local function is_ranged_guard(npc_serial)
    return npc_state.get_var(npc_serial, "guard_role") == "ranged"
end

local function get_behaviors(npc_serial)
    if is_ranged_guard(npc_serial) then
        return RANGED_BEHAVIORS
    end

    return MELEE_BEHAVIORS
end

-- Initialize minimal blackboard values for behaviors.
local function initialize_defaults(npc_serial)
    local function set_default(key, value)
        if npc_state.get_var(npc_serial, key) == nil then
            npc_state.set_var(npc_serial, key, value)
        end
    end

    -- Guards should not kite by default. Ranged positioning is handled by
    -- ranged_keep_distance, and low-hp retreat is handled by evade_hp_threshold.
    set_default("evade_desired_range", 0)

    -- Below this HP threshold, evade gets a score bonus
    set_default("evade_hp_threshold", 0.40)
    set_default("self_bandage_hp_threshold", 0.45)
    set_default("self_bandage_score_bonus", 70)

    if is_ranged_guard(npc_serial) then
        set_default("preferred_min_range", 4)
        set_default("preferred_max_range", 6)
        return
    end

    -- Minimum distance while following a target
    set_default("follow_stop_range", 1)
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
        local delay_ms = utility_runner.tick(npc_serial, ctx, get_behaviors(npc_serial))

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

function guard.on_in_range(npc_serial, source_serial, event_obj)
    local npc = mobile.get(npc_serial)
    local source = mobile.get(source_serial)
    if npc == nil or source == nil or type(event_obj) ~= "table" then
        return
    end

    local seen_key = get_seen_key(source_serial)
    local engaged_key = get_engaged_key(source_serial)
    local source_name = tostring(event_obj.source_name or source.name or "")
    local source_is_player = event_obj.source_is_player == true
    local source_is_enemy = event_obj.source_is_enemy == true

    if source_is_player and source_name ~= "" and npc_state.get_var(npc_serial, seen_key) ~= true then
        npc:say("Hello, " .. source_name .. ", How do you feel today?")
        npc_state.set_var(npc_serial, seen_key, true)
    end

    if source_is_enemy and npc_state.get_var(npc_serial, engaged_key) ~= true then
        npc:set_target(source)
        npc:set_war_mode(true)
        if combat.set_target(npc_serial, source_serial) == true then
            npc_state.set_var(npc_serial, "follow_target_serial", source_serial)
            npc_state.set_var(npc_serial, engaged_key, true)
        end
    end

    utility_runner.on_event(npc_serial, {}, "in_range", source_serial, event_obj)
end

function guard.on_out_range(npc_serial, source_serial, event_obj)
    npc_state.set_var(npc_serial, get_seen_key(source_serial), nil)
    npc_state.set_var(npc_serial, get_engaged_key(source_serial), nil)
    npc_state.set_var(npc_serial, "follow_target_serial", nil)
    combat.clear_target(npc_serial)
    utility_runner.on_event(npc_serial, {}, "out_range", source_serial, event_obj)
end

function guard.on_death(by_character, context)
    -- Optional hook: keep empty for now.
    -- Future example:
    -- log.info("Guard died. killer={0}", tostring(by_character))
end
