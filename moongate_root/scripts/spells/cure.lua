-- ==============================================================================
-- Moongate - scripts/spells/cure.lua
--
-- What it is for:
--   The second circle spell Cure: it may end the poison of the target. The chance
--   is (10000 + 75 a point of Magery - 1750 a level of the poison, the lesser
--   being 1) / 100 per cent, whole; a cure that works tells the target (and the
--   caster, if it is another), one that fails tells the caster. A target that
--   is not poisoned only shows the effect. Called by the spell service with the
--   caster, the target ({ kind = "mobile", serial }) and the data of the spell.
--
-- Functions:
--   cure.check(caster, target, info)   a cliloc number that refuses the cast
--                                      before anything is spent
--   cure.cast(caster, target, info)    the effect, once the cast succeeded
--   cure.random(low, high)             the roll against the chance, math.random
-- ==============================================================================

local magic = require("common.magic")

cure = {}

cure.random = math.random

local CURED_TARGET = 1010058   -- You have cured the target of all poisons!
local CURED = 1010059          -- You have been cured of all poisons.
local FAILED = 1010060         -- You have failed to cure your target!

function cure.check(caster, target, info)
    return magic.refuse_dead(target.serial)
end

function cure.cast(caster, target, info)
    local who = target.serial
    local level = mobile.poison_level(who)

    if level then
        local chance = math.floor((10000 + math.floor(magic.points(caster, "magery") * 75) - (level + 1) * 1750) / 100)

        if chance > cure.random(0, 99) then
            if mobile.cure(who) then
                if caster ~= who then
                    mobile.message_cliloc(caster, CURED_TARGET)
                end

                mobile.message_cliloc(who, CURED)
            end
        else
            mobile.message_cliloc(caster, FAILED)
        end
    end

    magic.show(info, who)
end
