local behavior = require("ai.behavior")

local HP_THRESHOLD_KEY = "self_bandage_hp_threshold"
local SCORE_BONUS_KEY = "self_bandage_score_bonus"

local M = {}

function M.score(npc_serial, _ctx)
    if healing.is_bandaging(npc_serial) == true then
        return 95
    end

    if healing.has_bandage(npc_serial) ~= true then
        return 0
    end

    local hp_percent = npc_state.get_hp_percent(npc_serial)
    local hp_threshold = tonumber(npc_state.get_var(npc_serial, HP_THRESHOLD_KEY) or 0.45) or 0.45
    if hp_percent <= 0 or hp_percent > hp_threshold then
        return 0
    end

    local score_bonus = tonumber(npc_state.get_var(npc_serial, SCORE_BONUS_KEY) or 70) or 70
    local missing_hp_bonus = math.floor((1.0 - hp_percent) * 20)

    return score_bonus + missing_hp_bonus
end

function M.run(npc_serial, _ctx)
    if healing.is_bandaging(npc_serial) == true then
        return 500
    end

    if healing.begin_self_bandage(npc_serial) == true then
        return 500
    end

    return 250
end

behavior.register("self_bandage", M)

return M
