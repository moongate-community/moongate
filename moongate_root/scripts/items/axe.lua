-- ==============================================================================
-- Moongate - scripts/items/axe.lua
--
-- What it is for:
--   The item script of the axes: a player double clicks an axe held in its
--   hands, picks a tree within 2 tiles, swings at it one to three times and
--   gets 10 logs. A template uses it with script_id = "axe".
--
--   A swing is played every 1.6 seconds, and the axe is heard 0.9 seconds after
--   each; the result comes with the last. Most cuts take two swings. The player
--   must still be within 2 tiles of the tree, with the axe in its hands, when
--   the last swing lands, and chops one tree at a time.
--
--   The wood of a place runs out: the resource "wood" of data/harvest.toml
--   (areas of 4 by 4 tiles with 2 to 4 cuts, back some minutes after the
--   first). Only a cut that works takes one, also when the backpack has no room
--   for the logs. A place with none left says so at once.
--
--   The Lumberjacking skill is tried between 0 and 100, so the chance is the
--   skill, and it may rise. The axe does not wear out.
--
-- Functions:
--   on_use(serial, user)   the player user double clicks the axe serial
--
-- What it keeps:
--   Who is chopping, in memory by serial, not saved: a restart frees everyone.
-- ==============================================================================

axe = {}

-- How far the tree may be, in tiles.
local RANGE = 2

-- A swing every so many seconds, and how long after it the axe is heard.
local BETWEEN_SWINGS = 1.6
local SOUND_AFTER = 0.9

-- How many swings a cut takes: one of these, so two more often than not.
local SWINGS = { 1, 2, 2, 2, 3 }

-- The chop of a human body, and its sound.
local CHOP = 13
local CHOP_SOUND = 0x13E

local RESOURCE = "wood"
local LOGS = "0x1be0_log"
local LOGS_PER_CUT = 10

-- Client texts.
local USE_ON_WHAT = 1010018   -- What do you want to use this item on?
local NOT_EQUIPPED = 500487   -- The axe must be equipped for any serious wood chopping.
local NOT_A_TREE = 500489     -- You can't use an axe on that.
local TOO_FAR = 500446        -- That is too far away.
local NO_WOOD = 500493        -- There's not enough wood here to harvest.
local FAILED = 500495         -- You hack at the tree for a while, but fail to produce any useable wood.
local NO_ROOM = 500497        -- You can't place any wood into your backpack!
local CHOPPED = 500498        -- the text for the logs put into the backpack

-- The graphics of the trees a player chops, alone or as a range.
local TREES = {
    { 0x0CCA, 0x0CE8 }, { 0x0CF8, 0x0D03 }, { 0x0D41, 0x0D53 }, { 0x0D57, 0x0D69 }, { 0x0D6E, 0x0D7F },
    { 0x0D84, 0x0D90 }, { 0x0D95, 0x0D97 }, { 0x0D99, 0x0D9B }, { 0x0D9D, 0x0D9F }, { 0x0DA1, 0x0DA3 },
    { 0x0DA5, 0x0DA7 }, { 0x0DA9, 0x0DAB }, { 0x12B5, 0x12C7 },
}

-- Who is chopping, by serial.
local chopping = {}

-- A number from 0 up to 1, drawn for each choice.
axe.roll = math.random

local function is_tree(graphic)
    for _, range in ipairs(TREES) do
        if graphic >= range[1] and graphic <= range[2] then
            return true
        end
    end

    return false
end

local function near(here, map, x, y)
    return here.map == map and math.abs(here.x - x) <= RANGE and math.abs(here.y - y) <= RANGE
end

local function has_wood(map, x, y)
    return (harvest.amount(RESOURCE, map, x, y) or 0) > 0
end

-- The last swing landed.
local function finish(tool, user, map, x, y)
    chopping[user] = nil

    local here = mobile.location(user)

    if not here or mobile.is_dead(user) or item.worn_by(tool) ~= user then
        return
    end

    if not near(here, map, x, y) then
        mobile.message_cliloc(user, TOO_FAR)

        return
    end

    -- Someone else may have taken the last cut meanwhile.
    if not has_wood(map, x, y) then
        mobile.message_cliloc(user, NO_WOOD)

        return
    end

    if not skill.check(user, "lumberjacking", 0, 100) then
        mobile.message_cliloc(user, FAILED)

        return
    end

    -- The wood leaves the tree whether or not the backpack takes it: a full backpack is no way to chop for ever.
    harvest.take(RESOURCE, map, x, y)

    if not item.give(user, LOGS, LOGS_PER_CUT) then
        mobile.message_cliloc(user, NO_ROOM)

        return
    end

    mobile.message_cliloc(user, CHOPPED)
end

-- The player picked what to use the axe on.
local function chop(tool, user, picked)
    if picked.kind == "canceled" or chopping[user] then
        return
    end

    local here = mobile.location(user)

    if not here or mobile.is_dead(user) or item.worn_by(tool) ~= user then
        return
    end

    if picked.kind ~= "location" or not is_tree(picked.graphic or 0) then
        mobile.message_cliloc(user, NOT_A_TREE)

        return
    end

    local map, x, y = picked.map, picked.x, picked.y

    if not near(here, map, x, y) then
        mobile.message_cliloc(user, TOO_FAR)

        return
    end

    if not has_wood(map, x, y) then
        mobile.message_cliloc(user, NO_WOOD)

        return
    end

    chopping[user] = true

    local swings = SWINGS[math.min(math.floor(axe.roll() * #SWINGS) + 1, #SWINGS)]

    for swing = 1, swings do
        local at = (swing - 1) * BETWEEN_SWINGS

        if swing == 1 then
            mobile.animate(user, CHOP)
        else
            timer.after(at, function()
                mobile.animate(user, CHOP)
            end)
        end

        timer.after(at + SOUND_AFTER, function()
            mobile.play_sound(user, CHOP_SOUND)

            if swing == swings then
                finish(tool, user, map, x, y)
            end
        end)
    end
end

-- Called when a player double clicks the axe.
function axe.on_use(serial, user)
    if chopping[user] then
        return true
    end

    if item.worn_by(serial) ~= user then
        mobile.message_cliloc(user, NOT_EQUIPPED)

        return true
    end

    mobile.message_cliloc(user, USE_ON_WHAT)

    target.pick_location(user, function(picked)
        chop(serial, user, picked)
    end)

    return true
end
