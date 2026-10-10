-- ==============================================================================
-- Moongate - scripts/items/smithing_tool.lua
--
-- What it is for:
--   The item script of the smithing tools (smith's hammers, sledge hammers
--   and tongs), with script_id = "smithing_tool": double clicked in the
--   backpack, a bag of it or the hands, it opens the crafting gump of
--   blacksmithing. Forging asks for an anvil and a forge within 2 tiles
--   (scripts/common/smithy.lua); the rules are scripts/common/crafting.lua,
--   the recipes data/crafts/blacksmithing.toml.
--
-- Functions:
--   on_use(serial, user)   the player user double clicks the tool serial
-- ==============================================================================

local crafting = require("common.crafting")

smithing_tool = {}

local CRAFT = "blacksmithing"

local IN_BACKPACK = 1062334   -- This item must be in your backpack to be used.

-- Called when a player double clicks the tool.
function smithing_tool.on_use(serial, user)
    if not crafting.carries(user, serial) then
        mobile.message_cliloc(user, IN_BACKPACK)

        return true
    end

    -- A new tool draws how long it lasts, so its tooltip tells it.
    crafting.uses(serial)
    crafting.open(user, serial, CRAFT)

    return true
end
