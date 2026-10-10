-- ==============================================================================
-- Moongate - scripts/common/heat.lua
--
-- What it is for:
--   What a cook needs to stand near: the graphics of the ovens and of every
--   source of heat (ovens, fireplaces, campfires, fire pits, heating stands,
--   fire fields, braziers and forges), as the ground items and the statics of
--   the map show them. Used by cooking (scripts/common/crafting.lua) with
--   local heat = require("common.heat").
--
-- Functions:
--   heat.is_oven(graphic)   whether a graphic is an oven
--   heat.is_fire(graphic)   whether a graphic gives heat to cook on
--   heat.at_oven(user)      whether the player stands within 2 tiles of an oven
--   heat.at_fire(user)      whether the player stands within 2 tiles of a source of heat
-- ==============================================================================

local smithy = require("common.smithy")

local heat = {}

-- How far an oven or a fire may be from a cook, in tiles.
local RANGE = 2

-- The graphics, as ranges.
local OVENS = {
    { 0x0461, 0x046F }, { 0x092B, 0x093F }, { 0x2DDB, 0x2DDC },
}

local FIRES = {
    { 0x0461, 0x048E }, { 0x092B, 0x096C }, { 0x0DE3, 0x0DE9 }, { 0x0FAC, 0x0FAC }, { 0x184A, 0x184C },
    { 0x184E, 0x1850 }, { 0x398C, 0x399F }, { 0x2DDB, 0x2DDC }, { 0x19AA, 0x19BB }, { 0x197A, 0x19A9 },
    { 0x0FB1, 0x0FB1 }, { 0x2DD8, 0x2DD8 },
}

local function within(graphic, ranges)
    for _, range in ipairs(ranges) do
        if graphic >= range[1] and graphic <= range[2] then
            return true
        end
    end

    return false
end

function heat.is_oven(graphic)
    return within(graphic or 0, OVENS)
end

function heat.is_fire(graphic)
    return within(graphic or 0, FIRES)
end

function heat.at_oven(user)
    return smithy.near(user, heat.is_oven, RANGE)
end

function heat.at_fire(user)
    return smithy.near(user, heat.is_fire, RANGE)
end

return heat
