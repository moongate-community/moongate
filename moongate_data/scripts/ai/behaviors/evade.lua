local behavior = require("ai.behavior")

local EVADE_FROM_KEY = "evade_from_serial"
local EVADE_RANGE_KEY = "evade_desired_range"
local EVADE_HP_THRESHOLD_KEY = "evade_hp_threshold"

local M = {}

function M.score(npc_serial, _ctx)
    local threat_serial = tonumber(npc_state.get_var(npc_serial, EVADE_FROM_KEY) or 0)
    if threat_serial <= 0 then
        return 0
    end

    local distance = perception.distance(npc_serial, threat_serial)
    if distance < 0 then
        return 0
    end

    local desired_range = tonumber(npc_state.get_var(npc_serial, EVADE_RANGE_KEY) or 6) or 6
    local hp_threshold = tonumber(npc_state.get_var(npc_serial, EVADE_HP_THRESHOLD_KEY) or 0.45) or 0.45
    local hp_percent = npc_state.get_hp_percent(npc_serial)
    local low_hp_bonus = hp_percent <= hp_threshold and 80 or 0
    local proximity_bonus = math.max(0, desired_range - distance) * 10

    return low_hp_bonus + proximity_bonus
end

function M.run(npc_serial, _ctx)
    local threat_serial = tonumber(npc_state.get_var(npc_serial, EVADE_FROM_KEY) or 0)
    if threat_serial <= 0 then
        return 250
    end

    local desired_range = tonumber(npc_state.get_var(npc_serial, EVADE_RANGE_KEY) or 6) or 6
    steering.evade(npc_serial, threat_serial, math.max(1, math.floor(desired_range)))
    return 250
end

function M.on_event(npc_serial, _ctx, event_type, from_serial, _event_obj)
    if event_type == "out_range" then
        local current_threat = tonumber(npc_state.get_var(npc_serial, EVADE_FROM_KEY) or 0)
        if current_threat == tonumber(from_serial) then
            npc_state.set_var(npc_serial, EVADE_FROM_KEY, nil)
        end

        return
    end

    if event_type == "speech_heard" or event_type == "in_range" then
        local current_threat = tonumber(npc_state.get_var(npc_serial, EVADE_FROM_KEY) or 0)
        if current_threat <= 0 then
            npc_state.set_var(npc_serial, EVADE_FROM_KEY, tonumber(from_serial) or 0)
        end
    end
end

behavior.register("evade", M)

return M
