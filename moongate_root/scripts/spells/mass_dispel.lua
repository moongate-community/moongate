-- ==============================================================================
-- Moongate - scripts/spells/mass_dispel.lua
--
-- What it is for:
--   The seventh circle spell Mass Dispel: every summoned creature within eight tiles of the place picked gets
--   the Dispel of the sixth circle (the chance (50 + 100 a point of Magery above its difficulty / twice its
--   focus) per cent, each its own roll): one that is undone goes in a puff, one that holds out shows it and the
--   caster is its aggressor. It is refused, before anything is spent, when there is no summoned creature there.
--   Called by the spell service with the caster, the target ({ kind = "location", map, x, y, z }) and the data
--   of the spell.
--
-- Functions:
--   mass_dispel.check(caster, target, info)   a cliloc number that refuses the
--                                      cast before anything is spent
--   mass_dispel.cast(caster, target, info)    the effect, once the cast succeeded
-- ==============================================================================

local magic = require("common.magic")
local summon = require("common.summon")

mass_dispel = {}

local WONT_WORK = 501857   -- This spell won't work on that!
local RANGE = 8
local PUFF = 0x3728
local PUFF_SPEED = 8
local PUFF_DURATION = 20
local HOLD_EFFECT = 0x3779
local HOLD_SPEED = 10
local HOLD_DURATION = 20

-- The summoned creatures near the place that can be harmed.
local function summons_near(target)
    local found = {}

    for _, who in ipairs(world.mobiles_in_range(target.map, target.x, target.y, RANGE)) do
        if not mobile.is_player(who) and summon.is_summoned(who) then
            found[#found + 1] = who
        end
    end

    return found
end

function mass_dispel.check(caster, target, info)
    if #summons_near(target) == 0 then
        return WONT_WORK
    end
end

function mass_dispel.cast(caster, target, info)
    for _, who in ipairs(summons_near(target)) do
        local difficulty, focus = summon.dispel_data(who)
        local at = npc.location(who)

        if difficulty and at then
            if magic.dispel_chance(caster, difficulty, focus) > magic.random() then
                effect.at(at.map, at.x, at.y, at.z, PUFF, { speed = PUFF_SPEED, duration = PUFF_DURATION })
                world.play_sound(at.map, at.x, at.y, at.z, info.sound ~= 0 and info.sound or 0x201)
                summon.dismiss(who)
            else
                if npc.get_prop(who, "owner") ~= caster then
                    combat.aggress(caster, who)
                end

                effect.on(who, HOLD_EFFECT, { speed = HOLD_SPEED, duration = HOLD_DURATION })
            end
        end
    end
end
