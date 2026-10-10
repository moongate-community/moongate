-- ==============================================================================
-- Moongate - scripts/spells/greater_heal.lua
--
-- What it is for:
--   The fourth circle spell Greater Heal: the target gets back four tenths of
--   the caster's Magery points, whole, and 1 to 10 more, hit points. A poisoned
--   target and one that is dead or at full hits are refused before anything is
--   spent. Called by the spell service with the caster, the target
--   ({ kind = "mobile", serial }) and the data of the spell.
--
-- Functions:
--   greater_heal.check(caster, target, info)   a cliloc number that refuses the
--                                      cast before anything is spent
--   greater_heal.cast(caster, target, info)    the effect, once the cast succeeded
--   greater_heal.random(low, high)     the roll of the extra hit points, math.random
-- ==============================================================================

local magic = require("common.magic")

greater_heal = {}

greater_heal.random = math.random

local LEAST = 1
local MOST = 10

function greater_heal.check(caster, target, info)
    return magic.refuse_unhealable(caster, target.serial)
end

function greater_heal.cast(caster, target, info)
    local who = target.serial
    local stats = mobile.stats(who)

    if not stats then
        return
    end

    local amount = math.floor(magic.points(caster, "magery") * 0.4) + greater_heal.random(LEAST, MOST)

    mobile.set_stats(who, { hits = math.min(stats.hits + amount, stats.hits_max) })
    magic.show(info, who)
end
