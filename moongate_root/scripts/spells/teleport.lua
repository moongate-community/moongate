-- ==============================================================================
-- Moongate - scripts/spells/teleport.lua
--
-- What it is for:
--   The third circle spell Teleport: the caster stands at the place it picked,
--   in sight and within the reach of a spell, with a puff at both places and the
--   sound. It is refused, before anything is spent, for a caster too loaded to
--   move, a place no one can stand on or that a mobile or an item fills ("that
--   location is blocked") and a region that does not let a teleport out of it
--   or into it (the teleport_out and teleport_in flags of data/regions).
--   Called by the spell service with the caster, the target
--   ({ kind = "location", map, x, y, z }) and the data of the spell.
--
-- Functions:
--   teleport.check(caster, target, info)   a cliloc number that refuses the
--                                      cast before anything is spent
--   teleport.cast(caster, target, info)    the effect, once the cast succeeded
-- ==============================================================================

teleport = {}

local TOO_ENCUMBERED = 502359   -- Thou art too encumbered to move.
local BLOCKED = 501942          -- That location is blocked.
local CANNOT_FROM = 502361      -- You cannot teleport into that area from here.
local CANNOT_TO = 501035        -- You cannot teleport from here to the destination.
local EFFECT_SPEED = 10

function teleport.check(caster, target, info)
    local here = mobile.location(caster)

    if not here then
        return false
    end

    if (mobile.weight(caster) or 0) > (mobile.max_weight(caster) or math.huge) then
        return TOO_ENCUMBERED
    end

    if not world.travel_allowed(here.map, here.x, here.y, here.z, "teleport_out") then
        return CANNOT_FROM
    end

    local z = world.standing_z(target.map, target.x, target.y, target.z)

    -- Nor where a mobile stands, or an impassable item lies on the ground, such as a shut door.
    if not z or not world.can_fit(target.map, target.x, target.y, z, caster) then
        return BLOCKED
    end

    if not world.travel_allowed(target.map, target.x, target.y, z, "teleport_in") then
        return CANNOT_TO
    end
end

function teleport.cast(caster, target, info)
    local here = mobile.location(caster)
    local z = world.standing_z(target.map, target.x, target.y, target.z)

    if not here or not z then
        return
    end

    if not mobile.teleport(caster, target.x, target.y, z, target.map) then
        return
    end

    local options = { speed = EFFECT_SPEED, duration = info.effect_duration }

    if info.effect ~= 0 then
        effect.at(here.map, here.x, here.y, here.z, info.effect, options)
        effect.at(target.map, target.x, target.y, z, info.effect, options)
    end

    if info.sound ~= 0 then
        world.play_sound(here.map, here.x, here.y, here.z, info.sound)
        world.play_sound(target.map, target.x, target.y, z, info.sound)
    end
end
