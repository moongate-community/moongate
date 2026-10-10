-- ==============================================================================
-- Moongate - scripts/spells/magic_arrow.lua
--
-- What it is for:
--   The first circle spell Magic Arrow: an arrow of fire flies to the target
--   and, half a second later, does 4 to 7 damage. A target that resists takes
--   three quarters of it; then it is scaled by the caster's Evaluating
--   Intelligence against the target's Resisting Spells, by the Magery of the
--   caster, and doubled against a monster or an animal. It is harmful: the
--   caster is the aggressor of the target, which fights back if it is an NPC.
--   Called by the spell service with the caster, the target
--   ({ kind = "mobile", serial }) and the data of the spell.
--
-- Functions:
--   magic_arrow.check(caster, target, info)   a cliloc number that refuses the
--                                      cast before anything is spent
--   magic_arrow.cast(caster, target, info)    the effect, once the cast succeeded
--   magic_arrow.random(low, high)      the roll of the damage, math.random
-- ==============================================================================

local magic = require("common.magic")

magic_arrow = {}

magic_arrow.random = math.random

local LEAST = 4
local MOST = 7
local DAMAGE_DELAY = 0.5
local ARROW = 0x36E4
local ARROW_SPEED = 5

function magic_arrow.check(caster, target, info)
    return magic.refuse_dead(target.serial)
end

function magic_arrow.cast(caster, target, info)
    local who = target.serial

    combat.aggress(caster, who)
    effect.moving(caster, who, info.projectile ~= 0 and info.projectile or ARROW, {
        speed = info.projectile_speed ~= 0 and info.projectile_speed or ARROW_SPEED,
    })

    if info.sound ~= 0 then
        mobile.play_sound(caster, info.sound)
    end

    local damage = magic.damage(caster, who, info, magic_arrow.random(LEAST, MOST))

    timer.after(DAMAGE_DELAY, function()
        combat.harm(who, damage, caster)
    end)
end
