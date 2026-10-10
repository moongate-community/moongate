-- ==============================================================================
-- Moongate - scripts/spells/flame_strike.lua
--
-- What it is for:
--   The seventh circle spell Flame Strike: a pillar of fire stands on the target
--   and, half a second later, does 27 to 48 damage. A target that resists takes
--   three fifths of it; then it is scaled by the caster's Evaluating
--   Intelligence against the target's Resisting Spells, by the Magery of the
--   caster, and doubled against a monster or an animal. The caster is the
--   aggressor of the target. Called by the spell service with the caster, the
--   target ({ kind = "mobile", serial }) and the data of the spell.
--
-- Functions:
--   flame_strike.check(caster, target, info)   a cliloc number that refuses the
--                                      cast before anything is spent
--   flame_strike.cast(caster, target, info)    the effect, once the cast succeeded
--   flame_strike.random(low, high)     the roll of the damage, math.random
-- ==============================================================================

local magic = require("common.magic")

flame_strike = {}

flame_strike.random = math.random

local LEAST = 27
local MOST = 48
local RESISTED_SHARE = 0.6
local DAMAGE_DELAY = 0.5
local PILLAR = 0x3709
local PILLAR_SPEED = 10
local PILLAR_DURATION = 30

function flame_strike.check(caster, target, info)
    return magic.refuse_dead(target.serial)
end

function flame_strike.cast(caster, target, info)
    local who = target.serial

    if not combat.aggress(caster, who) then
        return
    end

    local damage = magic.damage(caster, who, info, flame_strike.random(LEAST, MOST), RESISTED_SHARE)

    effect.on(who, info.effect ~= 0 and info.effect or PILLAR, { speed = PILLAR_SPEED, duration = PILLAR_DURATION })

    if info.sound ~= 0 then
        mobile.play_sound(who, info.sound)
    end

    magic.harm_after(caster, who, damage, DAMAGE_DELAY)
end
