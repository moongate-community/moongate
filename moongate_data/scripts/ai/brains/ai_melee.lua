local fsm = require("ai.modernuo.fsm")
local movement = require("ai.modernuo.movement")
local targeting = require("ai.modernuo.targeting")

ai_melee = {}

local DEFAULT_PERCEPTION_RANGE = 10
local DEFAULT_FIGHT_RANGE = 1
local WANDER_RADIUS = 4
local TICK_DELAY_MS = 1500

local function clear_target(npc_serial, npc)
    fsm.clear_target(npc_serial)
    fsm.set_action(npc_serial, fsm.actions.wander)
    combat.clear_target(npc_serial)

    if npc ~= nil then
        npc:set_war_mode(false)
    end
end

function ai_melee.on_think(npc_serial)
    while true do
        local npc = mobile.get(npc_serial)

        if npc ~= nil then
            local range_perception = targeting.get_perception_range(npc_serial, DEFAULT_PERCEPTION_RANGE)
            local range_fight = mobile.get_ai_range_fight(npc_serial) or DEFAULT_FIGHT_RANGE
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
                    movement.combat(npc_serial, target_serial, range_fight)
                else
                    clear_target(npc_serial, npc)
                    movement.wander(npc_serial, WANDER_RADIUS)
                end
            else
                clear_target(npc_serial, npc)
                movement.wander(npc_serial, WANDER_RADIUS)
            end
        end

        coroutine.yield(TICK_DELAY_MS)
    end
end

function ai_melee.on_death(_by_character, _context)
end
