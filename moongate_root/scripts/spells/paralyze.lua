-- ==============================================================================
-- Moongate - scripts/spells/paralyze.lua
--
-- What it is for:
--   The fifth circle spell Paralyze: the target is frozen for seven seconds and a
--   fifth of a second a point of the caster's Magery (27 at 100 points), three
--   quarters of it when it resists, and cannot step, turn or cast meanwhile; its
--   own cast is ruined. A target already frozen is told so and nothing is
--   spent. The caster is the aggressor of the target. Called by the spell
--   service with the caster, the target ({ kind = "mobile", serial }) and the
--   data of the spell.
--
-- Functions:
--   paralyze.check(caster, target, info)   a cliloc number that refuses the cast
--                                      before anything is spent
--   paralyze.cast(caster, target, info)    the effect, once the cast succeeded
-- ==============================================================================

local magic = require("common.magic")

paralyze = {}

local ALREADY_FROZEN = 1061923   -- The target is already frozen.
local RESISTING = 501783         -- You feel yourself resisting magical energy.
local BASE_SECONDS = 7
local PER_POINT = 0.2
local RESISTED_SHARE = 0.75

function paralyze.check(caster, target, info)
    local who = target.serial
    local dead = magic.refuse_dead(who)

    if dead then
        return dead
    end

    local flags = mobile.flags(who)

    if flags and flags.frozen then
        return ALREADY_FROZEN
    end
end

function paralyze.cast(caster, target, info)
    local who = target.serial

    if not combat.aggress(caster, who) then
        return
    end

    local seconds = BASE_SECONDS + magic.points(caster, "magery") * PER_POINT

    if magic.resisted(caster, who, info.circle) then
        seconds = seconds * RESISTED_SHARE
        mobile.message_cliloc(who, RESISTING)
    end

    -- A paralysis ruins the spell its target is casting.
    spell.disturb(who)
    mobile.paralyze(who, seconds)
    magic.show(info, who)
end
