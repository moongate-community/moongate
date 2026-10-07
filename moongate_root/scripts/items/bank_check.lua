-- ==============================================================================
-- Moongate - scripts/items/bank_check.lua
--
-- What it is for:
--   The item script of the bank checks (the item template bank_check), as
--   ModernUO's BankCheck: a banker writes one for gold of the bank ("check
--   5000"), and a double click on it inside the open bank box turns it back
--   into coins of the box, in piles of 60000. A box with room for part of the
--   gold takes what fits and the check keeps the rest. Outside the bank box a
--   check is only a piece of paper worth what its tooltip says.
--
-- Functions:
--   on_use(serial, user)   a player double clicks the check; returns true
-- ==============================================================================

local numbers = require("common.numbers")

bank_check = {}

-- The client's texts the player reads.
local must_be_in_bank = 1047026  -- That must be in your bank box to use it.
local deposited = 1042763        -- ~1_AMOUNT~ gold was deposited in your account.
local bank_full = 500390         -- Your bank box is full.

function bank_check.on_use(serial, user)
    local before = bank.worth(serial) or 0
    local result = bank.cash(user, serial)

    if result == BankResultType.Ok then
        -- Cashed whole the check is gone; a box with room for part of it leaves the rest on the check.
        mobile.message_cliloc(user, deposited, numbers.with_thousands(before - (bank.worth(serial) or 0)))
    elseif result == BankResultType.BankFull then
        mobile.message_cliloc(user, bank_full)
    elseif result == BankResultType.NotInBank or result == BankResultType.NoBank then
        mobile.message_cliloc(user, must_be_in_bank)
    end

    return true
end
