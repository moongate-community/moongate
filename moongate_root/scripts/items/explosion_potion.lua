-- ==============================================================================
-- Moongate - scripts/items/explosion_potion.lua
--
-- What it is for:
--   The item script of the explosion potions, with
--   script_id = "explosion_potion". Double clicked in the backpack or within
--   1 tile, one potion is armed ("You should throw it now!") and a cursor
--   opens to throw it; a countdown of 3, 2, 1 runs, the first number after
--   0.75 seconds, then one a second, and at 0 it explodes where it is: in the
--   hand, at its holder. Thrown within 10 tiles and in sight, it flies a
--   tenth of a second a tile and the countdown goes on where it lands. The
--   blast hurts every living mobile within 2 tiles (the thrower too) with the
--   rules of a blow, by the potion and the thrower's Alchemy, at most 40, and
--   sets off the other explosion potions within 2 tiles.
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
local SEE_RANGE = 15

local TOO_FAR_TO_USE = 502138 -- That is too far away for you to use.
local THROW_IT_NOW = 500236   -- You should throw it now!
local TOO_FAR = 500446        -- That is too far away.
local CANNOT_SEE = 500237     -- Target cannot be seen.

local POTION_GRAPHIC = 0x0F0D
local EXPLOSION_SOUND = 0x307
local EXPLOSION_EFFECT = 0x36B0
local ARMED_PROP = "explosion.armed"

-- The least and most damage of each potion, before the thrower's Alchemy.
local DAMAGE = {
    lesserexplosionpotion = { 5, 10 },
    explosionpotion = { 10, 20 },
    greaterexplosionpotion = { 15, 30 },
}

-- The armed potions, by serial: who armed it, its template, the count left, and where it flies.
local fuses = {}

local function ground_root(serial)
    local root = serial

    while item.container(root) do
        root = item.container(root)
    end

    return root
end

-- Where a potion is: its holder's place, or its own on the ground.
local function place_of(serial)
    local holder = item.owner(serial)

    if holder then
        return mobile.location(holder), holder
    end

    return item.location(ground_root(serial)), nil
end

local function alchemy(user)
    return math.floor(((mobile.skills(user) or {}).alchemy or 0) / 10)
end

local explode

local function explode_at(fuse, here)
    if not here then
        return
    end

    world.play_sound(here.map, here.x, here.y, here.z, EXPLOSION_SOUND)
    effect.at(here.map, here.x, here.y, here.z, EXPLOSION_EFFECT)

    local range = DAMAGE[fuse.template]
    local bonus = alchemy(fuse.thrower)

    for _, target in ipairs(world.mobiles_in_range(here.map, here.x, here.y, BLAST_RANGE)) do
        if not mobile.is_dead(target) then
            local damage = math.min(explosion_potion.random(range[1], range[2]) + bonus, MAX_DAMAGE)
            combat.harm(target, damage, fuse.thrower)
        end
    end

    -- The other explosion potions within reach of the blast go off with it.
    for _, other in ipairs(world.items_in_range(here.map, here.x, here.y, BLAST_RANGE)) do
        if item.script(other) == "explosion_potion" and not (fuses[other] and fuses[other].done) then
            explode(other, fuse.thrower)
        end
    end
end

explode = function(serial, thrower)
    local fuse = fuses[serial] or { thrower = thrower, template = item.template(serial) }

    if not DAMAGE[fuse.template or ""] then
        return
    end

    fuse.done = true
    fuses[serial] = fuse

    local here = place_of(serial)

    if fuse.aiming then
        target.cancel(fuse.thrower)
    end

    item.delete(serial)
    explode_at(fuse, here)
    fuses[serial] = nil
end

local function show_count(fuse, count)
    if not fuse.serial then
        return
    end

    local here, holder = place_of(fuse.serial)

    if holder then
        mobile.message(holder, tostring(count))

        return
    end

    if not here then
        return
    end

    for _, player in ipairs(world.mobiles_in_range(here.map, here.x, here.y, SEE_RANGE)) do
        if mobile.is_player(player) then
            item.message(fuse.serial, player, tostring(count))
        end
    end
end

local function tick(fuse)
    if fuse.done then
        return
    end

    if fuse.count == 0 then
        -- In flight it goes off where it lands.
        if fuse.serial then
            explode(fuse.serial, fuse.thrower)
        else
            fuse.go_off_on_landing = true
        end

        return
    end

    show_count(fuse, fuse.count)
    fuse.count = fuse.count - 1
    timer.after(COUNT_EVERY, function()
        tick(fuse)
    end)
end

local function land(fuse, spot)
    local serial = item.create(fuse.template, spot.map, spot.x, spot.y, spot.z)

    if not serial then
        fuse.done = true

        return
    end

    item.set_prop(serial, ARMED_PROP, true)
    fuse.serial = serial
    fuses[serial] = fuse

    if fuse.go_off_on_landing then
        explode(serial, fuse.thrower)
    end
end

local function aim(serial, user)
    local fuse = fuses[serial]
    fuse.aiming = true

    target.pick_location(user, function(picked)
        fuse.aiming = false

        if fuse.done or fuse.serial ~= serial or item.owner(serial) ~= user then
            return
        end

        local spot

        if picked.kind == "location" then
            spot = picked
        elseif picked.kind == "object" then
            spot = mobile.location(picked.serial) or item.location(picked.serial)
        end

        local here = mobile.location(user)

        if not spot or not here then
            return
        end

        local distance = math.max(math.abs(spot.x - here.x), math.abs(spot.y - here.y))

        if spot.map ~= here.map or distance > THROW_RANGE then
            mobile.message_cliloc(user, TOO_FAR)

            return
        end

        if not world.line_of_sight(here.map, here.x, here.y, here.z, spot.x, spot.y, spot.z) then
            mobile.message_cliloc(user, CANNOT_SEE)

            return
        end

        effect.moving_to(user, spot.x, spot.y, spot.z, POTION_GRAPHIC)
        fuses[serial] = nil
        fuse.serial = nil
        item.delete(serial)
        timer.after(distance * FLIGHT_A_TILE, function()
            land(fuse, { map = here.map, x = spot.x, y = spot.y, z = spot.z })
        end)
    end)
end

-- One potion of a stack, armed apart: its prop keeps it out of the stack.
local function arm_one(serial, user)
    if (item.amount(serial) or 1) <= 1 then
        item.set_prop(serial, ARMED_PROP, true)

        return serial
    end

    local here = mobile.location(user)
    local single = here and item.create(item.template(serial), here.map, here.x, here.y, here.z)

    if not single then
        return nil
    end

    item.set_prop(single, ARMED_PROP, true)
    item.consume(serial, 1)
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

    local armed = arm_one(serial, user)

    if not armed then
        return true
    end

    local fuse = { thrower = user, template = item.template(armed), count = COUNT_FROM, serial = armed }
    fuses[armed] = fuse
    mobile.message_cliloc(user, THROW_IT_NOW)
    timer.after(FIRST_COUNT, function()
        tick(fuse)
    end)
    aim(armed, user)

    return true
end
