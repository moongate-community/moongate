-- ==============================================================================
-- Moongate - scripts/spells/wall_of_stone.lua
--
-- What it is for:
--   The third circle spell Wall of Stone: three pieces of stone wall are raised
--   in a line across the way from the caster to the place picked, which block
--   movement for ten seconds and then go away. A piece is not raised where a
--   mobile stands or where nothing can stand. It is refused, before anything is spent, at a
--   guarded town. Called by the spell service with the caster, the target
--   ({ kind = "location", map, x, y, z }) and the data of the spell.
--
-- Functions:
--   wall_of_stone.check(caster, target, info)   a cliloc number that refuses the
--                                      cast before anything is spent
--   wall_of_stone.cast(caster, target, info)    the effect, once the cast succeeded
-- ==============================================================================

local field = require("common.field")

wall_of_stone = {}

local TEMPLATE = "magic_wall_of_stone"
local SECONDS = 10
local REACH = 1              -- a piece on each side of the middle one: three
local PIECE_EFFECT = 0x376A
local EFFECT_SPEED = 9
local EFFECT_DURATION = 10

function wall_of_stone.check(caster, target, info)
    return field.check(caster, target, info)
end

function wall_of_stone.cast(caster, target, info)
    if info.sound ~= 0 then
        world.play_sound(target.map, target.x, target.y, target.z, info.sound)
    end

    local pieces = field.place(caster, target, {
        east_west = TEMPLATE, north_south = TEMPLATE, reach = REACH, seconds = SECONDS, keep_free = true,
    })

    for _, piece in ipairs(pieces) do
        local at = item.location(piece)

        if at then
            effect.at(at.map, at.x, at.y, at.z, PIECE_EFFECT, { speed = EFFECT_SPEED, duration = EFFECT_DURATION })
        end
    end
end
