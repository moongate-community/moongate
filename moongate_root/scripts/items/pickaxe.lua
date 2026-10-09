-- ==============================================================================
-- Moongate - scripts/items/pickaxe.lua
--
-- What it is for:
--   The item script of the pickaxes and the shovels: a player double clicks
--   the tool, carried or in its hands, picks the rock of a mountain or the floor
--   of a cave within 2 tiles, swings once and digs out a pile of iron ore. A
--   template uses it with script_id = "pickaxe".
--
--   The swing is played at once and the result comes 0.9 seconds later, with the
--   sound of the pick. The player must still be within 2 tiles of the place,
--   and digs one place at a time.
--
--   The ore of a place runs out: the resource "ore" of data/harvest.toml (areas
--   of 8 by 8 tiles with 10 to 34 ore, back some minutes after the first). Only
--   a dig that works takes one, also when the backpack has no room for it. A
--   place with none left says so at once.
--
--   The Mining skill is tried between 0 and 100, so the chance is the skill, and
--   it may rise. What comes out is one pile of iron ore: large three times in
--   four, else medium or small. A forge turns it into ingots
--   (scripts/items/ore.lua). The tool does not wear out.
--
-- Functions:
--   on_use(serial, user)   the player user double clicks the tool serial
--
-- What it keeps:
--   Who is digging, in memory by serial, not saved: a restart frees everyone.
-- ==============================================================================

pickaxe = {}

-- How far the rock may be, in tiles.
local RANGE = 2

-- How long after the swing the pick is heard and the result comes, in seconds.
local SECONDS = 0.9

-- The swing of a human body, and the two sounds of a pick.
local DIG = 11
local DIG_SOUNDS = { 0x125, 0x126 }

local RESOURCE = "ore"

-- Client texts.
local WHERE_TO_DIG = 503033   -- Where do you wish to dig?
local NOT_THERE = 501862      -- You can't mine there.
local NOT_THAT = 501863       -- You can't mine that.
local TOO_FAR = 500446        -- That is too far away.
local MOVED_AWAY = 503041     -- You have moved too far away to continue mining.
local NO_METAL = 503040       -- There is no metal here to mine.
local GONE = 503042           -- Someone has gotten to the metal before you.
local FAILED = 503043         -- You loosen some rocks but fail to find any useable ore.
local NO_ROOM = 1010481       -- Your backpack is full, so the ore you mined is lost.
local DUG = 1007072           -- the text for the iron ore put into the backpack

-- The piles of iron ore a dig gives, each up to its share of the rolls: small, the two medium ones, large.
local PILES = {
    { upto = 0.125, template = "0x19b7_iron_ore" },
    { upto = 0.1875, template = "0x19b8_iron_ore" },
    { upto = 0.25, template = "0x19ba_iron_ore" },
    { upto = 1, template = "0x19b9_iron_ore" },
}

-- The land tiles that are the rock of a mountain or of a cave, as ranges.
local ROCK = {
    { 220, 231 }, { 236, 247 }, { 252, 263 }, { 268, 279 }, { 286, 297 }, { 321, 324 }, { 467, 474 },
    { 476, 487 }, { 492, 495 }, { 543, 579 }, { 581, 621 }, { 1741, 1757 }, { 1771, 1790 }, { 1801, 1824 },
    { 1831, 1854 }, { 1861, 1884 }, { 1981, 2004 }, { 2028, 2033 }, { 2100, 2105 },
    { 0x3F39, 0x3F74 }, { 0x3F82, 0x3F8F }, { 0x3F91, 0x3FCF },
}

-- The statics that are the floor of a cave, as ranges.
local CAVE_FLOOR = {
    { 0x053B, 0x054F },
}

-- Who is digging, by serial.
local digging = {}

-- A number from 0 up to 1, drawn for each choice.
pickaxe.roll = math.random

local function within(list, value)
    for _, range in ipairs(list) do
        if value >= range[1] and value <= range[2] then
            return true
        end
    end

    return false
end

local function near(here, map, x, y)
    return here.map == map and math.abs(here.x - x) <= RANGE and math.abs(here.y - y) <= RANGE
end

local function has_ore(map, x, y)
    return (harvest.amount(RESOURCE, map, x, y) or 0) > 0
end

-- Whether the player still has the tool: carried or in its hands, or lying within reach.
local function has_tool(tool, user)
    return item.owner(tool) == user or item.in_range(tool, user, 2)
end

-- The swing landed.
local function finish(tool, user, map, x, y)
    digging[user] = nil

    local here = mobile.location(user)

    if not here or mobile.is_dead(user) or not has_tool(tool, user) then
        return
    end

    mobile.play_sound(user, DIG_SOUNDS[math.min(math.floor(pickaxe.roll() * #DIG_SOUNDS) + 1, #DIG_SOUNDS)])

    if not near(here, map, x, y) then
        mobile.message_cliloc(user, MOVED_AWAY)

        return
    end

    -- Someone else may have taken the last ore meanwhile.
    if not has_ore(map, x, y) then
        mobile.message_cliloc(user, GONE)

        return
    end

    if not skill.check(user, "mining", 0, 100) then
        mobile.message_cliloc(user, FAILED)

        return
    end

    local roll = pickaxe.roll()
    local pile = PILES[#PILES]

    for _, each in ipairs(PILES) do
        if roll < each.upto then
            pile = each

            break
        end
    end

    -- The ore leaves the rock whether or not the backpack takes it: a full backpack is no way to dig for ever.
    harvest.take(RESOURCE, map, x, y)

    if not item.give(user, pile.template) then
        mobile.message_cliloc(user, NO_ROOM)

        return
    end

    mobile.message_cliloc(user, DUG)
end

-- The player picked where to dig.
local function dig(tool, user, picked)
    if picked.kind == "canceled" or digging[user] then
        return
    end

    local here = mobile.location(user)

    if not here or mobile.is_dead(user) or not has_tool(tool, user) then
        return
    end

    if picked.kind ~= "location" then
        mobile.message_cliloc(user, NOT_THAT)

        return
    end

    -- A static picked must be the floor of a cave; with none, the land must be rock.
    local graphic = picked.graphic or 0
    local minable

    if graphic ~= 0 then
        minable = within(CAVE_FLOOR, graphic)
    else
        minable = within(ROCK, picked.land or 0)
    end

    if not minable then
        mobile.message_cliloc(user, NOT_THERE)

        return
    end

    local map, x, y = picked.map, picked.x, picked.y

    if not near(here, map, x, y) then
        mobile.message_cliloc(user, TOO_FAR)

        return
    end

    if not has_ore(map, x, y) then
        mobile.message_cliloc(user, NO_METAL)

        return
    end

    digging[user] = true
    mobile.animate(user, DIG)

    timer.after(SECONDS, function()
        finish(tool, user, map, x, y)
    end)
end

-- Called when a player double clicks the tool.
function pickaxe.on_use(serial, user)
    if digging[user] then
        return true
    end

    mobile.message_cliloc(user, WHERE_TO_DIG)

    target.pick_location(user, function(picked)
        dig(serial, user, picked)
    end)

    return true
end
