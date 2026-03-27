berserk_combat = {}

local SEARCH_RANGE = 12
local TICK_DELAY_MS = 1000
local FOLLOW_STOP_RANGE = 1

function berserk_combat.on_think(npc_serial)
    while true do
        local npc = mobile.get(npc_serial)

        if npc == nil then
            coroutine.yield(TICK_DELAY_MS)
        else
            local target_serial = perception.find_nearest_player_enemy(npc_serial, SEARCH_RANGE)

            if target_serial ~= nil and target_serial > 0 then
                local target = mobile.get(target_serial)

                if target ~= nil then
                    npc:set_target(target)
                    npc:set_war_mode(true)
                    combat.set_target(npc_serial, target_serial)
                    steering.follow(npc_serial, target_serial, FOLLOW_STOP_RANGE)
                end
            else
                combat.clear_target(npc_serial)
                npc:set_war_mode(false)
            end

            coroutine.yield(TICK_DELAY_MS)
        end
    end
end

function berserk_combat.on_death(_by_character, _context)
end
