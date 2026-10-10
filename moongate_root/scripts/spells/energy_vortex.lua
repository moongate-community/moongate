-- ==============================================================================
-- Moongate - scripts/spells/energy_vortex.lua
--
-- What it is for:
--   The eighth circle spell Energy Vortex: an energy vortex (energyvortex_summon) is called at the place picked
--   and fights for the caster, by the pet order guard, for 80 to 119 seconds, or until it is dispelled or
--   killed. It counts for the control slots of its template as a follower. It is refused, before anything is
--   spent, when the caster has too many followers for it, at a guarded town, and at a place where nothing can
--   stand or a mobile or an impassable item is. Called by the spell service with the caster, the target ({ kind
--   = "location", map, x, y, z }) and the data of the spell.
--
-- Functions:
--   energy_vortex.check(caster, target, info)   a cliloc number that refuses the cast
--                                      before anything is spent
--   energy_vortex.cast(caster, target, info)    the effect, once the cast succeeded
--   energy_vortex.random(low, high)            the roll of the seconds, math.random
-- ==============================================================================

local magic = require("common.magic")
local summon = require("common.summon")

energy_vortex = {}

energy_vortex.random = math.random

local BLOCKED = 501942   -- That location is blocked.
local TEMPLATE = "energyvortex_summon"
local LEAST_SECONDS = 80
local SECONDS_SPREAD = 40   -- 80 to 119

-- The place the creature stands at: the one picked, on the ground there; nil when nothing can stand there or it is full.
local function place_of(target)
    local z = world.standing_z(target.map, target.x, target.y, target.z)

    if z and world.can_fit(target.map, target.x, target.y, z) then
        return { map = target.map, x = target.x, y = target.y, z = z }
    end

    return nil
end

function energy_vortex.check(caster, target, info)
    local refused = summon.refuse_template(caster, TEMPLATE)

    if refused then
        return refused
    end

    if not place_of(target) then
        return BLOCKED
    end

    return magic.refuse_in_town(target.map, target.x, target.y, target.z)
end

function energy_vortex.cast(caster, target, info)
    local place = place_of(target)

    if not place then
        return
    end

    summon.create(caster, TEMPLATE, place, LEAST_SECONDS + energy_vortex.random(0, SECONDS_SPREAD - 1), info.sound)
end
