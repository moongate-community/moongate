-- ==============================================================================
-- Moongate - scripts/mobiles/shopkeeper.lua
--
-- What it is for:
--   The NPC vendors, as ModernUO's: a player within 8 tiles picks Buy or Sell in the
--   vendor's context menu, or says "vendor buy" or "vendor sell" within 4 tiles, and
--   the shop window opens with what the vendor sells, or the list of what the player
--   carries that the vendor buys. What each vendor sells is its shop
--   in templates/shops. The client turns "vendor buy" into a speech keyword in
--   any language. When several vendors hear the same words, one answers. The
--   vendor templates that inherit from basevendor use it. The window, the
--   prices and the purchase are the work of the server (the vendor module
--   opens the window), not of this script. The script is called shopkeeper
--   because the vendor module already owns that name.
--
-- Vendors also teach skills, as ModernUO's do (common/training.lua): Train entries in
--   the menu, the word "train", and gold dropped on the vendor to pay.
--
-- Functions:
--   on_speech(serial, speaker, text, keywords)  a player speaks within 15
--                                               cells: the vendor answers
--                                               "vendor buy" and "vendor sell"
--                                               said within 4
--   on_context_menu(serial, player)             the entries the vendor adds to
--                                               its context menu: Buy when it sells, Sell when it buys
--   on_context_menu_select(serial, player, id)  the player chose it: the window
--                                               opens, or the price of a lesson
--                                               is quoted
--   on_drag_drop(serial, giver, item)           gold dropped on the vendor pays
--                                               for the lesson it quoted
-- ==============================================================================

local shop = require("common.shop")
local training = require("common.training")
local guild = require("common.guild")

shopkeeper = {}

-- How far a player may be to pick an entry of the context menu, in tiles.
local menu_range = 8

function shopkeeper.on_speech(serial, speaker, text, keywords)
    training.listen(serial, speaker, keywords)
    guild.listen(serial, speaker, text, keywords)

    shop.listen(serial, speaker, keywords)
end

function shopkeeper.on_context_menu(serial, player)
    if mobile.is_dead(player) then
        return {}
    end

    local entries = shop.entries(serial, player, menu_range)

    for _, entry in ipairs(training.entries(serial, player, menu_range)) do
        entries[#entries + 1] = entry
    end

    return entries
end

function shopkeeper.on_context_menu_select(serial, player, id)
    if not shop.select(serial, player, id) then
        training.select(serial, player, id)
    end
end

-- Gold dropped on a vendor pays for a lesson it quoted, or on a guildmaster joins its guild (common/guild.lua);
-- anything else goes back.
function shopkeeper.on_drag_drop(serial, giver, item)
    local handled, joined = guild.drop(serial, giver, item)

    if handled then
        return joined
    end

    return training.drop(serial, giver, item)
end
