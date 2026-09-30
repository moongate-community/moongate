-- ==============================================================================
-- Moongate - scripts/items/door.lua
--
-- What it is for:
--   The item script of the doors and gates ".decorate" places (item template
--   decoration_door). Double clicking a closed door opens it: its graphic goes
--   to the next one, it swings aside by its facing and plays its opening sound;
--   its linked door opens with it. Double clicking an open door closes both,
--   when nobody stands in either doorway. An open door closes by itself after
--   20 seconds, then retries every 10 seconds while the doorway is taken.
--   A locked closed door opens only for a player carrying a key with its
--   key.value (anywhere in the backpack) and for game masters and
--   administrators, as in ModernUO; it stays locked. Anyone else reads
--   "That is locked.".
--
-- Props it reads (set by .decorate):
--   facing           the DoorFacingType, such as west_cw; none opens in place
--   decoration_type  the kind, such as MetalDoor, which picks the sounds
--   door.link        the serial of the door that opens with this one
--   locked           true keeps the closed door shut for players
--   key.value        the number a key must carry (prop key.value) to open it
--
-- Props it keeps:
--   door.open        true while the door is open
--   door.x, door.y,  the closed spot of an open door, where closing puts it
--   door.z           back and where ".decorate" looks for it
--
-- Functions:
--   on_use(serial, user)             a player double clicks the door;
--                                    returns true
-- ==============================================================================

door = {}

-- How far an open door stands from its closed spot, by facing (ModernUO's BaseDoor).
local OFFSETS = {
    west_cw = { -1, 1 },
    east_ccw = { 1, 1 },
    west_ccw = { -1, 0 },
    east_cw = { 1, -1 },
    south_cw = { 1, 1 },
    north_ccw = { 1, -1 },
    south_ccw = { 0, 0 },
    north_cw = { 0, -1 },
}

local AUTO_CLOSE_SECONDS = 20
local RETRY_SECONDS = 10

-- The pending auto-close timer of each open door, by serial.
local pending = {}

-- The opening and closing sounds of a kind.
local function sounds(serial)
    local kind = item.get_prop(serial, "decoration_type") or ""

    if kind:find("Secret") then
        return 0xED, 0xF4
    elseif kind:find("Metal") or kind:find("Iron") then
        return 0xEC, 0xF3
    elseif kind:find("Gate") or kind:find("Rattan") then
        return 0xEB, 0xF2
    end

    return 0xEA, 0xF1
end

local function offset(serial)
    local by_facing = OFFSETS[item.get_prop(serial, "facing") or ""]

    if by_facing then
        return by_facing[1], by_facing[2]
    end

    return 0, 0
end

-- The door and the door linked to it, while that one still exists and links back.
local function doors_of(serial)
    local doors = { serial }
    local link = item.get_prop(serial, "door.link")

    if link and link ~= serial and item.item_id(link) and item.get_prop(link, "door.link") == serial then
        doors[2] = link
    end

    return doors
end

local function is_open(serial)
    return item.get_prop(serial, "door.open") == true
end

local function cancel_auto_close(serial)
    if pending[serial] then
        timer.cancel(pending[serial])
        pending[serial] = nil
    end
end

-- Where an open door goes back to: the spot it kept, or its offset back for a door opened before it kept one.
local function closed_spot(serial, here)
    local x, y, z = item.get_prop(serial, "door.x"), item.get_prop(serial, "door.y"), item.get_prop(serial, "door.z")

    if x and y and z then
        return x, y, z
    end

    local dx, dy = offset(serial)

    return here.x - dx, here.y - dy, here.z
end

-- Opens one door; false when it is not on the ground or cannot swing aside.
local function open_one(serial)
    local here = item.location(serial)

    if not here or is_open(serial) then
        return false
    end

    local dx, dy = offset(serial)

    if not item.move_to(serial, here.x + dx, here.y + dy, here.z) then
        return false
    end

    item.set_item_id(serial, item.item_id(serial) + 1)
    item.set_prop(serial, "door.open", true)
    item.set_prop(serial, "door.x", here.x)
    item.set_prop(serial, "door.y", here.y)
    item.set_prop(serial, "door.z", here.z)
    item.play_sound(serial, (sounds(serial)))

    return true
end

local function close_one(serial)
    local here = item.location(serial)

    if not here or not is_open(serial) then
        return
    end

    local x, y, z = closed_spot(serial, here)

    if not item.move_to(serial, x, y, z) then
        return
    end

    local _, closing = sounds(serial)
    item.set_item_id(serial, item.item_id(serial) - 1)
    item.set_prop(serial, "door.open", nil)
    item.set_prop(serial, "door.x", nil)
    item.set_prop(serial, "door.y", nil)
    item.set_prop(serial, "door.z", nil)
    item.play_sound(serial, closing)
end

-- Whether nobody stands where the open door goes back to.
local function free_to_close(serial)
    local here = item.location(serial)

    if not here or not is_open(serial) then
        return true
    end

    local x, y = closed_spot(serial, here)

    return not world.is_occupied(here.map, x, y)
end

-- Closes the door and its linked door; false when a doorway is taken.
local function close(serial)
    local doors = doors_of(serial)

    for _, each in ipairs(doors) do
        if not free_to_close(each) then
            return false
        end
    end

    for _, each in ipairs(doors) do
        cancel_auto_close(each)
        close_one(each)
    end

    return true
end

local function schedule_auto_close(serial, seconds)
    pending[serial] = timer.after(seconds, function()
        pending[serial] = nil

        if is_open(serial) and not close(serial) then
            schedule_auto_close(serial, RETRY_SECONDS)
        end
    end)
end

-- UOX3's messages, in the server's language.
local LOCKED_MESSAGE = 398
local STAFF_MESSAGE = 404
local KEY_MESSAGE = 405

local function say(serial, user, id, english)
    item.message(serial, user, localization.text(id) or english)
end

-- Whether the player gets through a locked door: staff always, others with its key.
local function may_pass(serial, user)
    if world.is_staff(user) then
        say(serial, user, STAFF_MESSAGE, "The door being locked magically unlocks itself to allow you passage.")

        return true
    end

    local key = item.get_prop(serial, "key.value")

    if key and world.carries(user, "key.value", key) then
        say(serial, user, KEY_MESSAGE, "Using your key, you quickly unlock and open the door.  You hastily relock it.")

        return true
    end

    say(serial, user, LOCKED_MESSAGE, "That is locked.")

    return false
end

-- Called when a player double clicks the door.
function door.on_use(serial, user)
    if not is_open(serial) and item.get_prop(serial, "locked") and not may_pass(serial, user) then
        return true
    end

    if is_open(serial) then
        close(serial)

        return true
    end

    local doors = doors_of(serial)

    if not open_one(serial) then
        return true
    end

    if doors[2] then
        open_one(doors[2])
    end

    cancel_auto_close(serial)
    schedule_auto_close(serial, AUTO_CLOSE_SECONDS)

    return true
end
