-- ==============================================================================
-- Moongate - scripts/spells/summon_creature.lua
--
-- What it is for:
--   The fifth circle spell Summon Creature: an animal picked at random from a fixed list (a polar bear, a brown
--   bear, a black bear, a horse, a walrus, a chicken, a scorpion, a giant serpent, a llama, an alligator, a
--   grey wolf, a slime, an eagle, a gorilla, a snow leopard, a pig, a hind or a rabbit) is called beside the
--   caster and fights for it, by the pet order guard, for as many seconds as the caster has points of Magery,
--   or until it is dispelled or killed. It is refused, before anything is spent, when the caster has fewer than
--   two followers' room left and when no place beside the caster is free. Called by the spell service with the
--   caster, the target (none) and the data of the spell.
--
-- Functions:
--   summon_creature.check(caster, target, info)   a cliloc number that refuses
--                                      the cast before anything is spent
--   summon_creature.cast(caster, target, info)    the effect, once the cast
--                                      succeeded
--   summon_creature.random(low, high)  the pick of the animal, math.random
-- ==============================================================================

local magic = require("common.magic")
local summon = require("common.summon")

summon_creature = {}

summon_creature.random = math.random

local BLOCKED = 501942   -- That location is blocked.
local SLOTS = 2
local ANIMALS = {
    "polarbear", "brownbear", "blackbear", "horse", "walrus", "chicken", "scorpion", "giantserpent", "llama",
    "alligator", "greywolf", "slime", "eagle", "gorilla", "snowleopard", "pig", "hind", "rabbit",
}

function summon_creature.check(caster, target, info)
    local refused = summon.refuse(caster, SLOTS)

    if refused then
        return refused
    end

    if not summon.near(caster) then
        return BLOCKED
    end
end

function summon_creature.cast(caster, target, info)
    local place = summon.near(caster)

    if not place then
        return
    end

    local seconds = math.max(magic.points(caster, "magery"), 1)

    summon.create(caster, ANIMALS[summon_creature.random(1, #ANIMALS)], place, seconds, info.sound)
end
