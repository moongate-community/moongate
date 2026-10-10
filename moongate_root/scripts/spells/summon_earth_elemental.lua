-- ==============================================================================
-- Moongate - scripts/spells/summon_earth_elemental.lua
--
-- What it is for:
--   The eighth circle spell Summon Earth Elemental: an earth elemental (earthele_summon) is called
--   beside the caster and fights for it, by the pet order guard, for as many
--   seconds as the caster has points of Magery, or until it is dispelled or
--   killed. It counts for the control slots of its template as a follower. It is
--   refused, before anything is spent, when the caster has too many followers
--   for it and when no place beside the caster is free. Called by the spell
--   service with the caster, the target (none) and the data of the spell.
--
-- Functions:
--   summon_earth_elemental.check(caster, target, info)   a cliloc number that refuses the cast
--                                      before anything is spent
--   summon_earth_elemental.cast(caster, target, info)    the effect, once the cast succeeded
-- ==============================================================================

local summon = require("common.summon")
local magic = require("common.magic")

summon_earth_elemental = {}

local BLOCKED = 501942   -- That location is blocked.
local TEMPLATE = "earthele_summon"

function summon_earth_elemental.check(caster, target, info)
    local refused = summon.refuse_template(caster, TEMPLATE)

    if refused then
        return refused
    end

    if not summon.near(caster) then
        return BLOCKED
    end
end

function summon_earth_elemental.cast(caster, target, info)
    local place = summon.near(caster)

    if not place then
        return
    end

    local seconds = math.max(magic.points(caster, "magery"), 1)

    summon.create(caster, TEMPLATE, place, seconds, info.sound)
end
