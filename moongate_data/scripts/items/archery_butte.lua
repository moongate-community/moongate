local MIN_SKILL = -25.0
local MAX_SKILL = 25.0
local USE_DELAY_MS = 2000
local STORED_ARROWS_PROP = "stored_arrows"
local STORED_BOLTS_PROP = "stored_bolts"
local LAST_USE_MS_PROP = "last_use_ms"

local function chebyshev_distance(actor, target)
    return math.max(
        math.abs(actor.location_x - target.location_x),
        math.abs(actor.location_y - target.location_y)
    )
end

local function facing_east(target)
    return target.item_id == 0x100A
end

local function get_integer_prop(target, key)
    local value = target:get_prop(key)

    return tonumber(value or 0) or 0
end

local function set_integer_prop(target, key, value)
    target:set_prop(key, math.floor(value))
end

local function get_shots_key(actor)
    return "shots_" .. tostring(actor.serial)
end

local function get_score_key(actor)
    return "score_" .. tostring(actor.serial)
end

local function get_ammo_template_id(ammo_item_id)
    if ammo_item_id == 0x0F3F then
        return "arrow"
    end

    if ammo_item_id == 0x1BFB then
        return "bolt"
    end

    return nil
end

local function has_stored_ammo(target)
    return get_integer_prop(target, STORED_ARROWS_PROP) > 0 or get_integer_prop(target, STORED_BOLTS_PROP) > 0
end

local function gather(actor, target)
    local arrows = get_integer_prop(target, STORED_ARROWS_PROP)
    local bolts = get_integer_prop(target, STORED_BOLTS_PROP)

    if arrows > 0 then
        mobile.add_item_to_backpack(actor.serial, "arrow", arrows)
    end

    if bolts > 0 then
        mobile.add_item_to_backpack(actor.serial, "bolt", bolts)
    end

    set_integer_prop(target, STORED_ARROWS_PROP, 0)
    set_integer_prop(target, STORED_BOLTS_PROP, 0)
    speech.say(actor.serial, "You gather the arrows and bolts.")
end

local function ensure_aligned(actor, target)
    if facing_east(target) then
        if actor.location_x <= target.location_x then
            speech.say(actor.serial, "You would do better to stand in front of the archery butte.")
            return false
        end

        if actor.location_y ~= target.location_y then
            speech.say(actor.serial, "You aren't properly lined up with the archery butte to get an accurate shot.")
            return false
        end
    else
        if actor.location_y <= target.location_y then
            speech.say(actor.serial, "You would do better to stand in front of the archery butte.")
            return false
        end

        if actor.location_x ~= target.location_x then
            speech.say(actor.serial, "You aren't properly lined up with the archery butte to get an accurate shot.")
            return false
        end
    end

    local distance = chebyshev_distance(actor, target)
    if distance > 6 then
        speech.say(actor.serial, "You are too far away from the archery butte to get an accurate shot.")
        return false
    end

    if distance < 5 then
        speech.say(actor.serial, "You are too close to the target.")
        return false
    end

    return true
end

local function calculate_score()
    local roll = math.random()

    if roll < 0.10 then
        return 50
    end

    if roll < 0.25 then
        return 10
    end

    if roll < 0.50 then
        return 5
    end

    return 2
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

    if has_stored_ammo(target) and chebyshev_distance(actor, target) <= 1 then
        gather(actor, target)
        return
    end

    local weapon = mobile.get_weapon(actor.serial)
    if weapon == nil or weapon.weapon_skill ~= "Archery" then
        speech.say(actor.serial, "You must practice with ranged weapons on this.")
        return
    end

    local ammo_item_id = tonumber(weapon.ammo_item_id or 0)
    local ammo_template_id = get_ammo_template_id(ammo_item_id)
    if ammo_item_id <= 0 or ammo_template_id == nil then
        speech.say(actor.serial, "You must practice with ranged weapons on this.")
        return
    end

    if not ensure_aligned(actor, target) then
        return
    end

    local now_ms = time.now_ms()
    local last_use_ms = get_integer_prop(target, LAST_USE_MS_PROP)
    if now_ms < last_use_ms + USE_DELAY_MS then
        return
    end

    if not mobile.consume_item(actor.serial, ammo_item_id, 1) then
        speech.say(actor.serial, "You do not have any ammunition with which to practice.")
        return
    end

    set_integer_prop(target, LAST_USE_MS_PROP, now_ms)

    local shots_key = get_shots_key(actor)
    set_integer_prop(target, shots_key, get_integer_prop(target, shots_key) + 1)

    local success = mobile.check_skill(actor.serial, weapon.weapon_skill, MIN_SKILL, MAX_SKILL, target.serial)
    if not success then
        speech.say(actor.serial, "You miss the target altogether.")
        return
    end

    if ammo_template_id == "arrow" then
        set_integer_prop(target, STORED_ARROWS_PROP, get_integer_prop(target, STORED_ARROWS_PROP) + 1)
    else
        set_integer_prop(target, STORED_BOLTS_PROP, get_integer_prop(target, STORED_BOLTS_PROP) + 1)
    end

    local score_key = get_score_key(actor)
    local score = calculate_score()
    set_integer_prop(target, score_key, get_integer_prop(target, score_key) + score)
    speech.say(actor.serial, "You score " .. tostring(score) .. " points.")
end

items_archery_butte = {
    on_double_click = on_double_click,
}
