local M = {}

local DEFAULT_FIGHT_MODE = "closest"

if combat ~= nil and combat.get_attack_range == nil and combat.attack_range ~= nil then
    combat.get_attack_range = combat.attack_range
end

function M.get_fight_mode(npc_serial, fallback)
    local fight_mode = mobile.get_ai_fight_mode(npc_serial) or fallback or DEFAULT_FIGHT_MODE

    if type(fight_mode) ~= "string" or fight_mode == "" then
        return DEFAULT_FIGHT_MODE
    end

    return string.lower(fight_mode)
end

function M.get_perception_range(npc_serial, fallback)
    return mobile.get_ai_range_perception(npc_serial) or fallback or 10
end

function M.get_fight_range(npc_serial, fallback)
    return mobile.get_ai_range_fight(npc_serial) or fallback or 1
end

function M.get_attack_range(npc_serial)
    if combat ~= nil and combat.get_attack_range ~= nil then
        return combat.get_attack_range(npc_serial)
    end

    return 1
end

function M.find_hostile_target(npc_serial, range, fallback_fight_mode, players_only)
    local fight_mode = M.get_fight_mode(npc_serial, fallback_fight_mode)

    if fight_mode == "none" then
        return nil
    end

    return perception.find_best_target(npc_serial, range, fight_mode, players_only == true)
end

function M.is_target_valid(npc_serial, target_serial, leash_range)
    if target_serial == nil or target_serial <= 0 then
        return false
    end

    if mobile.get(target_serial) == nil then
        return false
    end

    if leash_range ~= nil and leash_range > 0 then
        return perception.in_range(npc_serial, target_serial, leash_range)
    end

    return true
end

return M
