local fsm = require("ai.runtime.fsm")
local movement = require("ai.runtime.movement")
local targeting = require("ai.runtime.targeting")

ai_berserk = {}

local DEFAULT_PERCEPTION_RANGE = 12
local AGGRESSIVE_STOP_RANGE = 0
local TICK_DELAY_MS = 1000

local function clear_target(npc_serial, npc)
    fsm.clear_target(npc_serial)
    fsm.set_action(npc_serial, fsm.actions.guard)
    combat.clear_target(npc_serial)

    if npc ~= nil then
        npc:set_war_mode(false)
    end
end

function ai_berserk.on_think(npc_serial)
    while true do
        local npc = mobile.get(npc_serial)

        if npc ~= nil then
            local range_perception = targeting.get_perception_range(npc_serial, DEFAULT_PERCEPTION_RANGE)
            local fight_mode = targeting.get_fight_mode(npc_serial, "closest")
            local target_serial = fsm.get_target(npc_serial)

            if not targeting.is_target_valid(npc_serial, target_serial, range_perception + 2) then
                target_serial = perception.find_best_target(npc_serial, range_perception, fight_mode)
            end

            if target_serial ~= nil and target_serial > 0 then
                local target = mobile.get(target_serial)

                if target ~= nil then
                    fsm.set_target(npc_serial, target_serial)
                    fsm.set_action(npc_serial, fsm.actions.combat)
                    npc:set_target(target)
                    npc:set_war_mode(true)
                    combat.set_target(npc_serial, target_serial)
                    movement.combat(npc_serial, target_serial, AGGRESSIVE_STOP_RANGE)
                else
                    clear_target(npc_serial, npc)
                end
            else
                clear_target(npc_serial, npc)
            end
        end

        coroutine.yield(TICK_DELAY_MS)
    end
end

function ai_berserk.on_death(_by_character, _context)
end
