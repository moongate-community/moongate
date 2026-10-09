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
--   Picked onto logs in the backpack instead of a tree, the axe saws the whole
--   stack into boards, one for each log, with no skill tried.
--
-- Functions:
--   on_use(serial, user)   the player user double clicks the axe serial
--
-- What it keeps:
--   Who is chopping, in memory by serial, not saved: a restart frees everyone.
-- ==============================================================================

local trees = require("common.trees")

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

local LOGS = "0x1be0_log"
-- The other shape of a log, which shops sell: it is sawn too.
local LOGS_OTHER = "0x1bdd_log"
local BOARDS = "0x1bd7_board"
local LOGS_PER_CUT = 10

-- Client texts.
local USE_ON_WHAT = 1010018   -- What do you want to use this item on?
local NOT_EQUIPPED = 500487   -- The axe must be equipped for any serious wood chopping.
local NOT_A_TREE = 500489     -- You can't use an axe on that.
local TOO_FAR = 500446        -- That is too far away.
local NO_WOOD = 500493        -- There's not enough wood here to harvest.
local FAILED = 500495         -- You hack at the tree for a while, but fail to produce any useable wood.
local NO_ROOM = 500497        -- You can't place any wood into your backpack!
local IN_BACKPACK = 1062334   -- This item must be in your backpack to be used.
local CHOPPED = 500498        -- the text for the logs put into the backpack

-- Who is chopping, by serial.
local chopping = {}

-- A number from 0 up to 1, drawn for each choice.
axe.roll = math.random

local function near(here, map, x, y)
    return here.map == map and math.abs(here.x - x) <= RANGE and math.abs(here.y - y) <= RANGE
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
    if not trees.has_wood(map, x, y) then
        mobile.message_cliloc(user, NO_WOOD)

        return
    end

    if not skill.check(user, "lumberjacking", 0, 100) then
        mobile.message_cliloc(user, FAILED)

        return
    end

    -- The wood leaves the tree whether or not the backpack takes it: a full backpack is no way to chop for ever.
    trees.take_wood(map, x, y)

    if not item.give(user, LOGS, LOGS_PER_CUT) then
        mobile.message_cliloc(user, NO_ROOM)

        return
    end

    mobile.message_cliloc(user, CHOPPED)
end

-- The axe used on an item: logs in the backpack become boards, one for each log; anything else is no tree.
local function saw(user, picked)
    local template = item.template(picked)

    if template ~= LOGS and template ~= LOGS_OTHER then
        mobile.message_cliloc(user, NOT_A_TREE)

        return
    end

    -- Logs on the ground, in a chest or on a cursor stay logs: a pile on a cursor cannot be taken from.
    if item.owner(picked) ~= user or item.is_held(picked) then
        mobile.message_cliloc(user, IN_BACKPACK)

        return
    end

    local amount = item.amount(picked)

    -- The logs are taken before the boards are given: boards never come from logs that stayed.
    if not amount or not item.consume(picked, amount) then
        mobile.message_cliloc(user, IN_BACKPACK)

        return
    end

    mobile.play_sound(user, CHOP_SOUND)
    item.give(user, BOARDS, amount)
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

    if picked.kind == "object" then
        saw(user, picked.serial)

        return
    end

    if not trees.is_tree(picked.graphic or 0) then
        mobile.message_cliloc(user, NOT_A_TREE)

        return
    end

    local map, x, y = picked.map, picked.x, picked.y

    if not near(here, map, x, y) then
        mobile.message_cliloc(user, TOO_FAR)

        return
    end

    if not trees.has_wood(map, x, y) then
        mobile.message_cliloc(user, NO_WOOD)

        return
    end

    chopping[user] = true

    local swings = SWINGS[math.min(math.floor(axe.roll() * #SWINGS) + 1, #SWINGS)]

    -- Whether the player is still at it: alive, with the axe in its hands. One who is not swings no more.
    local function still_chopping()
        return not mobile.is_dead(user) and item.worn_by(tool) == user
    end

    for swing = 1, swings do
        local at = (swing - 1) * BETWEEN_SWINGS

        if swing == 1 then
            mobile.animate(user, CHOP)
        else
            timer.after(at, function()
                if still_chopping() then
                    mobile.animate(user, CHOP)
                end
            end)
        end

        timer.after(at + SOUND_AFTER, function()
            if still_chopping() then
                mobile.play_sound(user, CHOP_SOUND)
            end

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
