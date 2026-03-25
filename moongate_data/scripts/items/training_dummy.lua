local MIN_SKILL = -25.0
local MAX_SKILL = 25.0
local SWING_TIMER_PROP = "swing_timer_id"

local function chebyshev_distance(actor, target)
    return math.max(
        math.abs(actor.location_x - target.location_x),
        math.abs(actor.location_y - target.location_y)
    )
end

local function get_swing_item_id(item_id)
    if item_id == 0x1070 then
        return 0x1071
    end

    if item_id == 0x1074 then
        return 0x1075
    end

    return item_id
end

local function get_rest_item_id(item_id)
    if item_id == 0x1071 then
        return 0x1070
    end

    if item_id == 0x1075 then
        return 0x1074
    end

    return item_id
end

local function is_ranged_weapon(weapon)
    if weapon == nil then
        return false
    end

    if weapon.weapon_skill == "Archery" or weapon.weapon_skill == "Throwing" then
        return true
    end

    return tonumber(weapon.range_max or 0) > 1
end

local function is_swinging(dummy)
    local timer_id = dummy:get_prop(SWING_TIMER_PROP)

    return timer_id ~= nil and tostring(timer_id) ~= ""
end

local function begin_swing(dummy)
    local existing_timer = dummy:get_prop(SWING_TIMER_PROP)
    if existing_timer ~= nil and tostring(existing_timer) ~= "" then
        timer.cancel(tostring(existing_timer))
    end

    dummy:set_item_id(get_swing_item_id(dummy.item_id))

    local timer_id = timer.after("training_dummy_" .. tostring(dummy.serial), 3000, function()
        local resolved_dummy = item.get(dummy.serial)
        if resolved_dummy == nil then
            return
        end

        resolved_dummy:set_item_id(get_rest_item_id(resolved_dummy.item_id))
        resolved_dummy:remove_prop(SWING_TIMER_PROP)
    end)

    dummy:set_prop(SWING_TIMER_PROP, timer_id)
end

local function on_double_click(ctx)
    if ctx == nil or ctx.item == nil or ctx.mobile_id == nil then
        return
    end

    local actor = mobile.get(ctx.mobile_id)
    local dummy = item.get(ctx.item.serial)
    if actor == nil or dummy == nil then
        return
    end

    local weapon = mobile.get_weapon(actor.serial)
    if weapon == nil then
        speech.say(actor.serial, "You must equip a melee weapon to practice on this.")
        return
    end

    if is_ranged_weapon(weapon) then
        speech.say(actor.serial, "You can't practice ranged weapons on this.")
        return
    end

    if chebyshev_distance(actor, dummy) > math.max(1, tonumber(weapon.range_max or 1)) then
        speech.say(actor.serial, "You are too far away to do that.")
        return
    end

    if is_swinging(dummy) then
        speech.say(actor.serial, "You have to wait until it stops swinging.")
        return
    end

    if mobile.get_skill(actor.serial, weapon.weapon_skill) >= MAX_SKILL then
        speech.say(actor.serial, "Your skill cannot improve any further by simply practicing with a dummy.")
        return
    end

    if actor.is_mounted then
        speech.say(actor.serial, "You can't practice on this while on a mount.")
        return
    end

    begin_swing(dummy)
    mobile.check_skill(actor.serial, weapon.weapon_skill, MIN_SKILL, MAX_SKILL, dummy.serial)
end

items_training_dummy = {
    on_double_click = on_double_click,
}
