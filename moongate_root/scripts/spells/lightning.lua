-- ==============================================================================
-- Moongate - scripts/spells/lightning.lua
--
-- What it is for:
--   The fourth circle spell Lightning: a bolt strikes the target and at once
--   does 12 to 20 damage. A target that resists takes three quarters of it;
--   then it is scaled by the caster's Evaluating Intelligence against the
--   target's Resisting Spells, by the Magery of the caster, and doubled against
--   a monster or an animal. The caster is the aggressor of the target. Called
--   by the spell service with the caster, the target
--   ({ kind = "mobile", serial }) and the data of the spell.
--
-- Functions:
--   lightning.check(caster, target, info)   a cliloc number that refuses the
--                                      cast before anything is spent
--   lightning.cast(caster, target, info)    the effect, once the cast succeeded
--   lightning.random(low, high)        the roll of the damage, math.random
-- ==============================================================================

local magic = require("common.magic")

lightning = {}

lightning.random = math.random

local LEAST = 12
local MOST = 20

function lightning.check(caster, target, info)
    return magic.refuse_dead(target.serial)
end

function lightning.cast(caster, target, info)
    local who = target.serial

    if not magic.aggress(caster, who, info) then
        return
    end

    local damage = magic.damage(caster, who, info, lightning.random(LEAST, MOST))

    effect.lightning(who)

    if info.sound ~= 0 then
        mobile.play_sound(who, info.sound)
    end

    combat.harm(who, damage, caster)
end
