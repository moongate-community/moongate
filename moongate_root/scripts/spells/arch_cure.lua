-- ==============================================================================
-- Moongate - scripts/spells/arch_cure.lua
--
-- What it is for:
--   The fourth circle spell Arch Cure: the cure of the second circle on everyone
--   alive within two tiles of the place picked, with the sound of the place
--   first. The chance for a poison of the level n (the lesser is 0) is
--   (10000 + 75 a point of Magery - 1750 (n + 2)) / 100 less one per cent,
--   whole, which is a little less than Cure's. The caster is told when any was
--   cured. Called by the spell service with the caster, the target
--   ({ kind = "location", map, x, y, z }) and the data of the spell.
--
-- Functions:
--   arch_cure.check(caster, target, info)   nothing refuses it
--   arch_cure.cast(caster, target, info)    the effect, once the cast succeeded
--   arch_cure.random(low, high)        the roll against the chance, math.random
-- ==============================================================================

local magic = require("common.magic")

arch_cure = {}

arch_cure.random = math.random

local RANGE = 2
local PLACE_SOUND = 0x299
local CURED_TARGET = 1010058   -- You have cured the target of all poisons!

function arch_cure.cast(caster, target, info)
    world.play_sound(target.map, target.x, target.y, target.z, PLACE_SOUND)

    local cured = 0

    for _, who in ipairs(magic.alive_in_range(target.map, target.x, target.y, RANGE)) do
        local level = mobile.poison_level(who)

        if level then
            -- The level the poison would be raised to, as the classic game weighs it.
            local chance = math.floor((10000 + math.floor(magic.points(caster, "magery") * 75) - (level + 1) * 1750) / 100) - 1

            if chance > arch_cure.random(0, 99) and mobile.cure(who) then
                cured = cured + 1
            end
        end

        magic.show(info, who)
    end

    if cured > 0 then
        mobile.message_cliloc(caster, CURED_TARGET)
    end
end
