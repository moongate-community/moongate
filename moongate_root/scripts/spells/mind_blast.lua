-- ==============================================================================
-- Moongate - scripts/spells/mind_blast.lua
--
-- What it is for:
--   The fifth circle spell Mind Blast: half a second after the cast the target takes as much damage as the gap
--   between its highest and its lowest stat (Strength, Dexterity, Intelligence, each counted at 150 at most) is
--   worth, halved, scaled by the caster's Evaluating Intelligence against the target's Resisting Spells, by the
--   caster's Magery and doubled against a monster or an animal, and at most 45. A target that resists takes
--   half of it. The caster is the aggressor of the target. Called by the spell service with the caster, the
--   target ({ kind = "mobile", serial }) and the data of the spell.
--
-- Functions:
--   mind_blast.check(caster, target, info)   a cliloc number that refuses the
--                                      cast before anything is spent
--   mind_blast.cast(caster, target, info)    the effect, once the cast succeeded
-- ==============================================================================

local magic = require("common.magic")

mind_blast = {}

local DAMAGE_DELAY = 0.5
local STAT_CAP = 150
local MOST = 45
local RESISTED_SHARE = 0.5
local CASTER_EFFECT = 0x374A
local CASTER_EFFECT_DURATION = 15

function mind_blast.check(caster, target, info)
    return magic.refuse_dead(target.serial)
end

function mind_blast.cast(caster, target, info)
    local who = target.serial

    if not combat.aggress(caster, who) then
        return
    end

    local stats = mobile.stats(who)

    if not stats then
        return
    end

    local high = math.min(math.max(stats.strength, stats.dexterity, stats.intelligence), STAT_CAP)
    local low = math.min(math.min(stats.strength, stats.dexterity, stats.intelligence), STAT_CAP)
    local damage = math.min(magic.damage_scalar(caster, who) * (high - low) / 2, MOST)

    if magic.resisted(caster, who, info.circle) then
        damage = damage * RESISTED_SHARE
        mobile.message_cliloc(who, 501783)
    end

    effect.on(caster, CASTER_EFFECT, { speed = 10, duration = CASTER_EFFECT_DURATION })
    magic.show(info, who)
    magic.harm_after(caster, who, math.max(1, math.floor(damage)), DAMAGE_DELAY)
end
