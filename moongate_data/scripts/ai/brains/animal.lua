animal = {}

local SEARCH_RANGE = 8
local TICK_DELAY_MS = 3000
local WANDER_RADIUS = 4

function animal.on_think(npc_serial)
    while true do
        local npc = mobile.get(npc_serial)

        if npc == nil then
            coroutine.yield(TICK_DELAY_MS)
        else
            local threat_serial = perception.find_nearest_player_enemy(npc_serial, SEARCH_RANGE)

            if threat_serial ~= nil and threat_serial > 0 then
                steering.evade(npc_serial, threat_serial)
            else
                steering.wander(npc_serial, WANDER_RADIUS)
            end

            coroutine.yield(TICK_DELAY_MS)
        end
    end
end

function animal.on_death(_by_character, _context)
end
