-- ==============================================================================
-- Moongate - scripts/spells/energy_field.lua
--
-- What it is for:
--   The seventh circle spell Energy Field: five pieces of energy are raised in a line across the way from the
--   caster to the place picked, for two seconds and 0.28 of a second a point of the caster's Magery (30 at 100
--   points). A piece is a wall: nobody walks through it. It is refused, before anything is spent, at a guarded
--   town. Called by the spell service with the caster, the target ({ kind = "location", map, x, y, z }) and the
--   data of the spell.
--
-- Functions:
--   energy_field.check(caster, target, info)   a cliloc number that refuses the
--                                      cast before anything is spent
--   energy_field.cast(caster, target, info)    the effect, once the cast succeeded
-- ==============================================================================

local field = require("common.field")
local magic = require("common.magic")

energy_field = {}

local EAST_WEST = "magic_energy_field_ew"
local NORTH_SOUTH = "magic_energy_field_ns"
local REACH = 2              -- two pieces on each side of the middle one: five
local BASE_SECONDS = 2
local PER_POINT = 0.28
local PIECE_EFFECT = 0x376A
local EFFECT_SPEED = 9
local EFFECT_DURATION = 10

function energy_field.check(caster, target, info)
    return field.check(caster, target, info)
end

function energy_field.cast(caster, target, info)
    if info.sound ~= 0 then
        world.play_sound(target.map, target.x, target.y, target.z, info.sound)
    end

    local seconds = BASE_SECONDS + magic.points(caster, "magery") * PER_POINT
    local pieces = field.place(caster, target, {
        east_west = EAST_WEST, north_south = NORTH_SOUTH, reach = REACH, seconds = seconds,
    })

    for _, piece in ipairs(pieces) do
        local at = item.location(piece)

        if at then
            effect.at(at.map, at.x, at.y, at.z, PIECE_EFFECT, { speed = EFFECT_SPEED, duration = EFFECT_DURATION })
        end
    end
end
