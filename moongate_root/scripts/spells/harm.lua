-- ==============================================================================
-- Moongate - scripts/spells/harm.lua
--
-- What it is for:
--   The second circle spell Harm: at once, 1 to 15 damage to the target, a
--   resisting target taking three quarters of it, then scaled as every damage
--   spell is. At two tiles the damage is halved and it is a quarter beyond,
--   which is why it is a spell for a fight at arm's length. The caster is the
--   aggressor of the target: a criminal against an innocent, and an NPC fights
--   back. Called by the spell service with the caster, the target
--   ({ kind = "mobile", serial }) and the data of the spell.
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
local NEAR = 1       -- tiles within which the damage is whole
local HALF_RANGE = 2 -- tiles within which it is halved, a quarter beyond

function harm.check(caster, target, info)
    return magic.refuse_dead(target.serial)
end

function harm.cast(caster, target, info)
    local who = target.serial

    if not combat.aggress(caster, who) then
        return
    end

    local base = harm.random(LEAST, MOST)
    local from = mobile.location(caster)
    local to = mobile.location(who)

    if from and to then
        local distance = magic.distance(from, to)

        if distance > HALF_RANGE then
            base = base * 0.25
        elseif distance > NEAR then
            base = base * 0.5
        end
    end

    local damage = magic.damage(caster, who, info, base)

    magic.show(info, who)
    combat.harm(who, damage, caster)
end
