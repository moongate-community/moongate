local behavior_registry = require("ai.behavior")

local M = {}

local ACTIVE_BEHAVIOR_KEY = "active_behavior"
local HOLD_UNTIL_KEY = "active_behavior_hold_until_ms"

local function to_int(value, fallback)
    local number = tonumber(value)
    if number == nil then
        return fallback
    end
    return math.floor(number)
end

local function pick_best_behavior(npc_serial, ctx, behavior_ids)
    local best_id = nil
    local best_behavior = nil
    local best_score = -math.huge

    for _, behavior_id in ipairs(behavior_ids) do
        local behavior = behavior_registry.get(behavior_id)
        if behavior ~= nil and type(behavior.score) == "function" then
            local ok, raw_score = pcall(behavior.score, npc_serial, ctx)
            local score = ok and tonumber(raw_score) or nil

            if score ~= nil and score > best_score then
                best_score = score
                best_id = behavior_id
                best_behavior = behavior
            end
        end
    end

    return best_id, best_behavior
end

function M.tick(npc_serial, ctx, behavior_ids)
    if type(behavior_ids) ~= "table" or #behavior_ids == 0 then
        return 250
    end

    local now_ms = to_int((ctx and ctx.now_ms) or time.now_ms(), time.now_ms())
    local min_hold_ms = to_int((ctx and ctx.min_hold_ms) or 600, 600)

    local active_behavior_id = npc_state.get_var(npc_serial, ACTIVE_BEHAVIOR_KEY)
    local hold_until_ms = to_int(npc_state.get_var(npc_serial, HOLD_UNTIL_KEY), 0)

    if type(active_behavior_id) == "string" and active_behavior_id ~= "" and now_ms < hold_until_ms then
        local active_behavior = behavior_registry.get(active_behavior_id)
        if active_behavior ~= nil and type(active_behavior.run) == "function" then
            local ok, delay = pcall(active_behavior.run, npc_serial, ctx or {})
            if ok then
                return math.max(1, to_int(delay, 250))
            end
        end
    end

    local best_id, best_behavior = pick_best_behavior(npc_serial, ctx or {}, behavior_ids)
    if best_behavior == nil or type(best_behavior.run) ~= "function" then
        return 250
    end

    npc_state.set_var(npc_serial, ACTIVE_BEHAVIOR_KEY, best_id)
    npc_state.set_var(npc_serial, HOLD_UNTIL_KEY, now_ms + min_hold_ms)

    local ok, delay = pcall(best_behavior.run, npc_serial, ctx or {})
    if not ok then
        return 250
    end

    return math.max(1, to_int(delay, 250))
end

function M.on_event(npc_serial, ctx, event_type, from_serial, event_obj)
    local active_behavior_id = npc_state.get_var(npc_serial, ACTIVE_BEHAVIOR_KEY)
    if type(active_behavior_id) ~= "string" or active_behavior_id == "" then
        return
    end

    local active_behavior = behavior_registry.get(active_behavior_id)
    if active_behavior == nil or type(active_behavior.on_event) ~= "function" then
        return
    end

    pcall(active_behavior.on_event, npc_serial, ctx or {}, event_type, from_serial, event_obj)
end

return M
