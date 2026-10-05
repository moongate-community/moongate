-- ==============================================================================
-- Moongate - scripts/mobiles/banker.lua
--
-- What it is for:
--   The bankers, as ModernUO's: a player within 12 tiles says a word and the
--   banker opens its bank box ("bank"), tells its balance ("balance"), hands
--   out gold ("withdraw 500") or takes it ("deposit 500"). The client turns
--   bank, balance and withdraw into speech keywords in any language ("banca",
--   "saldo", "prelievo"...); the plain word "bank" is read too, for a client
--   that sends no keywords, and "deposit" is an English word only: the client
--   has no keyword for it. The amount is the first number of the sentence. A
--   banker does no business with a criminal. It answers with the client's own
--   texts, so every player reads them in its language. When several bankers
--   hear the same words, one answers, turning to who asks. The templates banker, m_banker and
--   f_banker and their gypsybanker variants use it. How much a banker hands out
--   at one time is the setting ultima.bank.max_withdraw.
--
-- Functions:
--   on_speech(serial, speaker, text, keywords)  a player speaks within 15
--                                               cells: the banker answers a
--                                               bank word said within 12
-- ==============================================================================

banker = {}

-- How far a banker hears its customers, in tiles.
local range = 12

-- The client's texts the banker says.
local criminal_bank = 500378      -- Thou art a criminal and cannot access thy bank box.
local criminal_business = 500389  -- I will not do business with a criminal!
local too_much = 500381           -- Thou canst not withdraw so much at one time!
local not_enough = 500384         -- Ah, art thou trying to fool me? Thou hast not so much gold!
local backpack_full = 1048147     -- Your backpack can't hold anything else.
local withdrawn = 1010005         -- Thou hast withdrawn gold from thy account.
local balance_is = 1042759        -- Thy current bank balance is ~1_AMOUNT~ gold.
local deposited = 1042763         -- ~1_AMOUNT~ gold was deposited in your account.
local bank_full = 500390          -- Your bank box is full.

local function has_keyword(keywords, wanted)
    for _, keyword in ipairs(keywords or {}) do
        if keyword == wanted then
            return true
        end
    end

    return false
end

-- What the player asks for: the client's keyword first, then the words it has no keyword for.
local function command_of(text, keywords)
    if has_keyword(keywords, SpeechKeywordType.Withdraw) then
        return "withdraw"
    elseif has_keyword(keywords, SpeechKeywordType.Balance) then
        return "balance"
    elseif has_keyword(keywords, SpeechKeywordType.Bank) then
        return "bank"
    elseif has_keyword(keywords, SpeechKeywordType.Check) then
        return "check"
    end

    local said = text:lower()

    -- The word alone: "depository" deposits nothing. The space lets the word end the sentence.
    if (said .. " "):find("%f[%a]deposit%f[%A]") then
        return "deposit"
    elseif said:find("bank", 1, true) then
        return "bank"
    end

    return nil
end

-- The first number of the sentence, wherever it stands; nil with none, and for one no bank could hold.
local function amount_in(text)
    -- The ten digits of the client's keyboard: %d would also take the digits of other scripts, which are no number here.
    local digits = text:match("[0-9]+")

    if not digits or #digits > 10 then
        return nil
    end

    local amount = tonumber(digits)

    if not amount or amount < 1 or amount > 2000000000 then
        return nil
    end

    return amount
end

-- "1234567" as "1,234,567".
local function with_thousands(amount)
    local digits = tostring(math.floor(amount))

    while true do
        local grouped, found = digits:gsub("^(%d+)(%d%d%d)", "%1,%2")
        digits = grouped

        if found == 0 then
            return digits
        end
    end
end

local function near(serial, speaker)
    local here = npc.location(serial)
    local there = mobile.location(speaker)

    return here and there and here.map == there.map
        and math.abs(here.x - there.x) <= range and math.abs(here.y - there.y) <= range
end

local function withdraw(serial, speaker, amount)
    local result = bank.withdraw(speaker, amount)

    if result == BankResultType.Ok then
        npc.say_cliloc(serial, withdrawn)
    elseif result == BankResultType.TooMuch then
        npc.say_cliloc(serial, too_much)
    elseif result == BankResultType.NotEnoughGold or result == BankResultType.NoBank then
        -- A player who never opened its bank has no gold in it.
        npc.say_cliloc(serial, not_enough)
    elseif result == BankResultType.BackpackFull then
        npc.say_cliloc(serial, backpack_full)
    end
end

local function deposit(serial, speaker, amount)
    local result = bank.deposit(speaker, amount)

    if result == BankResultType.Ok then
        npc.say_cliloc(serial, deposited, with_thousands(amount))
    elseif result == BankResultType.NotEnoughGold then
        npc.say_cliloc(serial, not_enough)
    elseif result == BankResultType.BankFull then
        npc.say_cliloc(serial, bank_full)
    elseif result == BankResultType.NoBank then
        -- No bank box yet: it is made and shown, and the player asks again.
        bank.open(speaker)
    end
end

-- Called when a player says something within 15 cells.
function banker.on_speech(serial, speaker, text, keywords)
    local command = command_of(text, keywords)

    -- Checks are not written yet.
    if not command or command == "check" then
        return
    end

    -- Every banker in range hears the words: the first one serves, and the gold moves once.
    if not near(serial, speaker) or not bank.attend(speaker) then
        return
    end

    -- A banker never walks, so it would face where it was born for ever: it turns to who asks.
    npc.look_at(serial, speaker)

    if mobile.criminal(speaker) then
        npc.say_cliloc(serial, command == "bank" and criminal_bank or criminal_business)
        return
    end

    if command == "bank" then
        bank.open(speaker)
    elseif command == "balance" then
        npc.say_cliloc(serial, balance_is, with_thousands(bank.balance(speaker) or 0))
    else
        local amount = amount_in(text)

        if not amount then
            return
        end

        if command == "withdraw" then
            withdraw(serial, speaker, amount)
        else
            deposit(serial, speaker, amount)
        end
    end
end
