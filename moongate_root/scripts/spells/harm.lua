-- ==============================================================================
-- Moongate - scripts/spells/harm.lua
--
-- What it is for:
--   The second circle spell Harm: at once, 1 to 15 damage to the target, a
--   resisting target taking three quarters of it, then scaled as every damage
--   spell is. The damage does not fall with the distance, as in the days of the
--   classic game before the Second Dawn. The caster is the aggressor of the
--   target: a criminal against an innocent, and an NPC fights back. Called by
--   the spell service with the caster, the target ({ kind = "mobile", serial })
--   and the data of the spell.
--
-- Functions:
--   harm.check(caster, target, info)   a cliloc number that refuses the cast
--                                      before anything is spent
--   harm.cast(caster, target, info)    the effect, once the cast succeeded
--   harm.random(low, high)             the roll of the damage, math.random
-- ==============================================================================

local magic = require("common.magic")

harm = {}

harm.random = math.random

local LEAST = 1
local MOST = 15

function harm.check(caster, target, info)
    return magic.refuse_dead(target.serial)
end

function harm.cast(caster, target, info)
    local who = target.serial

    if not magic.aggress(caster, who, info) then
        return
    end

    local damage = magic.damage(caster, who, info, harm.random(LEAST, MOST))

    magic.show(info, who)
    combat.harm(who, damage, caster)
end
