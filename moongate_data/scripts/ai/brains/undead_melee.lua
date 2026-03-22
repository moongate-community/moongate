undead_melee = {}

local SEARCH_RANGE = 10
local TICK_DELAY_MS = 2000

function undead_melee.brain_loop(npc_serial)
    while true do
        local npc = mobile.get(npc_serial)

        if npc == nil then
            coroutine.yield(TICK_DELAY_MS)
        else
            local target_serial = perception.find_nearest_enemy(npc_serial, SEARCH_RANGE)

            if target_serial ~= nil and target_serial > 0 then
                local target = mobile.get(target_serial)

                if target ~= nil then
                    npc:set_target(target)
                    npc:set_war_mode(true)
                    combat.set_target(npc_serial, target_serial)
                end
            else
                combat.clear_target(npc_serial)
            end

            coroutine.yield(2000)
        end
    end
end

function undead_melee.on_death(_by_character, _context)
end
