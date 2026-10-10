-- ==============================================================================
-- Moongate - scripts/spells/energy_bolt.lua
--
-- What it is for:
--   The sixth circle spell Energy Bolt: a bolt flies to the target and, half a second later, does 24 to 41
--   damage. A target that resists takes three quarters of it; then it is scaled by the caster's Evaluating
--   Intelligence against the target's Resisting Spells, by the Magery of the caster, and doubled against a
--   monster or an animal. The caster is the aggressor of the target. Called by the spell service with the
--   caster, the target ({ kind = "mobile", serial }) and the data of the spell.
--
-- Functions:
--   energy_bolt.check(caster, target, info)   a cliloc number that refuses the
--                                      cast before anything is spent
--   energy_bolt.cast(caster, target, info)    the effect, once the cast succeeded
--   energy_bolt.random(low, high)      the roll of the damage, math.random
-- ==============================================================================

local magic = require("common.magic")

energy_bolt = {}

energy_bolt.random = math.random

local LEAST = 24
local MOST = 41
local DAMAGE_DELAY = 0.5
local BOLT = 0x379F
local BOLT_SPEED = 7

function energy_bolt.check(caster, target, info)
    return magic.refuse_dead(target.serial)
end

function energy_bolt.cast(caster, target, info)
    local who = target.serial

    if not magic.aggress(caster, who, info) then
        return
    end

    effect.moving(info.reflector or caster, who, info.projectile ~= 0 and info.projectile or BOLT, {
        speed = info.projectile_speed ~= 0 and info.projectile_speed or BOLT_SPEED,
    })

    if info.sound ~= 0 then
        mobile.play_sound(caster, info.sound)
    end

    local damage = magic.damage(caster, who, info, energy_bolt.random(LEAST, MOST))

    magic.harm_after(caster, who, damage, DAMAGE_DELAY)
end
