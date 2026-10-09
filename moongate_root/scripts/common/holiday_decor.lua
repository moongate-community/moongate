-- ==============================================================================
-- Moongate - scripts/common/holiday_decor.lua
--
-- What it is for:
--   The decorations of the holiday events. place puts a set of items around the
--   center of the main towns, on Felucca and on Trammel, when an event starts;
--   remove takes them all away when it ends. The serials are kept in a world
--   prop, so the items go away even after a restart, and placing twice
--   (a restart in the middle of the event) puts nothing twice.
--
-- Functions:
--   place(event_id, templates)   puts the templates in turn around each town;
--                                returns how many items were made at once (the
--                                rest follows when the server has serials ready)
--   remove(event_id)             deletes what place made; returns how many
--
-- World prop it keeps:
--   holiday.<event_id>.items   the serials of the decorations, comma separated
-- ==============================================================================

local holiday_decor = {}

-- The towns of data/locations.toml (category Factions/Towns) and the maps that show them.
local TOWNS = { "Britain", "Trinsic", "Vesper", "Minoc", "Yew", "Skara Brae", "Moonglow" }
local TOWN_CATEGORY = "Factions/Towns"
local MAPS = { MapType.Felucca, MapType.Trammel }

-- Where around the center a decoration may stand, in tiles; and how far above or below the center its floor may be.
local OFFSETS = {
    { 3, 3 }, { -3, 3 }, { 3, -3 }, { -3, -3 },
    { 6, 0 }, { -6, 0 }, { 0, 6 }, { 0, -6 }
}
local MAX_FLOOR_DIFFERENCE = 8
local SEARCH_HEIGHT = 10

-- How long to wait for more serials, in seconds.
local RETRY_SECONDS = 2

local function key_of(event_id)
    return "holiday." .. event_id .. ".items"
end

local function center_of(town, map)
    for _, place in ipairs(locations.find(town, map)) do
        if place.name == town and place.category == TOWN_CATEGORY then
            return place
        end
    end

    return nil
end

-- Every free spot around the towns, with the template it takes.
local function spots_of(templates)
    local spots = {}
    local turn = 0

    for _, map in ipairs(MAPS) do
        for _, town in ipairs(TOWNS) do
            local center = center_of(town, map)

            for _, offset in ipairs(center and OFFSETS or {}) do
                local x = center.x + offset[1]
                local y = center.y + offset[2]
                local z = world.standing_z(map, x, y, center.z + SEARCH_HEIGHT)

                if z and math.abs(z - center.z) <= MAX_FLOOR_DIFFERENCE and not world.is_occupied(map, x, y) then
                    turn = turn % #templates + 1
                    spots[#spots + 1] = { template = templates[turn], map = map, x = x, y = y, z = z }
                end
            end
        end
    end

    return spots
end

-- Makes the items from index on. The server keeps only a few serials ready, so item.create may answer nil in a long
-- run: what was made is kept, and the rest follows after a pause. remove ends the work by forgetting the prop.
local function make(event_id, spots, index, serials)
    while index <= #spots do
        local spot = spots[index]
        local serial = item.create(spot.template, spot.map, spot.x, spot.y, spot.z)

        if not serial then
            world.set_prop(key_of(event_id), table.concat(serials, ","))
            timer.after(RETRY_SECONDS, function()
                if world.get_prop(key_of(event_id)) ~= nil then
                    make(event_id, spots, index, serials)
                end
            end)

            return #serials
        end

        serials[#serials + 1] = serial
        index = index + 1
    end

    -- Kept even when empty, so that remove has something to find and a second start does not place again.
    world.set_prop(key_of(event_id), table.concat(serials, ","))

    return #serials
end

function holiday_decor.place(event_id, templates)
    if world.get_prop(key_of(event_id)) ~= nil or #templates == 0 then
        return 0
    end

    return make(event_id, spots_of(templates), 1, {})
end

function holiday_decor.remove(event_id)
    local kept = world.get_prop(key_of(event_id))

    if kept == nil then
        return 0
    end

    local removed = 0

    for serial in tostring(kept):gmatch("%d+") do
        if item.delete(tonumber(serial)) then
            removed = removed + 1
        end
    end

    world.set_prop(key_of(event_id), nil)

    return removed
end

return holiday_decor
