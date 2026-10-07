-- ==============================================================================
-- Moongate - scripts/items/lockpick.lua
--
-- What it is for:
--   The item script of the lockpicks, as ModernUO's Lockpick: a player double
--   clicks the lockpick, picks a locked item within a tile, waits three seconds
--   and tries the skill. A template uses it with script_id = "lockpick".
--
--   What can be picked is an item whose props say how: locked, true while it is
--   locked, and lock.level, lock.max and lock.required, the points of Lockpicking
--   where the try may just succeed, where it never fails, and the least to try at
--   all. A lock with no lock.level cannot be picked by normal means. A player
--   short of lock.required "does not see how that lock can be manipulated". The
--   skill is tried from lock.level to lock.max points; on a success the item is
--   unlocked for good (locked is false, lock.picker is the player); on a failure
--   the lockpick breaks one time in four, and is taken out of its stack.
--   The player has to stay within a tile of the lock for the three seconds.
--
-- Functions:
--   on_use(serial, user)   the player user double clicks the lockpick serial
-- ==============================================================================

lockpick = {}

-- The client's own texts.
local WHAT = 502068          -- What do you want to pick?
local NOT_LOCKED = 502069    -- This does not appear to be locked.
local CANNOT_UNLOCK = 501666 -- You can't unlock that!
local NORMAL_MEANS = 502073  -- This lock cannot be picked by normal means.
local CANNOT_MANIPULATE = 502072 -- You don't see how that lock can be manipulated.
local YIELDS = 502076        -- The lock quickly yields to your skill.
local UNABLE = 502075        -- You are unable to pick the lock.
local BROKE = 502074         -- You broke the lockpick.

-- The sounds of the pick at work, of a lock that gives and of a pick that breaks.
local WORKING_SOUND = 0x241
local YIELDS_SOUND = 0x4A
local BROKE_SOUND = 0x3A4

-- How near the lock must be, and the seconds the picking takes.
local RANGE = 1
local SECONDS = 3

-- One failure in this many breaks the lockpick.
local BREAKS_ONE_IN = 4

local function told(lock, user, cliloc)
    item.message_cliloc(lock, user, cliloc)
end

-- Whether the lock is at hand: within a tile of the player, or carried by it.
local function at_hand(lock, user)
    return item.in_range(lock, user, RANGE) or item.owner(lock) == user
end

local function pick(serial, user, lock)
    if not at_hand(lock, user) then
        return
    end

    local level = item.get_prop(lock, "lock.level")

    if level == nil or level <= 0 then
        told(lock, user, NORMAL_MEANS)

        return
    end

    local points = mobile.skills(user).lockpicking or 0

    if points < (item.get_prop(lock, "lock.required") or 0) then
        told(lock, user, CANNOT_MANIPULATE)

        return
    end

    if skill.check(user, "lockpicking", level, item.get_prop(lock, "lock.max") or level) then
        told(lock, user, YIELDS)
        mobile.play_sound(user, YIELDS_SOUND)
        item.set_prop(lock, "locked", false)
        item.set_prop(lock, "lock.picker", user)

        return
    end

    -- A failure may break the lockpick, which is gone from its stack.
    if math.random(BREAKS_ONE_IN) == 1 and item.consume(serial) then
        told(lock, user, BROKE)
        mobile.play_sound(user, BROKE_SOUND)
    end

    told(lock, user, UNABLE)
end

-- Called when a player double clicks the lockpick.
function lockpick.on_use(serial, user)
    mobile.message_cliloc(user, WHAT)

    target.pick(user, function(picked)
        if picked.kind ~= "object" then
            return
        end

        local lock = picked.serial

        if item.template(lock) == nil or not at_hand(lock, user) then
            mobile.message_cliloc(user, CANNOT_UNLOCK)

            return
        end

        if not item.get_prop(lock, "locked") then
            told(lock, user, NOT_LOCKED)

            return
        end

        mobile.play_sound(user, WORKING_SOUND)

        timer.after(SECONDS, function()
            pick(serial, user, lock)
        end)
    end)

    return true
end
