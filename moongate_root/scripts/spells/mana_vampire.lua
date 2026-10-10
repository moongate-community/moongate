-- ==============================================================================
-- Moongate - scripts/spells/mana_vampire.lua
--
-- What it is for:
--   The seventh circle spell Mana Vampire: all the mana of the target goes to the caster (up to its own
--   maximum), unless the target resists, which it does nearly always (98 times in a hundred, whatever its
--   skill) and is told so. The target's own cast is ruined and its paralysis, if it had one, is ended. The
--   caster is the aggressor of the target. Called by the spell service with the caster, the target ({ kind =
--   "mobile", serial }) and the data of the spell.
--
-- Functions:
--   mana_vampire.check(caster, target, info)   a cliloc number that refuses the
--                                      cast before anything is spent
--   mana_vampire.cast(caster, target, info)    the effect, once the cast succeeded
-- ==============================================================================

local magic = require("common.magic")

mana_vampire = {}

local RESISTING = 501783     -- You feel yourself resisting magical energy.
local RESIST_PERCENT = 98

function mana_vampire.check(caster, target, info)
    return magic.refuse_dead(target.serial)
end

function mana_vampire.cast(caster, target, info)
    local who = target.serial

    if not combat.aggress(caster, who) then
        return
    end

    spell.disturb(who)
    mobile.release_paralysis(who)

    if magic.resisted(caster, who, info.circle, RESIST_PERCENT) then
        mobile.message_cliloc(who, RESISTING)
    else
        local theirs = mobile.stats(who)
        local ours = mobile.stats(caster)

        if theirs and ours and theirs.mana > 0 then
            local taken = math.min(theirs.mana, ours.mana_max - ours.mana)

            mobile.set_stats(who, { mana = 0 })
            mobile.set_stats(caster, { mana = ours.mana + math.max(taken, 0) })
        end
    end

    magic.show(info, who)
end
