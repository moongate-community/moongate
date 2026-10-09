-- ==============================================================================
-- Moongate - scripts/common/trees.lua
--
-- What it is for:
--   What the scripts that work wood from a tree share: which graphics of the
--   map are trees, and the wood left in a place (the resource "wood" of
--   data/harvest.toml). Used with local trees = require("common.trees").
--
-- Functions:
--   trees.is_tree(graphic)        whether a static of that graphic is a tree
--   trees.has_wood(map, x, y)     whether the place has wood left
--   trees.take_wood(map, x, y)    takes one cut from the place
-- ==============================================================================

local trees = {}

local RESOURCE = "wood"

-- The graphics of the trees, alone or as a range.
local TREES = {
    { 0x0CCA, 0x0CE8 }, { 0x0CF8, 0x0D03 }, { 0x0D41, 0x0D53 }, { 0x0D57, 0x0D69 }, { 0x0D6E, 0x0D7F },
    { 0x0D84, 0x0D90 }, { 0x0D95, 0x0D97 }, { 0x0D99, 0x0D9B }, { 0x0D9D, 0x0D9F }, { 0x0DA1, 0x0DA3 },
    { 0x0DA5, 0x0DA7 }, { 0x0DA9, 0x0DAB }, { 0x12B5, 0x12C7 },
}

function trees.is_tree(graphic)
    for _, range in ipairs(TREES) do
        if graphic >= range[1] and graphic <= range[2] then
            return true
        end
    end

    return false
end

function trees.has_wood(map, x, y)
    return (harvest.amount(RESOURCE, map, x, y) or 0) > 0
end

function trees.take_wood(map, x, y)
    return harvest.take(RESOURCE, map, x, y)
end

return trees
