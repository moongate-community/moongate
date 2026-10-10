-- ==============================================================================
-- Moongate - scripts/spells/mana_drain.lua
--
-- What it is for:
--   The fourth circle spell Mana Drain: it takes from the target a number of
--   mana points, 1 to 100 and at most what it has, unless it resists, which it
--   does nearly always (99 times in a hundred, whatever its skill) and is told
--   so. The target's own cast is disturbed and the caster is its aggressor.
--   Called by the spell service with the caster, the target
--   ({ kind = "mobile", serial }) and the data of the spell.
--
-- Functions:
--   mana_drain.check(caster, target, info)   a cliloc number that refuses the
--                                      cast before anything is spent
--   mana_drain.cast(caster, target, info)    the effect, once the cast succeeded
--   mana_drain.random(low, high)       the roll of the mana taken, math.random
-- ==============================================================================

local magic = require("common.magic")

mana_drain = {}

mana_drain.random = math.random

local RESISTING = 501783     -- You feel yourself resisting magical energy.
local MOST = 100
local RESIST_PERCENT = 99

function mana_drain.check(caster, target, info)
    return magic.refuse_dead(target.serial)
end

function mana_drain.cast(caster, target, info)
    local who = target.serial

    if not combat.aggress(caster, who) then
        return
    end

    spell.disturb(who)

    if magic.resisted(caster, who, info.circle, RESIST_PERCENT) then
        mobile.message_cliloc(who, RESISTING)
    else
        local stats = mobile.stats(who)

        if stats and stats.mana > 0 then
            local taken = mana_drain.random(1, math.min(stats.mana, MOST))

            mobile.set_stats(who, { mana = stats.mana - taken })
        end
    end

    magic.show(info, who)
end
