-- ==============================================================================
-- Moongate - scripts/items/treasure_chest.lua
--
-- What it is for:
--   The item script of the treasure chests of the dungeons (templates
--   treasure_chest_level_1 to 4), as ModernUO's TreasureChestLevel1 to 4: a
--   chest is made locked, and only a lockpick, with enough Lockpicking, or a
--   game master opens it. A template uses it with script_id = "treasure_chest".
--
--   On creation the chest takes the lock of its level: the points of Lockpicking
--   it needs (lock.required) are 57, 72, 84 and 92; the try runs from that less a
--   roll of 1 to 10 (lock.level) to that plus a roll of 1 to 10 (lock.max). A
--   locked chest does not open: "It appears to be locked.". A game master opens it
--   with the message "That is locked, but you open it with your godly powers.",
--   and a picked chest opens as any container.
--
--   The traps of ModernUO's chests are not there yet.
--
-- Props it sets:
--   locked, lock.required, lock.level, lock.max   as lockpick.lua reads them
--
-- Functions:
--   on_create(serial)      a chest is made: it takes its lock
--   on_use(serial, user)   a player double clicks the chest; it returns true
--                          when the chest stays shut, nothing when it opens
-- ==============================================================================

treasure_chest = {}

-- The client's own texts.
local LOCKED = 501747        -- It appears to be locked.
local GODLY = 502502         -- That is locked, but you open it with your godly powers.

-- The points of Lockpicking each level asks, as ModernUO's.
local REQUIRED = {
    treasure_chest_level_1 = 57,
    treasure_chest_level_2 = 72,
    treasure_chest_level_3 = 84,
    treasure_chest_level_4 = 92,
}

-- How far a roll takes the level and the maximum from the points required.
local LEAST_ROLL = 1
local MOST_ROLL = 10

-- Called when a chest is made.
function treasure_chest.on_create(serial)
    local required = REQUIRED[item.template(serial) or ""]

    if required == nil then
        return
    end

    item.set_prop(serial, "locked", true)
    item.set_prop(serial, "lock.required", required)
    item.set_prop(serial, "lock.level", required - math.random(LEAST_ROLL, MOST_ROLL))
    item.set_prop(serial, "lock.max", required + math.random(LEAST_ROLL, MOST_ROLL))
end

-- Called when a player double clicks the chest.
function treasure_chest.on_use(serial, user)
    if not item.get_prop(serial, "locked") then
        return
    end

    if world.is_staff(user) then
        item.message_cliloc(serial, user, GODLY)

        return
    end

    item.message_cliloc(serial, user, LOCKED)

    return true
end
