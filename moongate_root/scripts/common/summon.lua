-- ==============================================================================
-- Moongate - scripts/common/summon.lua
--
-- What it is for:
--   What the spells that summon a creature share (Blade Spirits, Energy Vortex,
--   Summon Creature, the elementals and the daemon): the creature is made from a
--   mobile template, becomes a follower of the caster (it has the prop owner, so
--   it counts for its control slots and fights for the caster by the pet order
--   guard, common/pet_orders.lua), and goes away when its time is up, when its
--   master dies or leaves the game, or when a Dispel undoes it. A spell takes it
--   with local summon = require("common.summon"). common/creature.lua calls
--   summon.tick on every think of a creature, so a summon that was saved goes
--   away at the right time after a restart.
--
-- Props it sets on the creature:
--   owner            the serial of the caster
--   pet.order        guard
--   summon.until     the time, as world.now(), it goes away at
--   summon.difficulty and summon.focus   what a Dispel weighs it by
--
-- Functions:
--   summon.refuse(caster, slots)      the cliloc 1049645 that refuses a summon
--                                     whose slots do not fit in the followers
--                                     of the caster; nil when they do
--   summon.refuse_template(caster, template)   the same, by the slots the
--                                     mobile template counts for
--   summon.near(caster)               a free place beside the caster as { map,
--                                     x, y, z }, nil when there is none
--   summon.create(caster, template, place, seconds, sound)   asks for a
--                                     creature of the template at the place
--                                     ({ map, x, y, z }); false when it could
--                                     not be asked for
--   summon.is_summoned(serial)        whether the creature is a summon
--   summon.dispel_data(serial)        its difficulty and its focus, nil for a
--                                     creature that is not a summon
--   summon.dismiss(serial)            takes the summon away, with the puff and
--                                     the sound; false when it is not one
--   summon.tick(serial)               dismisses it when its time is up or its
--                                     master is gone; true when it did
-- ==============================================================================

local summon = {}

local TOO_MANY_FOLLOWERS = 1049645   -- You have too many followers to summon that creature.
local PUFF = 0x3728
local PUFF_SPEED = 8
local PUFF_DURATION = 20
local PUFF_SOUND = 0x201

local UNTIL = "summon.until"

-- How hard a creature is to dispel and how much the Magery of the caster weighs against it, by template; the others
-- take the first two.
local DEFAULT_DIFFICULTY = 0
local DEFAULT_FOCUS = 20
local DISPEL = {
    energyvortex_summon = { 80, 20 },
    airele_summon = { 117.5, 45 },
    earthele_summon = { 117.5, 45 },
    firele_summon = { 117.5, 45 },
    waterele_summon = { 117.5, 45 },
    daemon_summon = { 125, 45 },
}

function summon.refuse(caster, slots)
    if pet.followers(caster) + slots > pet.max_followers() then
        return TOO_MANY_FOLLOWERS
    end
end

function summon.refuse_template(caster, template)
    return summon.refuse(caster, pet.slots_of(template))
end

function summon.near(caster)
    local here = mobile.location(caster)

    if not here then
        return nil
    end

    return world.spot_beside(here.map, here.x, here.y, here.z)
end

function summon.create(caster, template, place, seconds, sound)
    local asked = npc.spawn(template, place.map, place.x, place.y, place.z, function(serial)
        local data = DISPEL[template] or { DEFAULT_DIFFICULTY, DEFAULT_FOCUS }

        npc.set_prop(serial, "owner", caster)
        npc.set_prop(serial, "pet.order", "guard")
        npc.set_prop(serial, UNTIL, world.now() + seconds)
        npc.set_prop(serial, "summon.difficulty", data[1])
        npc.set_prop(serial, "summon.focus", data[2])

        -- The caster has one follower more, and is shown it.
        pet.refresh(caster)

        timer.after(seconds, function()
            summon.dismiss(serial)
        end)
    end)

    if asked and sound and sound ~= 0 then
        world.play_sound(place.map, place.x, place.y, place.z, sound)
    end

    return asked
end

function summon.is_summoned(serial)
    return npc.get_prop(serial, UNTIL) ~= nil
end

function summon.dispel_data(serial)
    if not summon.is_summoned(serial) then
        return nil
    end

    return npc.get_prop(serial, "summon.difficulty") or DEFAULT_DIFFICULTY,
        npc.get_prop(serial, "summon.focus") or DEFAULT_FOCUS
end

function summon.dismiss(serial)
    local at = npc.location(serial)

    if not at or not summon.is_summoned(serial) then
        return false
    end

    local owner = npc.get_prop(serial, "owner")

    -- Taking the time away first: a timer and a think may both come for it before the removal is done.
    npc.set_prop(serial, UNTIL, nil)
    effect.at(at.map, at.x, at.y, at.z, PUFF, { speed = PUFF_SPEED, duration = PUFF_DURATION })
    world.play_sound(at.map, at.x, at.y, at.z, PUFF_SOUND)
    npc.delete(serial)

    if owner and owner ~= 0 then
        pet.refresh(owner)
    end

    return true
end

function summon.tick(serial)
    local ends = npc.get_prop(serial, UNTIL)

    if ends == nil then
        return false
    end

    local owner = npc.get_prop(serial, "owner")

    -- Its time is up; or its master is gone, dead, out of the game, or let it go.
    if world.now() >= ends or owner == nil or owner == 0 or mobile.is_dead(owner) or mobile.location(owner) == nil then
        return summon.dismiss(serial)
    end

    return false
end

return summon
