-- ==============================================================================
-- Moongate - scripts/items/fishing_pole.lua
--
-- What it is for:
--   The item script of the fishing poles: a player
--   double clicks the pole, picks water within 4 tiles and in sight, and 8
--   seconds later pulls out a fish, an old piece of footwear or nothing. A
--   template uses it with script_id = "fishing_pole".
--
--   The cast is played at once and the water splashes 1.5 seconds later. The
--   player must still be within 4 tiles of the water when the 8 seconds are
--   over, and fishes one water at a time.
--
--   The fish of a place run out: the resource "fish" of data/harvest.toml
--   (areas of 8 by 8 tiles with 5 to 15 fish, back some minutes after the first
--   catch). Only a catch takes one. A place with none left says so at once.
--
--   What comes out, once the Fishing skill is tried between 0 and 100 (so the
--   chance is the skill, and it may rise):
--     footwear   boots, sandals, shoes or thigh boots, with a chance of
--                (105 - skill) / 525: 20% at 0, about 1% at 100
--     nothing    with a chance of (200 - skill) / 400: 50% at 0, 25% at 100
--     a fish     the rest, one of the four at random
--   The pole does not wear out. There is no riding yet, so no rule for it.
--
-- Functions:
--   on_use(serial, user)   the player user double clicks the pole serial
--
-- What it keeps:
--   Who is fishing, in memory by serial, not saved: a restart frees everyone.
-- ==============================================================================

fishing_pole = {}

-- How far the water may be, in tiles.
local RANGE = 4

-- A whole attempt, and when the water splashes, in seconds.
local SECONDS = 8
local SPLASH_AFTER = 1.5

-- The cast of a human body, the splash on the water and its sound.
local CAST = 12
local SPLASH = 0x352D
local SPLASH_SOUND = 0x364

-- Where a mobile sees from, above its feet.
local EYE = 14

local RESOURCE = "fish"

-- Client texts.
local WHAT_WATER = 500974     -- What water do you want to fish in?
local ALREADY = 500972        -- You are already fishing.
local CLOSER = 500976         -- You need to be closer to the water to fish!
local NOT_BITING = 503172     -- The fish don't seem to be biting here.
local NOTHING = 503171        -- You fish a while, but fail to catch anything.
local NO_ROOM = 503176        -- You do not have room in your backpack for a fish.
local PULLED = 1008124        -- You pull out an item : (and its name)
local NO_WATER = "You need water to fish in!"

local FISH = {
    { template = "0x09cc_fish", name = "fish" },
    { template = "0x09cd_fish", name = "fish" },
    { template = "0x09ce_fish", name = "fish" },
    { template = "0x09cf_fish", name = "fish" },
}

local FOOTWEAR = {
    { template = "0x170b_boots", name = "boots" },
    { template = "0x170d_sandals", name = "sandals" },
    { template = "0x170f_shoes", name = "shoes" },
    { template = "0x1711_thigh_boots", name = "thigh boots" },
}

-- Who is fishing, by serial.
local fishing = {}

-- A number from 0 up to 1, drawn for each choice.
fishing_pole.roll = math.random

local function near(here, map, x, y)
    return here.map == map and math.abs(here.x - x) <= RANGE and math.abs(here.y - y) <= RANGE
end

-- One of a list, by a roll.
local function one_of(list)
    local index = math.floor(fishing_pole.roll() * #list) + 1

    return list[math.min(index, #list)]
end

-- What a passed try pulls out: a list to draw from, or nil for nothing.
local function catch(points)
    if fishing_pole.roll() < (105 - points) / 525 then
        return FOOTWEAR
    end

    if fishing_pole.roll() < (200 - points) / 400 then
        return nil
    end

    return FISH
end

-- The 8 seconds are over.
local function finish(user, map, x, y)
    fishing[user] = nil

    local here = mobile.location(user)

    if not here or mobile.is_dead(user) then
        return
    end

    if not near(here, map, x, y) then
        mobile.message_cliloc(user, CLOSER)

        return
    end

    -- Someone else may have taken the last fish meanwhile.
    if (harvest.amount(RESOURCE, map, x, y) or 0) <= 0 then
        mobile.message_cliloc(user, NOT_BITING)

        return
    end

    if not skill.check(user, "fishing", 0, 100) then
        mobile.message_cliloc(user, NOTHING)

        return
    end

    local list = catch(mobile.skills(user).fishing or 0)

    if not list then
        mobile.message_cliloc(user, NOTHING)

        return
    end

    local caught = one_of(list)

    if not item.give(user, caught.template) then
        mobile.message_cliloc(user, NO_ROOM)

        return
    end

    harvest.take(RESOURCE, map, x, y)
    mobile.message_cliloc(user, PULLED, caught.name)
end

-- The player picked where to fish.
local function cast(user, picked)
    if picked.kind == "canceled" or fishing[user] then
        return
    end

    local here = mobile.location(user)

    if not here or mobile.is_dead(user) then
        return
    end

    if picked.kind ~= "location" or not world.is_water(picked.map, picked.x, picked.y) then
        mobile.message(user, NO_WATER)

        return
    end

    local map, x, y, z = picked.map, picked.x, picked.y, picked.z

    if not near(here, map, x, y) or not world.line_of_sight(map, here.x, here.y, here.z + EYE, x, y, z) then
        mobile.message_cliloc(user, CLOSER)

        return
    end

    if (harvest.amount(RESOURCE, map, x, y) or 0) <= 0 then
        mobile.message_cliloc(user, NOT_BITING)

        return
    end

    fishing[user] = true
    mobile.animate(user, CAST)

    timer.after(SPLASH_AFTER, function()
        effect.at(map, x, y, z, SPLASH)
        world.play_sound(map, x, y, z, SPLASH_SOUND)
    end)

    timer.after(SECONDS, function()
        finish(user, map, x, y)
    end)
end

-- Called when a player double clicks the pole.
function fishing_pole.on_use(serial, user)
    if fishing[user] then
        mobile.message_cliloc(user, ALREADY)

        return true
    end

    mobile.message_cliloc(user, WHAT_WATER)

    target.pick_location(user, function(picked)
        cast(user, picked)
    end)

    return true
end
