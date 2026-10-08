-- ==============================================================================
-- Moongate - scripts/mobiles/shopkeeper.lua
--
-- What it is for:
--   The NPC vendors, as ModernUO's: a player within 8 tiles picks Buy in the
--   vendor's context menu, or says "vendor buy" within 4 tiles, and the shop
--   window opens with what the vendor sells. What each vendor sells is its shop
--   in templates/shops. The client turns "vendor buy" into a speech keyword in
--   any language. When several vendors hear the same words, one answers. The
--   vendor templates that inherit from basevendor use it. The window, the
--   prices and the purchase are the work of the server (the vendor module
--   opens the window), not of this script. The script is called shopkeeper
--   because the vendor module already owns that name.
--
-- Functions:
--   on_speech(serial, speaker, text, keywords)  a player speaks within 15
--                                               cells: the vendor answers
--                                               "vendor buy" said within 4
--   on_context_menu(serial, player)             the entry the vendor adds to
--                                               its context menu: Buy
--   on_context_menu_select(serial, player, id)  the player chose it: the window
--                                               opens
-- ==============================================================================

shopkeeper = {}

-- How far a player may be to pick an entry of the context menu, in tiles.
local menu_range = 8

-- How far a vendor hears "vendor buy", in tiles.
local speech_range = 4

-- The client's texts.
local buy_entry = 6103  -- Buy

local function has_keyword(keywords, wanted)
    for _, keyword in ipairs(keywords or {}) do
        if keyword == wanted then
            return true
        end
    end

    return false
end

local function near(serial, speaker, range)
    local here = npc.location(serial)
    local there = mobile.location(speaker)

    return here and there and here.map == there.map
        and math.abs(here.x - there.x) <= range and math.abs(here.y - there.y) <= range
end

function shopkeeper.on_speech(serial, speaker, text, keywords)
    if not has_keyword(keywords, SpeechKeywordType.VendorBuy) then
        return
    end

    -- Every vendor in range hears the words: the first one serves.
    if not near(serial, speaker, speech_range) or not vendor.attend(speaker) then
        return
    end

    npc.look_at(serial, speaker)
    vendor.open_buy(serial, speaker)
end

function shopkeeper.on_context_menu(serial, player)
    if mobile.is_dead(player) then
        return {}
    end

    return {
        { id = "buy", cliloc = buy_entry, range = menu_range },
    }
end

function shopkeeper.on_context_menu_select(serial, player, id)
    if id ~= "buy" then
        return
    end

    npc.look_at(serial, player)
    vendor.open_buy(serial, player)
end
