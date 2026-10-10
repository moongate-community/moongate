-- ==============================================================================
-- Moongate - scripts/items/cooking_tool.lua
--
-- What it is for:
--   The item script of the cooking tools (skillets, flour sifters, rolling
--   pins), with script_id = "cooking_tool": double clicked in the backpack
--   or a bag of it, it opens the crafting gump of cooking. The rules of
--   making things are scripts/common/crafting.lua, the recipes
--   data/crafts/cooking.toml.
--
-- Functions:
--   on_use(serial, user)   the player user double clicks the tool serial
-- ==============================================================================

local crafting = require("common.crafting")

cooking_tool = {}

local CRAFT = "cooking"

local IN_BACKPACK = 1062334   -- This item must be in your backpack to be used.

-- Called when a player double clicks the tool.
function cooking_tool.on_use(serial, user)
    if not crafting.carries(user, serial) then
        mobile.message_cliloc(user, IN_BACKPACK)

        return true
    end

    -- A new tool draws how long it lasts, so its tooltip tells it.
    crafting.uses(serial)
    crafting.open(user, serial, CRAFT)

    return true
end
