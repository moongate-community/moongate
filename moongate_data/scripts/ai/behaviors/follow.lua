local behavior = require("ai.behavior")

local FOLLOW_TARGET_KEY = "follow_target_serial"
local FOLLOW_STOP_RANGE_KEY = "follow_stop_range"

local M = {}

function M.score(npc_serial, _ctx)
    local target_serial = tonumber(npc_state.get_var(npc_serial, FOLLOW_TARGET_KEY) or 0)
    if target_serial <= 0 then
        return 0
    end

    local distance = perception.distance(npc_serial, target_serial)
    if distance < 0 then
        return 0
    end

    return 40 - math.min(distance, 30)
end

function M.run(npc_serial, _ctx)
    local target_serial = tonumber(npc_state.get_var(npc_serial, FOLLOW_TARGET_KEY) or 0)
    if target_serial <= 0 then
        return 250
    end

    local stop_range = tonumber(npc_state.get_var(npc_serial, FOLLOW_STOP_RANGE_KEY) or 1) or 1
    steering.follow(npc_serial, target_serial, math.max(0, math.floor(stop_range)))
    return 250
end

function M.on_event(npc_serial, _ctx, event_type, from_serial, _event_obj)
    if event_type ~= "in_range" then
        return
    end

    local current_target = tonumber(npc_state.get_var(npc_serial, FOLLOW_TARGET_KEY) or 0)
    if current_target > 0 then
        return
    end

    npc_state.set_var(npc_serial, FOLLOW_TARGET_KEY, tonumber(from_serial) or 0)
end

behavior.register("follow", M)

return M
