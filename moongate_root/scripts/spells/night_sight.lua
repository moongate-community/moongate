-- ==============================================================================
-- Moongate - scripts/spells/night_sight.lua
--
-- What it is for:
--   The first circle spell Night Sight: the target sees in the dark for 15 to
--   39 minutes, in a light that grows with the caster's Magery (the whole 26
--   at 100 points). A target that has night sight already is told so and the
--   cast is spent. Called by the spell service with the caster, the target
--   ({ kind = "mobile", serial }) and the data of the spell.
--
-- Functions:
--   night_sight.check(caster, target, info)   a cliloc number that refuses the
--                                      cast before anything is spent
--   night_sight.cast(caster, target, info)    the effect, once the cast succeeded
--   night_sight.random(low, high)      the roll of the minutes, math.random
-- ==============================================================================

local magic = require("common.magic")

night_sight = {}

night_sight.random = math.random

local FULL_LEVEL = 26
local LEAST_MINUTES = 15
local MOST_MINUTES = 39
local SECONDS_A_MINUTE = 60
local OWN_ALREADY = "You already have nightsight."
local OTHER_ALREADY = "They already have nightsight."

function night_sight.check(caster, target, info)
    return magic.refuse_dead(target.serial)
end

function night_sight.cast(caster, target, info)
    local who = target.serial
    local level = math.max(math.floor(FULL_LEVEL * magic.points(caster, "magery") / 100), 0)
    local seconds = night_sight.random(LEAST_MINUTES, MOST_MINUTES) * SECONDS_A_MINUTE

    if mobile.set_night_sight(who, level, seconds) then
        magic.show(info, who)
    else
        mobile.message(caster, caster == who and OWN_ALREADY or OTHER_ALREADY)
    end
end
