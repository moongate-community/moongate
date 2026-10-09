-- ==============================================================================
-- Moongate - scripts/items/carpentry_tool.lua
--
-- What it is for:
--   The item script of the carpentry tools (saws, dovetail saws, planes,
--   chisels, the draw knife, the froe and the inshave), with
--   script_id = "carpentry_tool": double clicked in the backpack or a bag of
--   it, it opens the crafting gump of carpentry. The rules of making things
--   are scripts/common/crafting.lua, the recipes data/crafts/carpentry.toml.
--
-- Functions:
--   on_use(serial, user)   the player user double clicks the tool serial
-- ==============================================================================

local crafting = require("common.crafting")

carpentry_tool = {}

local CRAFT = "carpentry"

local IN_BACKPACK = 1062334   -- This item must be in your backpack to be used.

-- Called when a player double clicks the tool.
function carpentry_tool.on_use(serial, user)
    if not crafting.carries(user, serial) then
        mobile.message_cliloc(user, IN_BACKPACK)

        return true
    end

    crafting.open(user, serial, CRAFT)

    return true
end
