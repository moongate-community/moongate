-- ==============================================================================
-- Moongate - scripts/spells/fireball.lua
--
-- What it is for:
--   The third circle spell Fireball: a ball of fire flies to the target and,
--   half a second later, does 10 to 16 damage. A target that resists takes
--   three quarters of it; then it is scaled by the caster's Evaluating
--   Intelligence against the target's Resisting Spells, by the Magery of the
--   caster, and doubled against a monster or an animal. The caster is the
--   aggressor of the target. Called by the spell service with the caster, the
--   target ({ kind = "mobile", serial }) and the data of the spell.
--
-- Functions:
--   fireball.check(caster, target, info)   a cliloc number that refuses the
--                                      cast before anything is spent
--   fireball.cast(caster, target, info)    the effect, once the cast succeeded
--   fireball.random(low, high)         the roll of the damage, math.random
-- ==============================================================================

local magic = require("common.magic")

fireball = {}

fireball.random = math.random

local LEAST = 10
local MOST = 16
local DAMAGE_DELAY = 0.5
local BALL = 0x36D4
local BALL_SPEED = 7

function fireball.check(caster, target, info)
    return magic.refuse_dead(target.serial)
end

function fireball.cast(caster, target, info)
    local who = target.serial

    if not combat.aggress(caster, who) then
        return
    end

    effect.moving(caster, who, info.projectile ~= 0 and info.projectile or BALL, {
        speed = info.projectile_speed ~= 0 and info.projectile_speed or BALL_SPEED,
    })

    if info.sound ~= 0 then
        mobile.play_sound(caster, info.sound)
    end

    local damage = magic.damage(caster, who, info, fireball.random(LEAST, MOST))

    timer.after(DAMAGE_DELAY, function()
        -- A caster that left the game in the meantime is to blame for nothing, but the ball still burns.
        if not combat.harm(who, damage, caster) and not mobile.location(caster) then
            combat.harm(who, damage)
        end
    end)
end
