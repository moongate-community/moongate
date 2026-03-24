local behavior = require("ai.behavior")

local FOLLOW_TARGET_KEY = "follow_target_serial"
local HOME_X_KEY = "home_x"
local HOME_Y_KEY = "home_y"
local HOME_Z_KEY = "home_z"
local LEASH_RADIUS_KEY = "leash_radius"

local M = {}

function M.score(npc_serial, _ctx)
    local target_serial = tonumber(npc_state.get_var(npc_serial, FOLLOW_TARGET_KEY) or 0)
    if target_serial <= 0 then
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

    local leash_radius = tonumber(npc_state.get_var(npc_serial, LEASH_RADIUS_KEY) or 8) or 8
    local out_of_leash = math.abs(npc.location_x - home_x) > leash_radius or
        math.abs(npc.location_y - home_y) > leash_radius

    return out_of_leash and 100 or 0
end

function M.run(npc_serial, _ctx)
    npc_state.set_var(npc_serial, FOLLOW_TARGET_KEY, nil)
    combat.clear_target(npc_serial)
    steering.stop(npc_serial)
    return 200
end

behavior.register("leash", M)

return M
