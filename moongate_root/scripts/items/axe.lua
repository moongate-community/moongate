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
--   A place is of one kind of wood, a vein of the resource, drawn again each
--   time its wood is back: plain, oak, ash, yew, heartwood, bloodwood or
--   frostwood. A kind asks for a Lumberjacking skill (the table WOODS); one
--   who has it gets the logs of the kind one cut in two, tried between the
--   bounds of the kind, and plain logs the other. One who lacks it gets plain
--   logs.
--
--   At 100 of skill a cut that works may also find something rare (the table
--   FINDS): about one cut in six.
--
--   Picked onto logs in the backpack instead of a tree, the axe saws the whole
--   stack into boards of the same kind, one for each log, with no skill tried;
--   the logs of a kind ask for the skill of the kind.
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

local LOGS_PER_CUT = 10

-- Plain wood: its logs and boards, the bounds its cut is tried between and the text of its logs.
local PLAIN = { logs = "0x1be0_log", boards = "0x1bd7_board", skill = 0, min = 0, max = 100, chopped = 500498 }
-- The other shape of a plain log, which shops sell: it is sawn too.
local LOGS_OTHER = "0x1bdd_log"

-- The other kinds, by the vein of the place: skill is the Lumberjacking the kind asks for, to chop it and to saw it.
local WOODS = {
    oak = { logs = "oak_log", boards = "oak_board", skill = 65, min = 25, max = 105, chopped = 1072541 },
    ash = { logs = "ash_log", boards = "ash_board", skill = 80, min = 40, max = 120, chopped = 1072542 },
    yew = { logs = "yew_log", boards = "yew_board", skill = 95, min = 55, max = 135, chopped = 1072543 },
    heartwood = { logs = "heartwood_log", boards = "heartwood_board", skill = 100, min = 60, max = 140, chopped = 1072544 },
    bloodwood = { logs = "bloodwood_log", boards = "bloodwood_board", skill = 100, min = 60, max = 140, chopped = 1072545 },
    frostwood = { logs = "frostwood_log", boards = "frostwood_board", skill = 100, min = 60, max = 140, chopped = 1072546 },
}

-- How often a place of a kind gives plain logs all the same.
local PLAIN_INSTEAD = 0.5

-- What a master may find with the logs: each as often as its chance in a hundred cuts, with the text told.
local FINDS_SKILL = 100
local FINDS = {
    { template = "bark_fragment", chance = 10, found = 1072548 },
    { template = "luminescent_fungi", chance = 3, found = 1072550 },
    { template = "switch", chance = 2, found = 1072547 },
    { template = "parasitic_plant", chance = 1, found = 1072549 },
    { template = "brilliant_amber", chance = 0.1, found = 1072551 },
}

-- The kind each template of logs is of.
local KIND_OF = { [PLAIN.logs] = PLAIN, [LOGS_OTHER] = PLAIN }

for _, wood in pairs(WOODS) do
    KIND_OF[wood.logs] = wood
end

-- Client texts.
local USE_ON_WHAT = 1010018   -- What do you want to use this item on?
local NOT_EQUIPPED = 500487   -- The axe must be equipped for any serious wood chopping.
local NOT_A_TREE = 500489     -- You can't use an axe on that.
local TOO_FAR = 500446        -- That is too far away.
local NO_WOOD = 500493        -- There's not enough wood here to harvest.
local FAILED = 500495         -- You hack at the tree for a while, but fail to produce any useable wood.
local NO_ROOM = 500497        -- You can't place any wood into your backpack!
local IN_BACKPACK = 1062334   -- This item must be in your backpack to be used.
local STRANGE_WOOD = 1072652  -- You cannot work this strange and unusual wood.

-- Who is chopping, by serial.
local chopping = {}

-- A number from 0 up to 1, drawn for each choice.
axe.roll = math.random

local function near(here, map, x, y)
    return here.map == map and math.abs(here.x - x) <= RANGE and math.abs(here.y - y) <= RANGE
end

local function lumberjacking(user)
    return mobile.skills(user).lumberjacking or 0
end

-- The wood a cut of the place gives the player: its kind for one who has the skill of it, one cut in two; else plain.
local function wood_for(user, map, x, y)
    local wood = WOODS[trees.wood(map, x, y)]

    if not wood or lumberjacking(user) < wood.skill or axe.roll() < PLAIN_INSTEAD then
        return PLAIN
    end

    return wood
end

-- What a master finds with the logs, if anything.
local function find_for(user)
    if lumberjacking(user) < FINDS_SKILL then
        return nil
    end

    local roll = axe.roll() * 100

    for _, find in ipairs(FINDS) do
        if roll < find.chance then
            return find
        end

        roll = roll - find.chance
    end

    return nil
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

    local wood = wood_for(user, map, x, y)

    if not skill.check(user, "lumberjacking", wood.min, wood.max) then
        mobile.message_cliloc(user, FAILED)

        return
    end

    -- The wood leaves the tree whether or not the backpack takes it: a full backpack is no way to chop for ever.
    trees.take_wood(map, x, y)

    -- A root without the logs of the kind (templates/items/woods.toml) gets plain ones, as a full backpack tries to.
    if not item.give(user, wood.logs, LOGS_PER_CUT) then
        wood = PLAIN

        if not item.give(user, wood.logs, LOGS_PER_CUT) then
            mobile.message_cliloc(user, NO_ROOM)

            return
        end
    end

    mobile.message_cliloc(user, wood.chopped)

    local find = find_for(user)

    if find and item.give(user, find.template, 1) then
        mobile.message_cliloc(user, find.found)
    end
end

-- Whether an item of that template lies in the backpack of the player or in a bag of it: not in the bank box.
local function in_backpack(user, serial, template)
    for _, each in ipairs(item.find(user, template)) do
        if each == serial then
            return true
        end
    end

    return false
end

-- The axe used on an item: logs in the backpack become boards of their kind, one for each log; anything else is no
-- tree.
local function saw(user, picked)
    local template = item.template(picked)
    local wood = KIND_OF[template]

    if not wood then
        mobile.message_cliloc(user, NOT_A_TREE)

        return
    end

    -- Logs on the ground, in a chest, in the bank or on a cursor stay logs: a pile on a cursor cannot be taken from.
    if not in_backpack(user, picked, template) or item.is_held(picked) then
        mobile.message_cliloc(user, IN_BACKPACK)

        return
    end

    if lumberjacking(user) < wood.skill then
        mobile.message_cliloc(user, STRANGE_WOOD)

        return
    end

    local amount = item.amount(picked)

    -- The logs are taken before the boards are given: boards never come from logs that stayed.
    if not amount or not item.consume(picked, amount) then
        mobile.message_cliloc(user, IN_BACKPACK)

        return
    end

    mobile.play_sound(user, CHOP_SOUND)

    if not item.give(user, wood.boards, amount) then
        mobile.message_cliloc(user, NO_ROOM)
    end
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
