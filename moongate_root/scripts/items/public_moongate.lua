-- ==============================================================================
-- Moongate - scripts/items/public_moongate.lua
--
-- What it is for:
--   The item script of the public moongates (item template
--   decoration_public_moongate), as ModernUO's PublicMoongate: a player who
--   walks onto a gate, or double clicks it from the next cell, picks a city
--   from a gump with one page per map and stands there at once. The maps and
--   cities come from data/moongates.toml through moongates.facets().
--
-- Functions:
--   on_move_over(serial, who)   a player stepped onto the gate
--   on_use(serial, user)        a player double clicked the gate
-- ==============================================================================

public_moongate = {}

-- How far from the gate a traveller may stand, in cells.
local use_range = 1

local open_sound = 0x20E
local arrival_sound = 0x1FE

-- Server message: "You have moved too far away to use this."
local too_far_message = 2749

-- Client texts: "Pick your destination:" and "CANCEL".
local title_cliloc = 1012011
local cancel_cliloc = 1011012

local row_height = 25

-- As ModernUO: the player may have walked away while the gump was open.
local function near(serial, who)
    return item.in_range(serial, who, use_range) and mobile.location(who) or nil
end

-- Called by the button of a city. The player may have walked away while the gump was open.
local function travel(serial, who, facet, destination)
    local at = near(serial, who)

    if not at then
        mobile.message(who, localization.get(too_far_message))

        return
    end

    -- Already there: the gate of this very city.
    if at.map == facet.map and math.abs(at.x - destination.x) <= use_range and
        math.abs(at.y - destination.y) <= use_range then
        return
    end

    if mobile.teleport(who, destination.x, destination.y, destination.z, facet.map) then
        mobile.play_sound(who, arrival_sound)
    end
end

-- The page of the player's own map opens first, as in ModernUO; the tabs keep the order of the file.
local function pages_of(facets, map)
    local pages = {}
    local next_page = 2

    for index, facet in ipairs(facets) do
        if facet.map == map then
            pages[index] = 1
        else
            pages[index] = next_page
            next_page = next_page + 1
        end
    end

    -- No gate of the player's map is listed: the first facet opens.
    if next_page == #facets + 2 then
        for index = 1, #facets do
            pages[index] = index
        end
    end

    return pages
end

local function open(serial, who)
    local at = near(serial, who)
    local facets = moongates.facets()

    if not at or #facets == 0 then
        return
    end

    local pages = pages_of(facets, at.map)
    -- Not the name of this script: a gump answers to the script table of its own id.
    local g = gump.create("moongate_destinations", 100, 100)
    g:background{ x = 0, y = 0, gump = 5054, width = 380, height = 280 }
    g:html{ x = 5, y = 5, width = 200, height = 20, cliloc = title_cliloc }
    g:button{ x = 10, y = 235, up = 4005, down = 4007, on_click = function() end }
    g:html{ x = 45, y = 235, width = 140, height = 25, cliloc = cancel_cliloc }

    -- The tabs show on every page.
    for index, facet in ipairs(facets) do
        local y = 35 + (index - 1) * row_height
        g:button{ x = 10, y = y, up = 2117, down = 2118, page = pages[index] }
        g:html{ x = 30, y = y, width = 150, height = 20, cliloc = facet.cliloc }
    end

    -- One page per map, in page order.
    for page = 1, #facets do
        for index, facet in ipairs(facets) do
            if pages[index] == page then
                g:page()
                g:html{ x = 30, y = 35 + (index - 1) * row_height, width = 150, height = 20, cliloc = facet.selected_cliloc }

                for row, destination in ipairs(facet.destinations) do
                    local y = 35 + (row - 1) * row_height
                    g:button{ x = 200, y = y, up = 4005, down = 4007, on_click = function(player)
                        travel(serial, player, facet, destination)
                    end }
                    g:html{ x = 235, y = y, width = 140, height = 20, cliloc = destination.cliloc }
                end
            end
        end
    end

    if gump.send(who, g, {}) then
        mobile.play_sound(who, open_sound)
    end
end

-- Called when a player steps onto the gate.
function public_moongate.on_move_over(serial, who)
    open(serial, who)
end

-- Called when a player double clicks the gate.
function public_moongate.on_use(serial, user)
    open(serial, user)
end
