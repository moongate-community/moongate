-- ==============================================================================
-- Moongate - scripts/items/fletching_tool.lua
--
-- What it is for:
--   The item script of the fletcher's tools, with
--   script_id = "fletching_tool": double clicked in the backpack or a bag of
--   it, it opens the crafting gump of bowcraft and fletching. The rules of
--   making things are scripts/common/crafting.lua, the recipes
--   data/crafts/fletching.toml.
--
-- Functions:
--   on_use(serial, user)   the player user double clicks the tool serial
-- ==============================================================================

local crafting = require("common.crafting")

fletching_tool = {}

local CRAFT = "fletching"

local IN_BACKPACK = 1062334   -- This item must be in your backpack to be used.

-- Called when a player double clicks the tool.
function fletching_tool.on_use(serial, user)
    if not crafting.carries(user, serial) then
        mobile.message_cliloc(user, IN_BACKPACK)

        return true
    end

    -- A new tool draws how long it lasts, so its tooltip tells it.
    crafting.uses(serial)
    crafting.open(user, serial, CRAFT)

    return true
end
