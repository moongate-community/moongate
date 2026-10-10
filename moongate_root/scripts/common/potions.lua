-- ==============================================================================
-- Moongate - scripts/common/potions.lua
--
-- What it is for:
--   Which potion an item is: the named potions (healpotion, ...) and the plain
--   ones vendors sell, loot drops and alchemists make (0x0f0c_b_yellow_potion
--   is a heal potion). Used by scripts/items/potion.lua and
--   scripts/items/explosion_potion.lua with
--   local potions = require("common.potions").
--
-- Functions:
--   potions.kind(serial)   the named potion an item is, or nil
-- ==============================================================================

local potions = {}

-- The plain potions, by the named one they are.
local PLAIN = {
    ["0x0f06_black_potion"] = "nightsightpotion",
    ["0x0f07_orange_potion"] = "lessercurepotion",
    ["0x0f07_b_orange_potion"] = "curepotion",
    ["0x0f07_c_orange_potion"] = "greatercurepotion",
    ["0x0f08_blue_potion"] = "agilitypotion",
    ["0x0f08_b_blue_potion"] = "greateragilitypotion",
    ["0x0f09_white_potion"] = "strengthpotion",
    ["0x0f09_b_white_potion"] = "greaterstrengthpotion",
    ["0x0f0a_green_potion"] = "lesserpoisonpotion",
    ["0x0f0a_b_green_potion"] = "poisonpotion",
    ["0x0f0a_c_green_potion"] = "greaterpoisonpotion",
    ["0x0f0a_d_green_potion"] = "deadlypoisonpotion",
    ["0x0f0b_red_potion"] = "refreshmentpotion",
    ["0x0f0b_b_red_potion"] = "totalrefreshmentpotion",
    ["0x0f0c_yellow_potion"] = "lesserhealpotion",
    ["0x0f0c_b_yellow_potion"] = "healpotion",
    ["0x0f0c_c_yellow_potion"] = "greaterhealpotion",
    ["0x0f0d_purple_potion"] = "lesserexplosionpotion",
    ["0x0f0d_b_purple_potion"] = "explosionpotion",
    ["0x0f0d_c_purple_potion"] = "greaterexplosionpotion",
}

function potions.kind(serial)
    local template = item.template(serial)

    return template and (PLAIN[template] or template)
end

return potions
