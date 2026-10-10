-- ==============================================================================
-- Moongate - scripts/spells/dispel.lua
--
-- What it is for:
--   The sixth circle spell Dispel: a summoned creature (common/summon.lua) the
--   caster picks may be undone, in a puff, with the chance (50 + 100 a point of
--   Magery above its difficulty / twice its focus) per cent: the Blade Spirits
--   are easy, the elementals and the daemon hard. A creature that holds out is
--   told so and the caster is its aggressor. Anything that is no summoned
--   creature is refused, before anything is spent: "That cannot be dispelled".
--   Called by the spell service with the caster, the target ({ kind = "mobile",
--   serial }) and the data of the spell.
--
-- Functions:
--   dispel.check(caster, target, info)   a cliloc number that refuses the cast
--                                      before anything is spent
--   dispel.cast(caster, target, info)    the effect, once the cast succeeded
-- ==============================================================================

local magic = require("common.magic")
local summon = require("common.summon")

dispel = {}

local CANNOT_DISPEL = 1005049   -- That cannot be dispelled.
local RESISTED = 1010084        -- The creature resisted the attempt to dispel it!
local PUFF = 0x3728
local PUFF_SPEED = 8
local PUFF_DURATION = 20
local HOLD_EFFECT = 0x3779
local HOLD_SPEED = 10
local HOLD_DURATION = 20

function dispel.check(caster, target, info)
    if not summon.is_summoned(target.serial) then
        return CANNOT_DISPEL
    end
end

function dispel.cast(caster, target, info)
    local who = target.serial
    local difficulty, focus = summon.dispel_data(who)

    if not difficulty then
        return
    end

    -- Dispelling one's own summon is no attack.
    if npc.get_prop(who, "owner") ~= caster and not combat.aggress(caster, who) then
        return
    end

    local at = npc.location(who)

    if not at then
        return
    end

    if magic.dispel_chance(caster, difficulty, focus) > magic.random() then
        effect.at(at.map, at.x, at.y, at.z, PUFF, { speed = PUFF_SPEED, duration = PUFF_DURATION })
        world.play_sound(at.map, at.x, at.y, at.z, info.sound ~= 0 and info.sound or 0x201)
        summon.dismiss(who)
    else
        effect.on(who, HOLD_EFFECT, { speed = HOLD_SPEED, duration = HOLD_DURATION })
        mobile.message_cliloc(caster, RESISTED)
    end
end
