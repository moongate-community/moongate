local behavior = require("ai.behavior")

local FOLLOW_TARGET_KEY = "follow_target_serial"
local HOME_X_KEY = "home_x"
local HOME_Y_KEY = "home_y"
local HOME_Z_KEY = "home_z"
local HOLD_RADIUS_KEY = "hold_radius"

local M = {}

function M.score(npc_serial, _ctx)
    local target_serial = tonumber(npc_state.get_var(npc_serial, FOLLOW_TARGET_KEY) or 0)
    if target_serial > 0 then
        return 0
    end

    local home_x = tonumber(npc_state.get_var(npc_serial, HOME_X_KEY))
    local home_y = tonumber(npc_state.get_var(npc_serial, HOME_Y_KEY))
    if home_x == nil or home_y == nil then
        return 0
    end

    local npc = mobile.get(npc_serial)
    if npc == nil then
        return 0
    end

    local hold_radius = tonumber(npc_state.get_var(npc_serial, HOLD_RADIUS_KEY) or 1) or 1
    local out_of_home_range = math.abs(npc.location_x - home_x) > hold_radius or
        math.abs(npc.location_y - home_y) > hold_radius

    return out_of_home_range and 35 or 0
end

function M.run(npc_serial, _ctx)
    local home_x = tonumber(npc_state.get_var(npc_serial, HOME_X_KEY))
    local home_y = tonumber(npc_state.get_var(npc_serial, HOME_Y_KEY))
    local home_z = tonumber(npc_state.get_var(npc_serial, HOME_Z_KEY) or 0) or 0
    if home_x == nil or home_y == nil then
        return 400
    end

    local hold_radius = tonumber(npc_state.get_var(npc_serial, HOLD_RADIUS_KEY) or 1) or 1
    steering.move_to(npc_serial, math.floor(home_x), math.floor(home_y), math.floor(home_z), math.max(0, math.floor(hold_radius)))
    return 250
end

behavior.register("return_home", M)

return M
