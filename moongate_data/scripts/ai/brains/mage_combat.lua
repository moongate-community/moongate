mage_combat = {}

local SEARCH_RANGE = 12
local TICK_DELAY_MS = 2000
local PREFERRED_RANGE = 8
local EVADE_THRESHOLD = 4
local WANDER_RADIUS = 4

function mage_combat.on_think(npc_serial)
    while true do
        local npc = mobile.get(npc_serial)

        if npc == nil then
            coroutine.yield(TICK_DELAY_MS)
        else
            local target_serial = perception.find_nearest_player_enemy(npc_serial, SEARCH_RANGE)

            if target_serial ~= nil and target_serial > 0 then
                local target = mobile.get(target_serial)

                if target ~= nil then
                    local dist = perception.distance(npc_serial, target_serial)

                    npc:set_target(target)
                    npc:set_war_mode(true)
                    combat.set_target(npc_serial, target_serial)

                    if dist < EVADE_THRESHOLD then
                        steering.evade(npc_serial, target_serial)
                    else
                        steering.follow(npc_serial, target_serial, PREFERRED_RANGE)
                    end
                end
            else
                combat.clear_target(npc_serial)
                npc:set_war_mode(false)
                steering.wander(npc_serial, WANDER_RADIUS)
            end

            coroutine.yield(TICK_DELAY_MS)
        end
    end
end

function mage_combat.on_death(_by_character, _context)
end
