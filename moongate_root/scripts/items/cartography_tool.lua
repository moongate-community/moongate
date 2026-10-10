-- ==============================================================================
-- Moongate - scripts/items/cartography_tool.lua
--
-- What it is for:
--   The item script of the pens a cartographer draws with, with
--   script_id = "cartography_tool": double clicked in the backpack or a bag
--   of it, it opens the crafting gump of cartography. The rules of making
--   things are scripts/common/crafting.lua, the recipes
--   data/crafts/cartography.toml, the drawing scripts/common/cartography.lua.
--
-- Functions:
--   on_use(serial, user)   the player user double clicks the tool serial
-- ==============================================================================

local crafting = require("common.crafting")

cartography_tool = {}

local CRAFT = "cartography"

local IN_BACKPACK = 1062334   -- This item must be in your backpack to be used.

-- Called when a player double clicks the tool.
function cartography_tool.on_use(serial, user)
    if not crafting.carries(user, serial) then
        mobile.message_cliloc(user, IN_BACKPACK)

        return true
    end

    -- A new tool draws how long it lasts, so its tooltip tells it.
    crafting.uses(serial)
    crafting.open(user, serial, CRAFT)

    return true
end
