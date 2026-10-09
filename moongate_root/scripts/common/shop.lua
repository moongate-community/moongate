-- ==============================================================================
-- Moongate - scripts/common/shop.lua
--
-- What it is for:
--   What the scripts of the NPCs that keep a shop share, as ModernUO's vendors:
--   the Buy and Sell entries of the context menu and the words "vendor buy" and
--   "vendor sell". An NPC offers Buy only when its shop sells something and Sell
--   only when it buys something (templates/shops); one with no shop offers
--   neither and does not answer the words. Used with
--   local shop = require("common.shop").
--
-- Functions:
--   shop.entries(serial, player, range)     the Buy and Sell entries of the
--                                           context menu; none for a ghost
--   shop.select(serial, player, id)         true when id was Buy or Sell, and
--                                           the window opens
--   shop.listen(serial, speaker, keywords)  the window opens at the words
--                                           "vendor buy" or "vendor sell" said
--                                           within 4 tiles; true when it did
-- ==============================================================================

local shop = {}

-- How far a vendor hears "vendor buy", in tiles.
local speech_range = 4

-- The client's texts.
local buy_entry = 3006103   -- Buy
local sell_entry = 3006104  -- Sell

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

function shop.entries(serial, player, range)
    local entries = {}

    if mobile.is_dead(player) then
        return entries
    end

    -- Only what the vendor does, as in ModernUO: one with no shop offers neither.
    if vendor.sells(serial) then
        entries[#entries + 1] = { id = "buy", cliloc = buy_entry, range = range }
    end

    if vendor.buys(serial) then
        entries[#entries + 1] = { id = "sell", cliloc = sell_entry, range = range }
    end

    return entries
end

function shop.select(serial, player, id)
    if id == "buy" then
        npc.look_at(serial, player)
        vendor.open_buy(serial, player)

        return true
    end

    if id == "sell" then
        npc.look_at(serial, player)
        vendor.open_sell(serial, player)

        return true
    end

    return false
end

function shop.listen(serial, speaker, keywords)
    local buying = has_keyword(keywords, SpeechKeywordType.VendorBuy)
    local selling = has_keyword(keywords, SpeechKeywordType.VendorSell)

    if not buying and not selling then
        return false
    end

    -- Every vendor in range hears the words: the first one that can serve does.
    if not near(serial, speaker, speech_range) then
        return false
    end

    local opened

    if buying then
        opened = vendor.open_buy_once(serial, speaker)
    else
        opened = vendor.open_sell_once(serial, speaker)
    end

    if opened then
        npc.look_at(serial, speaker)
    end

    return opened
end

return shop
