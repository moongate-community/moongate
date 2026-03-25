thief = {}

local SEARCH_RANGE = 8
local TICK_DELAY_MS = 3000
local APPROACH_RANGE = 2
local WANDER_RADIUS = 4

function thief.brain_loop(npc_serial)
    while true do
        local npc = mobile.get(npc_serial)

        if npc == nil then
            coroutine.yield(TICK_DELAY_MS)
        else
            local target_serial = perception.find_nearest_player_enemy(npc_serial, SEARCH_RANGE)

            if target_serial ~= nil and target_serial > 0 then
                local target = mobile.get(target_serial)

                if target ~= nil then
                    steering.follow(npc_serial, target_serial, APPROACH_RANGE)
                end
            else
                steering.wander(npc_serial, WANDER_RADIUS)
            end

            coroutine.yield(TICK_DELAY_MS)
        end
    end
end

function thief.on_death(_by_character, _context)
end
