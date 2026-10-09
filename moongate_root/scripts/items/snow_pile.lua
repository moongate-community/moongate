-- ==============================================================================
-- Moongate - scripts/items/snow_pile.lua
--
-- What it is for:
--   The snowball of the Christmas event, as ModernUO's SnowPile: double clicking
--   a pile in the backpack asks for a target; the snowball flies to a mobile
--   that carries a pile too ("something that can throw one back"), hits it, and
--   both read it. A player waits 5 seconds between two snowballs, and cannot
--   throw one while mounted. An item template uses it with script_id = "snow_pile".
--
-- Functions:
--   on_use(serial, user)   the player double clicks the pile; returns true
-- ==============================================================================

snow_pile = {}

-- The animation, sound and flight of the snowball (ModernUO's).
local SNOWBALL = 0x36E4
local SNOWBALL_HUE = 0x47F
local SNOWBALL_SPEED = 7
local THROW_SOUND = 0x145
local THROW_ANIMATION = 9

-- How far the target may be, in tiles; how long to wait between two snowballs, in seconds.
local RANGE = 10
local WAIT = 5

-- The templates that count as snow.
local PILES = { "snow_pile", "glacial_snow" }

-- The texts of the client (ModernUO's).
local NOT_CARRIED = 1042010 -- You must have the object in your backpack to use it.
local MOUNTED = 1010097     -- You cannot use this while mounted.
local PACKING = 1005575     -- You carefully pack the snow into a ball...
local NOT_READY = 1005574   -- The snow is not ready to be packed yet.  Keep trying.
local AT_SELF = 1005576     -- You can't throw this at yourself.
local NO_ANSWER = 1005577   -- You can only throw a snowball at something that can throw one back.
local TARGET_HIT = 1010572  -- You have just been hit by a snowball!
local THROWER_HIT = 1010573 -- You throw the snowball and hit the target!
local TOO_FAR = 500446      -- That is too far away.

local function carries_snow(who)
    for _, template in ipairs(PILES) do
        if #item.find(who, template) > 0 then
            return true
        end
    end

    return false
end

local function in_range(user, target)
    local here = mobile.location(user)
    local there = mobile.location(target)

    return here and there and here.map == there.map
        and math.abs(here.x - there.x) <= RANGE and math.abs(here.y - there.y) <= RANGE
end

local function throw(serial, user, picked)
    if picked.kind ~= "object" then
        return
    end

    local target = picked.serial

    if target == user then
        mobile.message_cliloc(user, AT_SELF)

        return
    end

    -- Only a mobile can answer; the item may have been dropped while the cursor was open.
    if mobile.location(target) == nil or not carries_snow(target) then
        mobile.message_cliloc(user, NO_ANSWER)

        return
    end

    if item.owner(serial) ~= user then
        mobile.message_cliloc(user, NOT_CARRIED)

        return
    end

    if not in_range(user, target) then
        mobile.message_cliloc(user, TOO_FAR)

        return
    end

    local now = world.now()

    if now < (mobile.get_prop(user, "snow_pile.next") or 0) then
        mobile.message_cliloc(user, NOT_READY)

        return
    end

    mobile.set_prop(user, "snow_pile.next", now + WAIT)
    mobile.play_sound(user, THROW_SOUND)
    mobile.animate(user, THROW_ANIMATION, 1, 1)
    effect.moving(user, target, SNOWBALL, { speed = SNOWBALL_SPEED, hue = SNOWBALL_HUE })
    mobile.message_cliloc(target, TARGET_HIT)
    mobile.message_cliloc(user, THROWER_HIT)
end

function snow_pile.on_use(serial, user)
    if item.owner(serial) ~= user then
        mobile.message_cliloc(user, NOT_CARRIED)

        return true
    end

    if mobile.is_mounted(user) then
        mobile.message_cliloc(user, MOUNTED)

        return true
    end

    if world.now() < (mobile.get_prop(user, "snow_pile.next") or 0) then
        mobile.message_cliloc(user, NOT_READY)

        return true
    end

    mobile.message_cliloc(user, PACKING)
    target.pick(user, function(picked)
        throw(serial, user, picked)
    end)

    return true
end
