local fsm = require("ai.modernuo.fsm")
local movement = require("ai.modernuo.movement")

ai_vendor = {}

local FLEE_RANGE = 8
local TICK_DELAY_MS = 2000

local function resolve_npc_serial(event_obj)
    if type(event_obj) ~= "table" then
        return 0
    end

    return tonumber(event_obj.listener_npc_id or event_obj.mobile_id or 0) or 0
end

local function handle_threat(npc_serial, threat_serial)
    local npc = mobile.get(npc_serial)
    local threat = mobile.get(threat_serial)

    if npc == nil or threat == nil then
        return
    end

    fsm.set_target(npc_serial, threat_serial)
    fsm.set_action(npc_serial, fsm.actions.flee)
    combat.clear_target(npc_serial)
    npc:set_war_mode(false)
    npc:say("Guards!")
    movement.flee(npc_serial, threat_serial, FLEE_RANGE)
end

function ai_vendor.brain_loop(npc_serial)
    while true do
        local npc = mobile.get(npc_serial)

        if npc ~= nil then
            local fight_mode = mobile.get_ai_fight_mode(npc_serial) or "none"
            local threat_serial = fsm.get_target(npc_serial)

            if fight_mode == "none" and threat_serial == nil then
                fsm.set_action(npc_serial, fsm.actions.interact)
                movement.interact(npc_serial)
            elseif threat_serial ~= nil and mobile.get(threat_serial) ~= nil then
                fsm.set_action(npc_serial, fsm.actions.flee)
                movement.flee(npc_serial, threat_serial, FLEE_RANGE)
            else
                fsm.clear_target(npc_serial)
                fsm.set_action(npc_serial, fsm.actions.interact)
                movement.interact(npc_serial)
            end
        end

        coroutine.yield(TICK_DELAY_MS)
    end
end

function ai_vendor.on_event(event_type, from_serial, event_obj)
    local npc_serial = resolve_npc_serial(event_obj)

    if npc_serial <= 0 then
        return
    end

    if event_type == "in_range" and type(event_obj) == "table" and event_obj.source_is_enemy == true then
        handle_threat(npc_serial, from_serial)
        return
    end

    if event_type == "attacked" or event_type == "combat" then
        handle_threat(npc_serial, from_serial)
    end
end

function ai_vendor.on_in_range(npc_serial, source_serial, event_obj)
    if type(event_obj) == "table" and event_obj.source_is_enemy == true then
        handle_threat(npc_serial, source_serial)
    end
end

function ai_vendor.on_attacked(source_serial, event_obj)
    local npc_serial = resolve_npc_serial(event_obj)

    if npc_serial > 0 then
        handle_threat(npc_serial, source_serial)
    end
end

function ai_vendor.on_death(_by_character, _context)
end
