-- ==============================================================================
-- Moongate - scripts/common/cartography.lua
--
-- What it is for:
--   Draws a map a cartographer has just made, in the facet the cartographer
--   stands in: a local map 64 tiles around the cartographer and 2 more a point
--   of skill on a drawing of 200; a city map and a sea chart farther, on a
--   larger drawing as the skill grows; a world map of Britannia 20 tiles a
--   point of skill around Britain, wherever the cartographer is. The world
--   maps of the other facets keep the area of their template. Called by
--   scripts/common/crafting.lua (the table MADE) with
--   require("common.cartography").draw(user, made, recipe, points). The
--   skill is the base one: there are no skill bonuses yet.
--
-- Functions:
--   cartography.draw(user, made, recipe, points)   sets the area of a made map; false when it could not
-- ==============================================================================

local cartography = {}

local SMALL = 200
local LARGE = 400

local function clamp(value, low, high)
    return math.max(low, math.min(high, value))
end

local FELUCCA = 0

-- A map around the cartographer: how far each side it reaches and the size of its drawing.
local function around(reach, size)
    return function(here)
        return here.x - reach, here.y - reach, here.x + reach, here.y + reach, size, size, here.map
    end
end

-- The area each map shows, by the item made and the skill: a function of where the cartographer stands.
local DRAWINGS = {
    craftedlocalmap = function(points)
        return around(64 + math.floor(points * 2), SMALL)
    end,
    craftedcitymap = function(points)
        return around(math.max(64 + math.floor(points * 4), SMALL), clamp(32 + math.floor(points * 2), SMALL, LARGE))
    end,
    craftedseachart = function(points)
        return around(math.max(64 + math.floor(points * 10), SMALL), clamp(24 + math.floor(points * 3.3), SMALL, LARGE))
    end,
    largeworldmap = function(points)
        local reach = math.floor(points * 20)
        local size = clamp(25 + math.floor(points * 6.6), SMALL, LARGE)

        return function()
            return 1344 - reach, 1600 - reach, 1472 + reach, 1728 + reach, size, size, FELUCCA
        end
    end,
}

function cartography.draw(user, made, recipe, points)
    local drawing = DRAWINGS[recipe.item]

    if not drawing then
        return true
    end

    local here = mobile.location(user)

    return here ~= nil and map.set_bounds(made, drawing(points)(here))
end

return cartography
