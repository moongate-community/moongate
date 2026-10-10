-- ==============================================================================
-- Moongate - scripts/spells/protection.lua
--
-- What it is for:
--   The second circle spell Protection: the target's armor is raised by a tenth
--   of the caster's Magery points, whole, for 1.2 seconds a point of them, as
--   the classic game had it before the defensive spells changed. The raise
--   adds to what absorbs a blow (the combat service reads the props
--   magic.armor and magic.armor_until). A target that has it already is told
--   so and nothing is spent. Called by the spell service with the caster, the
--   target ({ kind = "mobile", serial }) and the data of the spell.
--
-- Functions:
--   protection.check(caster, target, info)   a cliloc number that refuses the
--                                      cast before anything is spent
--   protection.cast(caster, target, info)    the effect, once the cast succeeded
-- ==============================================================================

local magic = require("common.magic")

protection = {}

local IN_EFFECT = 1005559    -- This spell is already in effect.

function protection.check(caster, target, info)
    local who = target.serial
    local dead = magic.refuse_dead(who)

    if dead then
        return dead
    end

    if magic.protected(who) then
        return IN_EFFECT
    end
end

function protection.cast(caster, target, info)
    local who = target.serial

    magic.protect(caster, who)
    magic.show(info, who)
end
