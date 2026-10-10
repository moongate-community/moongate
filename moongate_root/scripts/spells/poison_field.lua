-- ==============================================================================
-- Moongate - scripts/spells/poison_field.lua
--
-- What it is for:
--   The fifth circle spell Poison Field: five pieces of poison are raised in a
--   line across the way from the caster to the place picked, for twenty seconds.
--   Whoever steps onto a piece, or stands in it, once a second, is poisoned at
--   the regular level, if the caster may harm it (not a player that looks
--   innocent, unless the caster is a murderer; not the caster's own creatures);
--   the caster is the aggressor. The poison does not block the way. It is
--   refused, before anything is spent, at a guarded town. Called by the spell
--   service with the caster, the target ({ kind = "location", map, x, y, z })
--   and the data of the spell.
--
-- Functions:
--   poison_field.check(caster, target, info)   a cliloc number that refuses the
--                                      cast before anything is spent
--   poison_field.cast(caster, target, info)    the effect, once the cast succeeded
-- ==============================================================================

local field = require("common.field")

poison_field = {}

local EAST_WEST = "magic_poison_field_ew"
local NORTH_SOUTH = "magic_poison_field_ns"
local SECONDS = 20
local REACH = 2              -- two pieces on each side of the middle one: five
local LEVEL = 1              -- the regular poison
local PIECE_EFFECT = 0x376A
local EFFECT_SPEED = 9
local EFFECT_DURATION = 10

function poison_field.check(caster, target, info)
    return field.check(caster, target, info)
end

function poison_field.cast(caster, target, info)
    if info.sound ~= 0 then
        world.play_sound(target.map, target.x, target.y, target.z, info.sound)
    end

    local pieces = field.place(caster, target, {
        east_west = EAST_WEST, north_south = NORTH_SOUTH, reach = REACH, seconds = SECONDS, damage = 0,
        effect = "poison", power = LEVEL, tick = true,
    })

    for _, piece in ipairs(pieces) do
        local at = item.location(piece)

        if at then
            effect.at(at.map, at.x, at.y, at.z, PIECE_EFFECT, { speed = EFFECT_SPEED, duration = EFFECT_DURATION })
        end
    end
end
