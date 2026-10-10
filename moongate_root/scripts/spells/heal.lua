-- ==============================================================================
-- Moongate - scripts/spells/heal.lua
--
-- What it is for:
--   The first circle spell Heal: the target gets back a tenth of the caster's
--   Magery points, whole, and 1 to 5 more, hit points. A poisoned target and one
--   that is dead or at full hits are refused before anything is spent. Called by
--   the spell service with the caster, the target ({ kind = "mobile", serial })
--   and the data of the spell.
--
-- Functions:
--   heal.check(caster, target, info)   a cliloc number that refuses the cast
--                                      before anything is spent
--   heal.cast(caster, target, info)    the effect, once the cast succeeded
--   heal.random(low, high)             the roll of the extra hit points, math.random
-- ==============================================================================

local magic = require("common.magic")

heal = {}

heal.random = math.random

local LEAST = 1
local MOST = 5

function heal.check(caster, target, info)
    return magic.refuse_unhealable(caster, target.serial)
end

function heal.cast(caster, target, info)
    local who = target.serial
    local stats = mobile.stats(who)

    if not stats then
        return
    end

    local amount = math.floor(magic.points(caster, "magery") * 0.1) + heal.random(LEAST, MOST)

    mobile.set_stats(who, { hits = math.min(stats.hits + amount, stats.hits_max) })
    magic.show(info, who)
end
