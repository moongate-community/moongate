-- ==============================================================================
-- Moongate - scripts/common/smithy.lua
--
-- What it is for:
--   What a smithy holds: the graphics of the anvils and of the forges, as the
--   ground items and the statics of the map show them, and whether a player
--   stands near one. Shared by the smelting of ore (scripts/items/ore.lua) and
--   blacksmithing (scripts/common/crafting.lua). Used with
--   local smithy = require("common.smithy").
--
-- Functions:
--   smithy.is_anvil(graphic)          whether a graphic is an anvil
--   smithy.is_forge(graphic)          whether a graphic is a forge
--   smithy.near(user, test, range)    whether a ground item or a static within range tiles, and 16 above or below, passes test
--   smithy.at_anvil_and_forge(user)   whether the player stands within 2 tiles of an anvil and of a forge
-- ==============================================================================

local smithy = {}

-- How far an anvil and a forge may be from a smith, in tiles, and above or below: one on another floor is not there.
local RANGE = 2
local HEIGHT = 16

-- The graphics, alone or as a range.
local ANVILS = {
    { 0x0FAF, 0x0FB0 }, { 0x2DD5, 0x2DD6 },
}

local FORGES = {
    { 0x0FB1, 0x0FB1 }, { 0x197A, 0x19A9 }, { 0x2DD8, 0x2DD8 },
}

local function within(graphic, ranges)
    for _, range in ipairs(ranges) do
        if graphic >= range[1] and graphic <= range[2] then
            return true
        end
    end

    return false
end

function smithy.is_anvil(graphic)
    return within(graphic or 0, ANVILS)
end

function smithy.is_forge(graphic)
    return within(graphic or 0, FORGES)
end

function smithy.near(user, test, range)
    local here = mobile.location(user)

    if not here then
        return false
    end

    for _, static in ipairs(world.statics(here.map, here.x, here.y, range)) do
        if test(static.graphic) and math.abs(static.z - here.z) <= HEIGHT then
            return true
        end
    end

    for _, serial in ipairs(world.items_in_range(here.map, here.x, here.y, range)) do
        local there = item.location(serial)

        if test(item.item_id(serial)) and there and math.abs(there.z - here.z) <= HEIGHT then
            return true
        end
    end

    return false
end

function smithy.at_anvil_and_forge(user)
    return smithy.near(user, smithy.is_anvil, RANGE) and smithy.near(user, smithy.is_forge, RANGE)
end

return smithy
