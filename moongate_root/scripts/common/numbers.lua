-- ==============================================================================
-- Moongate - scripts/common/numbers.lua
--
-- What it is for:
--   How the shipped scripts write a number a player reads: the banker
--   (mobiles/banker.lua) and the bank check (items/bank_check.lua) tell
--   amounts of gold with it. A script takes it with
--   local numbers = require("common.numbers").
--
-- Functions:
--   numbers.with_thousands(amount)   the whole part of a number with a comma
--                                    every three digits: 1234567 gives
--                                    "1,234,567"
-- ==============================================================================

local numbers = {}

function numbers.with_thousands(amount)
    local digits = tostring(math.floor(amount))

    while true do
        local grouped, found = digits:gsub("^(%d+)(%d%d%d)", "%1,%2")
        digits = grouped

        if found == 0 then
            return digits
        end
    end
end

return numbers
