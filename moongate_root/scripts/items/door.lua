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
--   Locks and keys come later.
--
-- Props it reads (set by .decorate):
--   facing           the DoorFacingType, such as west_cw; none opens in place
--   decoration_type  the kind, such as MetalDoor, which picks the sounds
--   door.link        the serial of the door that opens with this one
--
-- Props it keeps:
--   door.open        true while the door is open
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

-- The door and the door linked to it, while that one still exists.
local function doors_of(serial)
    local doors = { serial }
    local link = item.get_prop(serial, "door.link")

    if link and link ~= serial and item.item_id(link) then
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

local function open_one(serial)
    local here = item.location(serial)

    if not here or is_open(serial) then
        return
    end

    local dx, dy = offset(serial)
    item.set_item_id(serial, item.item_id(serial) + 1)
    item.move_to(serial, here.x + dx, here.y + dy, here.z)
    item.set_prop(serial, "door.open", true)
    item.play_sound(serial, (sounds(serial)))
end

local function close_one(serial)
    local here = item.location(serial)

    if not here or not is_open(serial) then
        return
    end

    local dx, dy = offset(serial)
    local _, closing = sounds(serial)
    item.set_item_id(serial, item.item_id(serial) - 1)
    item.move_to(serial, here.x - dx, here.y - dy, here.z)
    item.set_prop(serial, "door.open", nil)
    item.play_sound(serial, closing)
end

-- Whether nobody stands where the open door goes back to.
local function free_to_close(serial)
    local here = item.location(serial)

    if not here or not is_open(serial) then
        return true
    end

    local dx, dy = offset(serial)

    return not world.is_occupied(here.map, here.x - dx, here.y - dy)
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

-- Called when a player double clicks the door.
function door.on_use(serial, user)
    if is_open(serial) then
        close(serial)

        return true
    end

    for _, each in ipairs(doors_of(serial)) do
        open_one(each)
    end

    cancel_auto_close(serial)
    schedule_auto_close(serial, AUTO_CLOSE_SECONDS)

    return true
end
