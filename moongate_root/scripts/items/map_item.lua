-- ==============================================================================
-- Moongate - scripts/items/map_item.lua
--
-- What it is for:
--   The item script of the maps, with script_id = "map_item": double clicked in
--   the backpack or on the ground within 2 tiles, it opens the map on the area
--   it shows, its own or the one of its preset template, with the course of
--   pins plotted on it. A blank map, with no area yet, does not open.
--
-- Functions:
--   map_item.on_use(serial, user)   opens the map for the player
-- ==============================================================================

map_item = {}

local REACH = 2
local TOO_FAR = 500446 -- That is too far away.

function map_item.on_use(serial, user)
    if item.owner(serial) ~= user and not item.in_range(serial, user, REACH) then
        mobile.message_cliloc(user, TOO_FAR)

        return true
    end

    map.display(user, serial)

    return true
end
