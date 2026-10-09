-- ==============================================================================
-- Moongate - scripts/items/blade.lua
--
-- What it is for:
--   The item script of the blades, the knives, the daggers and the swords: a
--   player double clicks one it carries, picks a tree within 2 tiles, and hacks
--   one kindling off it. A template uses it with script_id = "blade".
--
--   No skill is tried and nothing is waited for. The tree must stand in a place
--   with wood left (the resource "wood" of data/harvest.toml), but kindling
--   takes none of it: an axe gets the logs.
--
-- Functions:
--   on_use(serial, user)   the player user double clicks the blade serial
-- ==============================================================================

local trees = require("common.trees")

blade = {}

-- How far the tree may be, in tiles.
local RANGE = 2

local KINDLING = "0x0de1_kindling"

-- The sound of the blade on the wood.
local HACK_SOUND = 0x13E

-- Client texts.
local USE_ON_WHAT = 1010018   -- What do you want to use this item on?
local NOT_ON_THAT = 500494    -- You can't use a bladed item on that!
local TOO_FAR = 500446        -- That is too far away.
local NO_WOOD = 500493        -- There's not enough wood here to harvest.
local NO_ROOM = 500497        -- You can't place any wood into your backpack!
local KINDLED = 500491        -- You put some kindling into your backpack.

-- The player picked what to use the blade on.
local function hack(tool, user, picked)
    if picked.kind == "canceled" then
        return
    end

    local here = mobile.location(user)

    if not here or mobile.is_dead(user) or item.owner(tool) ~= user then
        return
    end

    if picked.kind ~= "location" or not trees.is_tree(picked.graphic or 0) then
        mobile.message_cliloc(user, NOT_ON_THAT)

        return
    end

    if here.map ~= picked.map or math.abs(here.x - picked.x) > RANGE or math.abs(here.y - picked.y) > RANGE then
        mobile.message_cliloc(user, TOO_FAR)

        return
    end

    if not trees.has_wood(picked.map, picked.x, picked.y) then
        mobile.message_cliloc(user, NO_WOOD)

        return
    end

    if not item.give(user, KINDLING) then
        mobile.message_cliloc(user, NO_ROOM)

        return
    end

    mobile.play_sound(user, HACK_SOUND)
    mobile.message_cliloc(user, KINDLED)
end

-- Called when a player double clicks the blade.
function blade.on_use(serial, user)
    -- A blade lying on the ground cuts nothing: it must be carried.
    if item.owner(serial) ~= user then
        return true
    end

    mobile.message_cliloc(user, USE_ON_WHAT)

    target.pick_location(user, function(picked)
        hack(serial, user, picked)
    end)

    return true
end
