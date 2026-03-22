local behavior = require("ai.behavior")

local FOLLOW_TARGET_KEY = "follow_target_serial"
local GUARD_ROLE_KEY = "guard_role"
local PREFERRED_MIN_RANGE_KEY = "preferred_min_range"
local PREFERRED_MAX_RANGE_KEY = "preferred_max_range"

local M = {}

local function get_preferred_min_range(npc_serial)
    return tonumber(npc_state.get_var(npc_serial, PREFERRED_MIN_RANGE_KEY) or 4) or 4
end

local function get_preferred_max_range(npc_serial)
    return tonumber(npc_state.get_var(npc_serial, PREFERRED_MAX_RANGE_KEY) or 6) or 6
end

function M.score(npc_serial, _ctx)
    if npc_state.get_var(npc_serial, GUARD_ROLE_KEY) ~= "ranged" then
        return 0
    end

    local target_serial = tonumber(npc_state.get_var(npc_serial, FOLLOW_TARGET_KEY) or 0)
    if target_serial <= 0 then
        return 0
    end

    local distance = perception.distance(npc_serial, target_serial)
    if distance < 0 then
        return 0
    end

    local preferred_min_range = get_preferred_min_range(npc_serial)
    local preferred_max_range = get_preferred_max_range(npc_serial)

    if distance < preferred_min_range then
        return 80 + (preferred_min_range - distance) * 10
    end

    if distance > preferred_max_range then
        return 50 + math.min(distance - preferred_max_range, 20) * 5
    end

    return 25
end

function M.run(npc_serial, _ctx)
    local target_serial = tonumber(npc_state.get_var(npc_serial, FOLLOW_TARGET_KEY) or 0)
    if target_serial <= 0 then
        return 250
    end

    if combat.set_target(npc_serial, target_serial) ~= true then
        npc_state.set_var(npc_serial, FOLLOW_TARGET_KEY, nil)
        return 250
    end

    local distance = perception.distance(npc_serial, target_serial)
    if distance < 0 then
        return 250
    end

    local preferred_min_range = math.max(1, math.floor(get_preferred_min_range(npc_serial)))
    local preferred_max_range = math.max(preferred_min_range, math.floor(get_preferred_max_range(npc_serial)))

    if distance < preferred_min_range then
        steering.evade(npc_serial, target_serial, preferred_max_range)
        return 200
    end

    if distance > preferred_max_range then
        steering.follow(npc_serial, target_serial, preferred_max_range)
        return 200
    end

    steering.stop(npc_serial)
    return 200
end

behavior.register("ranged_keep_distance", M)

return M
