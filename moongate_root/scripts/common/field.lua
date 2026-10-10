-- ==============================================================================
-- Moongate - scripts/common/field.lua
--
-- What it is for:
--   What the spells that raise a line of items on the ground share (Wall of
--   Stone and Fire Field, and the fields of the next circles): which way the
--   line lies, and the pieces put down along it, each kept by the script
--   items/magic_field.lua until its time is up. A spell takes it with
--   local field = require("common.field").
--
-- Props it sets on every piece (read by items/magic_field.lua):
--   field.caster    the serial of who raised it
--   field.until     the time, as world.now(), the piece goes away at
--   field.damage    what a piece of fire does to whoever is in it, 0 for none
--
-- Functions:
--   field.east_to_west(from, x, y)   whether a line raised at x, y lies from east
--                                    to west when raised by someone at from,
--                                    as the classic spells decide it
--   field.check(caster, target, info)   the check of a spell of a field: the
--                                    cliloc 500946 that refuses a cast at a guarded
--                                    town, as the classic spells do; nil for none
--   field.place(caster, target, options)   puts the pieces down; the list of
--                                    their serials, which may be empty
--       options.east_west, options.north_south   the templates of the two ways
--       options.reach    how many pieces on each side of the middle one
--       options.seconds  how long the field lasts
--       options.damage   the damage of a piece of fire, 0 for none
--       options.keep_free   true skips a place where someone stands, as a wall
--
--   A place is skipped when the caster cannot see it, nothing can stand on it
--   (world.standing_z), or an impassable item already lies on it, so two walls
--   do not stack.
--       options.tick     true makes the piece wake every second, as fire does
-- ==============================================================================

local field = {}

-- How far under the place a piece may be put, as the classic spells adjust it.
local DROP = 9

-- How high above its feet the caster looks from, as the cast service measures a line of sight.
local EYE = 14

function field.east_to_west(from, x, y)
    local dx = from.x - x
    local dy = from.y - y
    local rx = (dx - dy) * 44
    local ry = (dx + dy) * 44

    return (rx >= 0 and ry < 0) or (ry >= 0 and rx < 0)
end

local IN_TOWN = 500946   -- You cannot cast this in town!

function field.check(caster, target, info)
    if world.is_guarded(target.map, target.x, target.y, target.z) then
        return IN_TOWN
    end
end

local function standing(map, x, y, z)
    local found = world.standing_z(map, x, y, z)

    if found and z - found <= DROP then
        return found
    end
end

function field.place(caster, target, options)
    local here = mobile.location(caster)

    if not here then
        return {}
    end

    local east_west = field.east_to_west(here, target.x, target.y)
    local template = east_west and options.east_west or options.north_south
    local spots = {}

    -- Which places take a piece is decided before any is put down, so a piece does not hide the next from the caster.
    for step = -options.reach, options.reach do
        local x = east_west and target.x + step or target.x
        local y = east_west and target.y or target.y + step
        local z = standing(target.map, x, y, target.z)

        if z and
            world.line_of_sight(target.map, here.x, here.y, here.z + EYE, x, y, z) and
            world.can_fit(target.map, x, y, z, 0, options.keep_free == true)
        then
            spots[#spots + 1] = { x = x, y = y, z = z }
        end
    end

    local pieces = {}

    for _, spot in ipairs(spots) do
        local piece = item.create(template, target.map, spot.x, spot.y, spot.z)

        if piece then
            item.set_prop(piece, "field.caster", caster)
            item.set_prop(piece, "field.until", world.now() + options.seconds)
            item.set_prop(piece, "field.damage", options.damage or 0)
            item.start_timer(piece, options.tick and "tick" or "expire", options.tick and 1 or options.seconds)
            pieces[#pieces + 1] = piece
        end
    end

    return pieces
end

return field
