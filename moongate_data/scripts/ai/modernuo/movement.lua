local M = {}

function M.wander(npc_serial, radius)
    return steering.wander(npc_serial, radius or 4)
end

function M.guard(npc_serial)
    return steering.stop(npc_serial)
end

function M.combat(npc_serial, target_serial, stop_range)
    return steering.follow(npc_serial, target_serial, stop_range or 1)
end

function M.flee(npc_serial, threat_serial, desired_range)
    return steering.evade(npc_serial, threat_serial, desired_range or 6)
end

function M.backoff(npc_serial, threat_serial, desired_range)
    return steering.evade(npc_serial, threat_serial, desired_range or 4)
end

function M.interact(npc_serial)
    return steering.stop(npc_serial)
end

return M
