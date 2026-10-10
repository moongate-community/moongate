-- ==============================================================================
-- Moongate - scripts/spells/fire_field.lua
--
-- What it is for:
--   The fourth circle spell Fire Field: five pieces of fire are raised in a line
--   across the way from the caster to the place picked, for twenty seconds. Each
--   piece burns, once a second, whoever steps onto it or stands in it, for 2
--   damage (1 when the burned resists, by a try of Resisting Spells); the caster
--   is the aggressor of the burned, as for a blow. The fire does not block the
--   way. Called by the spell service with the caster, the target
--   ({ kind = "location", map, x, y, z }) and the data of the spell.
--
-- Functions:
--   fire_field.check(caster, target, info)   nothing refuses it
--   fire_field.cast(caster, target, info)    the effect, once the cast succeeded
-- ==============================================================================

local field = require("common.field")

fire_field = {}

local EAST_WEST = "magic_fire_field_ew"
local NORTH_SOUTH = "magic_fire_field_ns"
local SECONDS = 20
local REACH = 2              -- two pieces on each side of the middle one: five
local DAMAGE = 2
local PIECE_EFFECT = 0x376A
local EFFECT_SPEED = 9
local EFFECT_DURATION = 10

function fire_field.cast(caster, target, info)
    if info.sound ~= 0 then
        world.play_sound(target.map, target.x, target.y, target.z, info.sound)
    end

    local pieces = field.place(caster, target, {
        east_west = EAST_WEST, north_south = NORTH_SOUTH, reach = REACH, seconds = SECONDS, damage = DAMAGE, tick = true,
    })

    for _, piece in ipairs(pieces) do
        local at = item.location(piece)

        if at then
            effect.at(at.map, at.x, at.y, at.z, PIECE_EFFECT, { speed = EFFECT_SPEED, duration = EFFECT_DURATION })
        end
    end
end
