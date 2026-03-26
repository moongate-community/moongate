local M = {}

M.actions = {
    wander = "wander",
    guard = "guard",
    combat = "combat",
    flee = "flee",
    backoff = "backoff",
    interact = "interact",
}

local ACTION_KEY = "modernuo_action"
local TARGET_KEY = "modernuo_target_serial"

function M.set_action(npc_serial, action)
    if action == nil or action == "" then
        npc_state.set_var(npc_serial, ACTION_KEY, nil)
        return M.actions.wander
    end

    npc_state.set_var(npc_serial, ACTION_KEY, action)
    return action
end

function M.get_action(npc_serial)
    return npc_state.get_var(npc_serial, ACTION_KEY) or M.actions.wander
end

function M.set_target(npc_serial, target_serial)
    if target_serial == nil or target_serial <= 0 then
        npc_state.set_var(npc_serial, TARGET_KEY, nil)
        return nil
    end

    npc_state.set_var(npc_serial, TARGET_KEY, target_serial)
    return target_serial
end

function M.get_target(npc_serial)
    local target_serial = tonumber(npc_state.get_var(npc_serial, TARGET_KEY) or 0) or 0

    if target_serial <= 0 then
        return nil
    end

    return target_serial
end

function M.clear_target(npc_serial)
    npc_state.set_var(npc_serial, TARGET_KEY, nil)
end

return M
