-- ==============================================================================
-- Moongate - scripts/items/explosion_potion.lua
--
-- What it is for:
--   The item script of the explosion potions, with
--   script_id = "explosion_potion". Double clicked in the backpack or within
--   1 tile, one potion is armed in the backpack ("You should throw it now!")
--   and a cursor opens to throw it; a countdown of 3, 2, 1 runs over whoever
--   holds it, the first number after 0.75 seconds, then one a second, and at
--   0 it explodes where it is: in the hand, at its holder. Thrown within 10
--   tiles and in sight, it flies a tenth of a second a tile and the countdown
--   goes on where it lands. The blast hurts every living mobile within 2
--   tiles (the thrower too) with the rules of a blow, by the potion and the
--   thrower's Alchemy, at most 40, and sets off the other explosion potions
--   within 2 tiles. A potion held on a cursor goes off once it is let go.
--
-- Functions:
--   explosion_potion.on_use(serial, user)   arms the potion, or aims it again
--   explosion_potion.random(low, high)      the roll of the damage, math.random
-- ==============================================================================

explosion_potion = {}

explosion_potion.random = math.random

local REACH = 1
local THROW_RANGE = 10
local BLAST_RANGE = 2
local MAX_DAMAGE = 40
local FIRST_COUNT = 0.75
local COUNT_EVERY = 1.0
local COUNT_FROM = 3
local FLIGHT_A_TILE = 0.1
local HELD_RETRY = 0.25
local SEE_RANGE = 15
local EYE_HEIGHT = 14

local TOO_FAR_TO_USE = 502138 -- That is too far away for you to use.
local THROW_IT_NOW = 500236   -- You should throw it now!
local TOO_FAR = 500446        -- That is too far away.
local CANNOT_SEE = 500237     -- Target cannot be seen.

local POTION_GRAPHIC = 0x0F0D
local EXPLOSION_SOUND = 0x307
local EXPLOSION_EFFECT = 0x36B0

-- The prop of an armed potion: the id of its fuse. One the fuses do not know, as after a restart, is armed no more.
local ARMED_PROP = "explosion.armed"

-- The least and most damage of each potion, before the thrower's Alchemy.
local DAMAGE = {
    lesserexplosionpotion = { 5, 10 },
    explosionpotion = { 10, 20 },
    greaterexplosionpotion = { 15, 30 },
}

-- The armed potions, by serial.
local fuses = {}
local next_fuse = 0

local function ground_root(serial)
    local root = serial

    while item.container(root) do
        root = item.container(root)
    end

    return root
end

-- Where a potion is: its holder's place, or the place of what it lies in on the ground; and what to show a label over.
local function place_of(serial)
    local holder = item.owner(serial)

    if holder then
        return mobile.location(holder), holder, nil
    end

    local root = ground_root(serial)

    return item.location(root), nil, root
end

local function alchemy(user)
    return math.floor(((mobile.skills(user) or {}).alchemy or 0) / 10)
end

-- The thrower, while it is in the world to blame.
local function blamed(thrower)
    if thrower and mobile.location(thrower) then
        return thrower
    end

    return nil
end

local explode

local function blast(fuse, here, direct)
    world.play_sound(here.map, here.x, here.y, here.z, EXPLOSION_SOUND)
    effect.at(here.map, here.x, here.y, here.z, EXPLOSION_EFFECT)

    local range = DAMAGE[fuse.template]
    local attacker = blamed(fuse.thrower)
    -- Alchemy strengthens the potion that was thrown, not those it sets off.
    local bonus = direct and attacker and alchemy(attacker) or 0

    for _, target in ipairs(world.mobiles_in_range(here.map, here.x, here.y, BLAST_RANGE)) do
        if not mobile.is_dead(target) then
            local damage = math.min(explosion_potion.random(range[1], range[2]) + bonus, MAX_DAMAGE)

            if attacker then
                combat.harm(target, damage, attacker)
            else
                combat.harm(target, damage)
            end
        end
    end

    for _, other in ipairs(world.items_in_range(here.map, here.x, here.y, BLAST_RANGE)) do
        if item.script(other) == "explosion_potion" and not (fuses[other] and fuses[other].done) then
            explode(other, fuse.thrower, false)
        end
    end
end

-- Goes off; false when the potion cannot be used up now, as one held on a cursor.
explode = function(serial, thrower, direct)
    local fuse = fuses[serial] or { thrower = thrower, template = item.template(serial) }

    if not DAMAGE[fuse.template or ""] then
        return true
    end

    local here = place_of(serial)

    -- One potion of a stack goes off.
    local gone = (item.amount(serial) or 1) > 1 and item.consume(serial, 1) or item.delete(serial)

    if not gone then
        return false
    end

    fuse.done = true
    fuses[serial] = nil

    if fuse.aimer then
        target.cancel(fuse.aimer)
    end

    if here then
        blast(fuse, here, direct)
    end

    return true
end

local function show_count(fuse, count)
    local here, holder, root = place_of(fuse.serial)

    if holder then
        mobile.say(holder, tostring(count))

        return
    end

    if not here then
        return
    end

    for _, player in ipairs(world.mobiles_in_range(here.map, here.x, here.y, SEE_RANGE)) do
        if mobile.is_player(player) then
            item.message(root, player, tostring(count))
        end
    end
end

-- Whether the fuse still burns: its potion carries its id, or it is in flight.
local function burning(fuse)
    if fuse.done then
        return false
    end

    return fuse.serial == nil or item.get_prop(fuse.serial, ARMED_PROP) == fuse.id
end

local function tick(fuse)
    if not burning(fuse) then
        return
    end

    if fuse.count > 0 then
        show_count(fuse, fuse.count)
        fuse.count = fuse.count - 1
        timer.after(COUNT_EVERY, function()
            tick(fuse)
        end)

        return
    end

    -- In flight it goes off where it lands; held on a cursor, once it is let go.
    if not fuse.serial then
        fuse.go_off_on_landing = true
    elseif not explode(fuse.serial, fuse.thrower, true) then
        timer.after(HELD_RETRY, function()
            tick(fuse)
        end)
    end
end

local function land(fuse, spot)
    if fuse.done then
        return
    end

    local z = world.standing_z(spot.map, spot.x, spot.y, spot.z) or spot.z
    local serial = item.create(fuse.template, spot.map, spot.x, spot.y, z)

    if not serial then
        fuse.done = true

        return
    end

    item.set_prop(serial, ARMED_PROP, fuse.id)
    fuse.serial = serial
    fuses[serial] = fuse

    if fuse.go_off_on_landing then
        explode(serial, fuse.thrower, true)
    end
end

-- Where the cursor points: a place, a mobile, or an item and what it lies in.
local function spot_of(picked)
    if picked.kind == "location" then
        return picked
    end

    if picked.kind == "object" then
        return mobile.location(picked.serial) or item.location(ground_root(picked.serial))
    end

    return nil
end

local function aim(serial, user)
    local fuse = fuses[serial]
    fuse.aims = (fuse.aims or 0) + 1
    fuse.aimer = user
    local this_aim = fuse.aims

    target.pick_location(user, function(picked)
        -- A cursor another one replaced says nothing of the live one.
        if fuse.aims ~= this_aim then
            return
        end

        fuse.aimer = nil

        if not burning(fuse) or fuse.serial ~= serial or item.owner(serial) ~= user then
            return
        end

        local spot = spot_of(picked)
        local here = mobile.location(user)

        if picked.kind == "canceled" or not here then
            return
        end

        if not spot then
            mobile.message_cliloc(user, CANNOT_SEE)

            return
        end

        local dx, dy = spot.x - here.x, spot.y - here.y
        local distance = math.sqrt(dx * dx + dy * dy)

        if spot.map ~= here.map or math.max(math.abs(dx), math.abs(dy)) > THROW_RANGE then
            mobile.message_cliloc(user, TOO_FAR)

            return
        end

        if not world.line_of_sight(here.map, here.x, here.y, here.z + EYE_HEIGHT, spot.x, spot.y, spot.z) then
            mobile.message_cliloc(user, CANNOT_SEE)

            return
        end

        -- The potion leaves the hand only if it can: one held on a cursor is not thrown.
        local thrown = (item.amount(serial) or 1) > 1 and item.consume(serial, 1) or item.delete(serial)

        if not thrown then
            return
        end

        effect.moving_to(user, spot.x, spot.y, spot.z, POTION_GRAPHIC)
        fuses[serial] = nil
        fuse.serial = nil
        timer.after(distance * FLIGHT_A_TILE, function()
            land(fuse, { map = here.map, x = spot.x, y = spot.y, z = spot.z })
        end)
    end)
end

-- One potion, armed apart in the holder's backpack: its prop keeps it out of the stack. Nil when it cannot be.
local function arm_one(serial, user, id)
    if (item.amount(serial) or 1) <= 1 then
        if item.owner(serial) ~= user and not item.move_into(serial, user) then
            return nil
        end

        item.set_prop(serial, ARMED_PROP, id)

        return serial
    end

    local here = mobile.location(user)
    local template = item.template(serial)

    -- Taken from the stack first: a stack held on a cursor gives nothing.
    if not here or not item.consume(serial, 1) then
        return nil
    end

    local single = item.create(template, here.map, here.x, here.y, here.z)

    if not single then
        return nil
    end

    item.set_prop(single, ARMED_PROP, id)
    item.move_into(single, user)

    return single
end

function explosion_potion.on_use(serial, user)
    if not DAMAGE[item.template(serial) or ""] then
        return true
    end

    if item.owner(serial) ~= user and not item.in_range(ground_root(serial), user, REACH) then
        mobile.message_cliloc(user, TOO_FAR_TO_USE)

        return true
    end

    -- Armed already: the cursor opens again.
    if fuses[serial] then
        aim(serial, user)

        return true
    end

    next_fuse = next_fuse + 1
    local id = world.now() * 1000 + next_fuse
    local armed = arm_one(serial, user, id)

    if not armed then
        return true
    end

    local fuse = { id = id, thrower = user, template = item.template(armed), count = COUNT_FROM, serial = armed }
    fuses[armed] = fuse
    mobile.message_cliloc(user, THROW_IT_NOW)
    timer.after(FIRST_COUNT, function()
        tick(fuse)
    end)
    aim(armed, user)

    return true
end
