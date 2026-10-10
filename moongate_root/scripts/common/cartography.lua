-- ==============================================================================
-- Moongate - scripts/common/cartography.lua
--
-- What it is for:
--   Draws a map a cartographer has just made, in the facet the cartographer
--   stands in: a local map 64 tiles around the cartographer and 2 more a point
--   of skill on a drawing of 200; a city map and a sea chart farther, on a
--   larger drawing as the skill grows. A world map keeps the area of its
--   template. Called by scripts/common/crafting.lua (the table MADE) with
--   require("common.cartography").draw(user, made, recipe, points).
--
-- Functions:
--   cartography.draw(user, made, recipe, points)   sets the area of a made map
-- ==============================================================================

local cartography = {}

local SMALL = 200
local LARGE = 400

local function clamp(value, low, high)
    return math.max(low, math.min(high, value))
end

-- How far each side of the cartographer a map reaches, and the size of its drawing, by the item made and the skill.
local DRAWINGS = {
    craftedlocalmap = function(points)
        return 64 + math.floor(points * 2), SMALL
    end,
    craftedcitymap = function(points)
        return math.max(64 + math.floor(points * 4), SMALL), clamp(32 + math.floor(points * 2), SMALL, LARGE)
    end,
    craftedseachart = function(points)
        return math.max(64 + math.floor(points * 10), SMALL), clamp(24 + math.floor(points * 3.3), SMALL, LARGE)
    end,
}

function cartography.draw(user, made, recipe, points)
    local drawing = DRAWINGS[recipe.item]
    local here = mobile.location(user)

    if not drawing or not here then
        return
    end

    local reach, size = drawing(points)
    map.set_bounds(made, here.x - reach, here.y - reach, here.x + reach, here.y + reach, size, size, here.map)
end

return cartography
