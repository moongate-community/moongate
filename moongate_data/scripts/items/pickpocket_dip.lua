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
    if item_id == 0x1EC0 then
        return 0x1EC1
    end

    if item_id == 0x1EC3 then
        return 0x1EC4
    end

    return item_id
end

local function get_rest_item_id(item_id)
    if item_id == 0x1EC1 then
        return 0x1EC0
    end

    if item_id == 0x1EC4 then
        return 0x1EC3
    end

    return item_id
end

local function is_swinging(target)
    local timer_id = target:get_prop(SWING_TIMER_PROP)

    return timer_id ~= nil and tostring(timer_id) ~= ""
end

local function begin_swing(target)
    local existing_timer = target:get_prop(SWING_TIMER_PROP)
    if existing_timer ~= nil and tostring(existing_timer) ~= "" then
        timer.cancel(tostring(existing_timer))
    end

    target:set_item_id(get_swing_item_id(target.item_id))

    local timer_id = timer.after("pickpocket_dip_" .. tostring(target.serial), 3000, function()
        local resolved_target = item.get(target.serial)
        if resolved_target == nil then
            return
        end

        resolved_target:set_item_id(get_rest_item_id(resolved_target.item_id))
        resolved_target:remove_prop(SWING_TIMER_PROP)
    end)

    target:set_prop(SWING_TIMER_PROP, timer_id)
end

local function on_double_click(ctx)
    if ctx == nil or ctx.item == nil or ctx.mobile_id == nil then
        return
    end

    local actor = mobile.get(ctx.mobile_id)
    local target = item.get(ctx.item.serial)
    if actor == nil or target == nil then
        return
    end

    if chebyshev_distance(actor, target) > 1 then
        speech.say(actor.serial, "You are too far away to do that.")
        return
    end

    if is_swinging(target) then
        speech.say(actor.serial, "You have to wait until it stops swinging.")
        return
    end

    if mobile.get_skill(actor.serial, "stealing") >= MAX_SKILL then
        speech.say(actor.serial, "Your ability to steal cannot improve any further by simply practicing on a dummy.")
        return
    end

    if actor.is_mounted then
        speech.say(actor.serial, "You can't practice on this while on a mount.")
        return
    end

    local success = mobile.check_skill(actor.serial, "stealing", MIN_SKILL, MAX_SKILL, target.serial)
    if success then
        speech.say(actor.serial, "You successfully avoid disturbing the dip while searching it.")
        return
    end

    begin_swing(target)
    speech.say(actor.serial, "You carelessly bump the dip and start it swinging.")
end

items_pickpocket_dip = {
    on_double_click = on_double_click,
}
