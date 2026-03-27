-- guard.lua
-- Guard policy lives here instead of in the generic runner.
-- Melee and ranged guards share the same focus lifecycle but diverge on spacing.

local fsm = require("ai.runtime.fsm")
local movement = require("ai.runtime.movement")
local targeting = require("ai.runtime.targeting")
local guards = guards

guard = {}

local HOME_X_KEY = "home_x"
local HOME_Y_KEY = "home_y"
local HOME_Z_KEY = "home_z"
local HOME_MAP_ID_KEY = "home_map_id"
local GUARD_MODE_KEY = "guard_mode"
local GUARD_ROLE_KEY = "guard_role"
local PATROL_MODE_KEY = "patrol_mode"
local PATROL_RADIUS_KEY = "patrol_radius"
local PREFERRED_MIN_RANGE_KEY = "preferred_min_range"
local PREFERRED_MAX_RANGE_KEY = "preferred_max_range"
local HOLD_RADIUS_KEY = "hold_radius"
local LEASH_RADIUS_KEY = "leash_radius"
local TICK_DELAY_MS = 1500

local function get_seen_key(source_serial)
    return "guard_seen_" .. tostring(source_serial)
end

local function get_engaged_key(source_serial)
    return "guard_engaged_" .. tostring(source_serial)
end

local function is_ranged_guard(npc_serial)
    return tostring(npc_state.get_var(npc_serial, GUARD_ROLE_KEY) or "melee"):lower() == "ranged"
end

local function set_default(npc_serial, key, value)
    if npc_state.get_var(npc_serial, key) == nil then
        npc_state.set_var(npc_serial, key, value)
    end
end

local function initialize_defaults(npc_serial)
    local npc = mobile.get(npc_serial)
    local ranged_guard = is_ranged_guard(npc_serial)

    if npc ~= nil then
        set_default(npc_serial, HOME_X_KEY, npc.location_x)
        set_default(npc_serial, HOME_Y_KEY, npc.location_y)
        set_default(npc_serial, HOME_Z_KEY, npc.location_z)
        set_default(npc_serial, HOME_MAP_ID_KEY, npc.map_id)
    end

    set_default(npc_serial, HOLD_RADIUS_KEY, 1)
    set_default(npc_serial, LEASH_RADIUS_KEY, 8)
    set_default(npc_serial, GUARD_MODE_KEY, ranged_guard and "ranged" or "melee")
    set_default(npc_serial, PREFERRED_MIN_RANGE_KEY, ranged_guard and 4 or 1)
    set_default(npc_serial, PREFERRED_MAX_RANGE_KEY, ranged_guard and 6 or 1)
end

local function get_home_values(npc_serial)
    local home_x = tonumber(npc_state.get_var(npc_serial, HOME_X_KEY))
    local home_y = tonumber(npc_state.get_var(npc_serial, HOME_Y_KEY))
    local home_z = tonumber(npc_state.get_var(npc_serial, HOME_Z_KEY))
    local home_map_id = tonumber(npc_state.get_var(npc_serial, HOME_MAP_ID_KEY))
    local hold_radius = tonumber(npc_state.get_var(npc_serial, HOLD_RADIUS_KEY) or 1) or 1
    local leash_radius = tonumber(npc_state.get_var(npc_serial, LEASH_RADIUS_KEY) or 8) or 8

    return home_x, home_y, home_z, home_map_id, hold_radius, leash_radius
end

local function get_patrol_mode(npc_serial)
    return tostring(npc_state.get_var(npc_serial, PATROL_MODE_KEY) or ""):lower()
end

local function get_patrol_radius(npc_serial)
    return tonumber(npc_state.get_var(npc_serial, PATROL_RADIUS_KEY) or 0) or 0
end

local function mark_mode(npc_serial, mode)
    npc_state.set_var(npc_serial, GUARD_MODE_KEY, mode)
end

local function clear_focus(npc_serial, npc)
    guards.set_focus(npc_serial, nil)
    fsm.clear_target(npc_serial)
    combat.clear_target(npc_serial)
    npc_state.set_var(npc_serial, GUARD_MODE_KEY, "idle")

    if npc ~= nil then
        npc:set_war_mode(false)
    end
end

local function set_focus(npc_serial, target_serial)
    guards.set_focus(npc_serial, target_serial)
    fsm.set_target(npc_serial, target_serial)
end

local move_home
local should_return_home

local function patrol_random_roam(npc_serial, npc)
    local home_x, home_y, home_z, _, _, leash_radius = get_home_values(npc_serial)
    local patrol_radius = get_patrol_radius(npc_serial)

    if home_x == nil or home_y == nil or home_z == nil or patrol_radius <= 0 then
        return false
    end

    local radius = math.max(0, math.floor(math.min(patrol_radius, leash_radius)))
    if radius <= 0 then
        return false
    end

    local origin_x = math.floor(home_x)
    local origin_y = math.floor(home_y)
    local origin_z = math.floor(home_z)
    local patrol_x = origin_x
    local patrol_y = origin_y

    for _ = 1, 8 do
        local offset_x = math.random(-radius, radius)
        local offset_y = math.random(-radius, radius)

        if (offset_x ~= 0 or offset_y ~= 0) and offset_x * offset_x + offset_y * offset_y <= radius * radius then
            patrol_x = origin_x + offset_x
            patrol_y = origin_y + offset_y
            break
        end
    end

    mark_mode(npc_serial, "patrol")
    fsm.set_action(npc_serial, fsm.actions.wander)

    local moved = steering.move_to(npc_serial, patrol_x, patrol_y, origin_z, 0)
    local current_npc = mobile.get(npc_serial)

    if current_npc ~= nil and should_return_home(npc_serial, current_npc) then
        return move_home(npc_serial, current_npc)
    end

    return moved
end

move_home = function(npc_serial, npc)
    local home_x, home_y, home_z, home_map_id, hold_radius = get_home_values(npc_serial)
    if home_x == nil or home_y == nil or home_z == nil then
        return false
    end

    mark_mode(npc_serial, "return_home")
    fsm.set_action(npc_serial, fsm.actions.guard)
    combat.clear_target(npc_serial)

    if npc ~= nil then
        npc:set_war_mode(false)
    end

    if npc ~= nil and home_map_id ~= nil and npc.map_id ~= home_map_id then
        return mobile.teleport(npc_serial, home_map_id, math.floor(home_x), math.floor(home_y), math.floor(home_z))
    end

    steering.move_to(npc_serial, math.floor(home_x), math.floor(home_y), math.floor(home_z), math.max(0, math.floor(hold_radius)))

    return true
end

should_return_home = function(npc_serial, npc)
    local home_x, home_y, _, home_map_id, _, leash_radius = get_home_values(npc_serial)
    if home_x == nil or home_y == nil or npc == nil then
        return false
    end

    if home_map_id ~= nil and npc.map_id ~= home_map_id then
        return true
    end

    return math.abs(npc.location_x - home_x) > leash_radius or math.abs(npc.location_y - home_y) > leash_radius
end

local function engage_target(npc_serial, target_serial)
    local npc = mobile.get(npc_serial)
    local target = mobile.get(target_serial)

    if npc == nil or target == nil then
        return false
    end

    set_focus(npc_serial, target_serial)
    npc:set_target(target)
    npc:set_war_mode(true)
    combat.set_target(npc_serial, target_serial)

    return true
end

local function handle_melee_guard(npc_serial, npc, target_serial, target, range_perception, range_fight)
    local distance = perception.distance(npc_serial, target_serial)
    if distance < 0 then
        clear_focus(npc_serial, npc)
        return false
    end

    if distance > range_perception + 2 then
        if guards.teleport_to_target(npc_serial, target_serial) == true then
            mark_mode(npc_serial, "combat")
            fsm.set_action(npc_serial, fsm.actions.combat)
            movement.combat(npc_serial, target_serial, range_fight)
            return true
        end

        clear_focus(npc_serial, npc)
        return move_home(npc_serial, npc)
    end

    if distance > range_fight then
        mark_mode(npc_serial, "combat")
        fsm.set_action(npc_serial, fsm.actions.combat)
        movement.combat(npc_serial, target_serial, range_fight)
        return true
    end

    mark_mode(npc_serial, "guard")
    fsm.set_action(npc_serial, fsm.actions.guard)
    guards.try_reveal(npc_serial, target_serial)
    movement.guard(npc_serial)

    return true
end

local function handle_ranged_guard(npc_serial, npc, target_serial, target, range_perception, range_fight)
    local distance = perception.distance(npc_serial, target_serial)
    if distance < 0 then
        clear_focus(npc_serial, npc)
        return false
    end

    local preferred_min_range = tonumber(npc_state.get_var(npc_serial, PREFERRED_MIN_RANGE_KEY) or 4) or 4
    local preferred_max_range = tonumber(npc_state.get_var(npc_serial, PREFERRED_MAX_RANGE_KEY) or 6) or 6
    local attack_range = combat.get_attack_range(npc_serial)
    local follow_range = math.max(preferred_max_range, attack_range)

    if distance > range_perception + 2 then
        if guards.teleport_to_target(npc_serial, target_serial) == true then
            mark_mode(npc_serial, "combat")
            fsm.set_action(npc_serial, fsm.actions.combat)
            movement.combat(npc_serial, target_serial, follow_range)
            return true
        end

        clear_focus(npc_serial, npc)
        return move_home(npc_serial, npc)
    end

    if distance < preferred_min_range then
        mark_mode(npc_serial, "backoff")
        fsm.set_action(npc_serial, fsm.actions.backoff)
        guards.try_reveal(npc_serial, target_serial)
        movement.backoff(npc_serial, target_serial, preferred_max_range)
        return true
    end

    if distance > follow_range then
        mark_mode(npc_serial, "combat")
        fsm.set_action(npc_serial, fsm.actions.combat)
        movement.combat(npc_serial, target_serial, follow_range)
        return true
    end

    mark_mode(npc_serial, "guard")
    fsm.set_action(npc_serial, fsm.actions.guard)
    guards.try_reveal(npc_serial, target_serial)
    movement.guard(npc_serial)

    return true
end

local function handle_target(npc_serial, npc, target_serial, range_perception, range_fight)
    local target = mobile.get(target_serial)
    if target == nil then
        clear_focus(npc_serial, npc)
        return false
    end

    if target.map_id ~= npc.map_id then
        clear_focus(npc_serial, npc)
        return move_home(npc_serial, npc)
    end

    engage_target(npc_serial, target_serial)

    if is_ranged_guard(npc_serial) then
        return handle_ranged_guard(npc_serial, npc, target_serial, target, range_perception, range_fight)
    end

    return handle_melee_guard(npc_serial, npc, target_serial, target, range_perception, range_fight)
end

local function maybe_acquire_target(npc_serial, range_perception)
    local fight_mode = targeting.get_fight_mode(npc_serial, "aggressor")
    return targeting.find_hostile_target(npc_serial, range_perception, fight_mode)
end

local function handle_combat_hook(npc_serial, source_serial, event_obj)
    local npc = mobile.get(npc_serial)
    local source = mobile.get(source_serial)
    if npc == nil or source == nil or type(event_obj) ~= "table" then
        return
    end

    guards.try_reveal(npc_serial, source_serial)
    set_focus(npc_serial, source_serial)
    npc:set_target(source)
    npc:set_war_mode(true)
    combat.set_target(npc_serial, source_serial)
    npc_state.set_var(npc_serial, get_engaged_key(source_serial), true)
    mark_mode(npc_serial, is_ranged_guard(npc_serial) and "ranged" or "melee")
end

function guard.on_think(npc_serial)
    initialize_defaults(npc_serial)

    while true do
        local npc = mobile.get(npc_serial)

        if npc ~= nil then
            local range_perception = mobile.get_ai_range_perception(npc_serial) or (is_ranged_guard(npc_serial) and 10 or 3)
            local range_fight = mobile.get_ai_range_fight(npc_serial) or 1
            local target_serial = guards.get_focus(npc_serial)

            if not targeting.is_target_valid(npc_serial, target_serial) then
                target_serial = maybe_acquire_target(npc_serial, range_perception)

                if target_serial ~= nil and target_serial > 0 then
                    set_focus(npc_serial, target_serial)
                else
                    clear_focus(npc_serial, npc)

                    if should_return_home(npc_serial, npc) then
                        move_home(npc_serial, npc)
                    elseif get_patrol_mode(npc_serial) == "random_roam" and get_patrol_radius(npc_serial) > 0 then
                        patrol_random_roam(npc_serial, npc)
                    else
                        mark_mode(npc_serial, "idle")
                        fsm.set_action(npc_serial, fsm.actions.guard)
                        movement.guard(npc_serial)
                    end

                    coroutine.yield(TICK_DELAY_MS)
                    goto continue
                end
            end

            if target_serial ~= nil and target_serial > 0 then
                handle_target(npc_serial, npc, target_serial, range_perception, range_fight)
            elseif should_return_home(npc_serial, npc) then
                move_home(npc_serial, npc)
            elseif get_patrol_mode(npc_serial) == "random_roam" and get_patrol_radius(npc_serial) > 0 then
                patrol_random_roam(npc_serial, npc)
            else
                mark_mode(npc_serial, "idle")
                fsm.set_action(npc_serial, fsm.actions.guard)
                movement.guard(npc_serial)
            end
        end

        coroutine.yield(TICK_DELAY_MS)
        ::continue::
    end
end

function guard.on_event(event_type, from_serial, event_obj)
    local npc_serial = 0

    if type(event_obj) == "table" then
        npc_serial = tonumber(event_obj.listener_npc_id or event_obj.mobile_id or 0) or 0
    end

    if npc_serial <= 0 then
        return
    end

    if event_type == "in_range" then
        guard.on_in_range(npc_serial, from_serial, event_obj)
        return
    end

    if event_type == "out_range" then
        guard.on_out_range(npc_serial, from_serial, event_obj)
        return
    end

    if
        event_type == "attack"
        or event_type == "missed_attack"
        or event_type == "attacked"
        or event_type == "missed_by_attack"
        or event_type == "combat"
    then
        handle_combat_hook(npc_serial, from_serial, event_obj)
    end
end

function guard.on_in_range(npc_serial, source_serial, event_obj)
    local npc = mobile.get(npc_serial)
    local source = mobile.get(source_serial)
    if npc == nil or source == nil or type(event_obj) ~= "table" then
        return
    end

    local seen_key = get_seen_key(source_serial)
    local engaged_key = get_engaged_key(source_serial)
    local source_name = tostring(event_obj.source_name or source.name or "")
    local source_is_player = event_obj.source_is_player == true
    local source_is_enemy = event_obj.source_is_enemy == true

    if source_is_player and source_name ~= "" and npc_state.get_var(npc_serial, seen_key) ~= true then
        npc:say("Hello, " .. source_name .. ", How do you feel today?")
        npc_state.set_var(npc_serial, seen_key, true)
    end

    if source_is_enemy then
        guards.try_reveal(npc_serial, source_serial)

        if npc_state.get_var(npc_serial, engaged_key) ~= true then
            set_focus(npc_serial, source_serial)
            npc:set_target(source)
            npc:set_war_mode(true)
            combat.set_target(npc_serial, source_serial)
            npc_state.set_var(npc_serial, engaged_key, true)
            mark_mode(npc_serial, is_ranged_guard(npc_serial) and "ranged" or "melee")
        end
    end
end

function guard.on_out_range(npc_serial, source_serial, event_obj)
    _ = event_obj

    npc_state.set_var(npc_serial, get_engaged_key(source_serial), nil)
end

function guard.on_death(by_character, context)
    _ = by_character
    _ = context
end
