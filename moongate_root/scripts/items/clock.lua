-- ==============================================================================
-- Moongate - scripts/items/clock.lua
--
-- What it is for:
--   The item script of the clocks (the item templates 0x104b_clock and
--   0x104c_clock, and decoration_clock for those ".decorate" places), as
--   ModernUO's Clock: a player who double clicks one reads the part of the day
--   and the time to the minute where they stand, as texts of the client over
--   the clock, in the language of the client.
--
-- Functions:
--   on_use(serial, user)   a player double clicks the clock; returns true
-- ==============================================================================

clock = {}

-- Client text: "~1_TIME~ to be exact".
local exact_cliloc = 1042958

-- The client text of the part of the day an hour of the 24 falls in, from "'Tis the witching hour. 12 Midnight."
-- to "It's late at night".
local function part_of_day(hours)
    if hours >= 20 then
        return 1042957
    elseif hours >= 16 then
        return 1042956
    elseif hours >= 13 then
        return 1042955
    elseif hours >= 12 then
        return 1042954
    elseif hours >= 8 then
        return 1042953
    elseif hours >= 4 then
        return 1042952
    elseif hours >= 1 then
        return 1042951
    end

    return 1042950
end

-- Called when a player double clicks the clock.
function clock.on_use(serial, user)
    local at = mobile.location(user)

    if not at then
        return true
    end

    local now = world.time(at.map, at.x)
    local hours = now.hours % 12

    if hours == 0 then
        hours = 12
    end

    item.message_cliloc(serial, user, part_of_day(now.hours))
    item.message_cliloc(serial, user, exact_cliloc, string.format("%d:%02d", hours, now.minutes))

    return true
end
