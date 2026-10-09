-- ==============================================================================
-- Moongate - scripts/events/christmas.lua
--
-- What it is for:
--   The hooks of the event christmas (data/schedule.toml), as ModernUO's Winter
--   2004 gift giver: the season is announced when it starts and ends, and each
--   character that logs in during it gets a gift once, two piles of snow for
--   scripts/items/snow_pile.lua, a holiday candle and one
--   decoration, in its backpack.
--
-- Functions:
--   on_start(id, name)            the event began: the towns are decorated, everybody is told
--   on_end(id, name)              the event is over: the decorations go, everybody is told
--   on_login(id, name, player)    a character entered the world while the event
--                                 is on: it gets its gift if it did not get one
--                                 in the last 200 days
--
-- Props it keeps on the character:
--   christmas.gift   the world.now() second of the gift
-- ==============================================================================

local holiday_decor = require("common.holiday_decor")

christmas = {}

-- The decorations of the towns, templates of templates/items/misc/holiday_decorations.toml.
local TOWN_DECORATIONS = { "xm_snowy_tree", "xm_topiary", "xm_cactus", "xm_poinsettia" }

-- Messages of data/messages/<language>/moongate.toml.
local STARTED = 30238
local OVER = 30239
local GIFT_GIVEN = 30240

-- A character that got a gift less than this long ago gets none: the window is under two weeks, so this is once a season.
local SEASON_SECONDS = 200 * 86400

-- The templates of the gift: ModernUO picks one decoration, 60%, 24% or 16%.
local PILES = { "snow_pile", "glacial_snow" }
local LIGHT = "0x236e"
local DECORATIONS = {
    { upto = 60, template = "0x2378_a_decorative_topiary" },
    { upto = 84, template = "0x2376_a_festive_cactus" },
    { upto = 100, template = "0x2377_a_snowy_tree" }
}

local function decoration()
    local roll = math.random(100)

    for _, entry in ipairs(DECORATIONS) do
        if roll <= entry.upto then
            return entry.template
        end
    end

    return DECORATIONS[#DECORATIONS].template
end

function christmas.on_start(id, name)
    holiday_decor.place(id, TOWN_DECORATIONS)
    world.broadcast(localization.get(STARTED))
end

function christmas.on_end(id, name)
    holiday_decor.remove(id)
    world.broadcast(localization.get(OVER))
end

function christmas.on_login(id, name, player)
    local now = world.now()
    local last = mobile.get_prop(player, "christmas.gift")

    if last and now - last < SEASON_SECONDS then
        return
    end

    -- The first pile tells whether the backpack can take a gift at all; nothing is marked otherwise.
    if not item.give(player, PILES[1]) then
        return
    end

    item.give(player, PILES[2])
    item.give(player, LIGHT)
    item.give(player, decoration())
    mobile.set_prop(player, "christmas.gift", now)
    mobile.message(player, localization.get(GIFT_GIVEN))
end
