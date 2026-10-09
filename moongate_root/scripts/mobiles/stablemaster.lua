-- ==============================================================================
-- Moongate - scripts/mobiles/stablemaster.lua
--
-- What it is for:
--   The animal trainers, who keep a stable: a player within 12 tiles says "stable"
--   and picks one of its pets, or chooses Stable in the context menu, and leaves
--   it for a fee; "claim" opens the list of the pets in the stable, one button
--   each (templates/gumps/stable_claim.xml), and Claim All in the menu takes them
--   all back. The pet is kept as its template and its owner (the stable module);
--   a claimed pet comes back beside the player. Only a pet that is a mount of the
--   player's own can be left. A template uses it with script_id = "stablemaster".
--
--   An animal trainer is a vendor too: it keeps its shop (common/shop.lua) and
--   teaches the skills it has (common/training.lua), as the other vendors.
--
-- Functions:
--   on_speech(serial, speaker, text, keywords)  "stable" and "claim", and the
--                                               words of a shop and of the lessons
--   on_context_menu(serial, player)             Stable, Claim All, and the shop
--                                               and lesson entries
--   on_context_menu_select(serial, player, id)  the player chose one
--   on_drag_drop(serial, giver, item)           gold dropped on it pays a lesson
-- ==============================================================================

local shop = require("common.shop")
local training = require("common.training")

stablemaster = {}

-- How far a player may be to be heard or to pick an entry of the menu, in tiles.
local reach = 12

-- The client's texts of a stablemaster.
local PROMPT = 1042558        -- I charge 30 gold per pet for a real week's stable time. Which animal wouldst thou like to stable here?
local STABLED = 1049677       -- Your pet has been stabled.
local CANNOT = 1048053        -- You can't stable that!
local NOT_YOURS = 1042562     -- You do not own that pet!
local STABLE_FULL = 1042565   -- You have too many pets in the stables!
local NO_GOLD = 1042556       -- Thou dost not have enough gold, not even in thy bank account.
local TOO_FAR = 500446        -- That is too far away.
local NONE = 502671           -- But I have no animals stabled with me at the moment!
local THE_LIST = 502100       -- I currently have the following pets of yours stabled right now...
local HANDED = 1042559        -- Here you go... and good day to you!

-- The context menu texts: Stable, Claim All.
local STABLE_ENTRY = 3006126
local CLAIM_ALL_ENTRY = 3006127

-- What the stablemaster says to each answer of the stable.
local answers = {
    [StableResultType.Ok] = STABLED,
    [StableResultType.NotAPet] = CANNOT,
    [StableResultType.NotYours] = NOT_YOURS,
    [StableResultType.TooFar] = TOO_FAR,
    [StableResultType.Dying] = CANNOT,
    [StableResultType.Full] = STABLE_FULL,
    [StableResultType.NoGold] = NO_GOLD,
    [StableResultType.Failed] = CANNOT,
}

local function has_keyword(keywords, wanted)
    for _, keyword in ipairs(keywords or {}) do
        if keyword == wanted then
            return true
        end
    end

    return false
end

local function near(serial, player, range)
    local here = npc.location(serial)
    local there = mobile.location(player)

    return here ~= nil and there ~= nil and here.map == there.map
        and math.abs(here.x - there.x) <= range and math.abs(here.y - there.y) <= range
end

-- The player picks the pet to leave.
local function begin_stable(serial, player)
    -- A ghost leaves nothing: the menu may have been opened before it died.
    if mobile.is_dead(player) then
        return
    end

    npc.look_at(serial, player)
    mobile.message_cliloc(player, PROMPT)

    target.pick(player, function(picked)
        if picked.kind ~= "object" then
            return
        end

        -- The player may have walked away while the cursor was out.
        if mobile.is_dead(player) or not near(serial, player, reach) then
            npc.say_cliloc(serial, TOO_FAR)

            return
        end

        local said = answers[stable.stable(player, picked.serial)]

        if said then
            npc.say_cliloc(serial, said)
        end
    end)
end

-- The list of the pets in the stable, one button each.
local function begin_claim(serial, player)
    if mobile.is_dead(player) then
        return
    end

    npc.look_at(serial, player)

    local pets = stable.pets(player)

    if pets == nil or #pets == 0 then
        npc.say_cliloc(serial, NONE)

        return
    end

    npc.say_cliloc(serial, THE_LIST)
    gump.open(player, "stable_claim", { stablemaster = serial })
end

-- Every pet, from the first place: the list is one shorter at each claim.
local function claim_all(serial, player)
    if mobile.is_dead(player) then
        return
    end

    npc.look_at(serial, player)

    local pets = stable.pets(player)

    if pets == nil or #pets == 0 then
        npc.say_cliloc(serial, NONE)

        return
    end

    for _ = 1, #pets do
        local first = (stable.pets(player) or {})[1]

        if first == nil then
            break
        end

        stable.claim(player, 1, first.template)
    end

    npc.say_cliloc(serial, HANDED)
end

function stablemaster.on_speech(serial, speaker, text, keywords)
    training.listen(serial, speaker, keywords)
    shop.listen(serial, speaker, keywords)

    local stabling = has_keyword(keywords, SpeechKeywordType.Stable)
    local claiming = has_keyword(keywords, SpeechKeywordType.Claim)

    if not (stabling or claiming) or mobile.is_dead(speaker) or not near(serial, speaker, reach) then
        return
    end

    -- Every stablemaster in range hears the words: the first one that can serve does.
    if not stable.attend(speaker) then
        return
    end

    if stabling then
        begin_stable(serial, speaker)
    else
        begin_claim(serial, speaker)
    end
end

function stablemaster.on_context_menu(serial, player)
    if mobile.is_dead(player) then
        return {}
    end

    local entries = shop.entries(serial, player, reach)

    for _, entry in ipairs(training.entries(serial, player, reach)) do
        entries[#entries + 1] = entry
    end

    entries[#entries + 1] = { id = "stable", cliloc = STABLE_ENTRY, range = reach }
    entries[#entries + 1] = { id = "claim_all", cliloc = CLAIM_ALL_ENTRY, range = reach }

    return entries
end

function stablemaster.on_context_menu_select(serial, player, id)
    if id == "stable" then
        begin_stable(serial, player)
    elseif id == "claim_all" then
        claim_all(serial, player)
    elseif not shop.select(serial, player, id) then
        training.select(serial, player, id)
    end
end

function stablemaster.on_drag_drop(serial, giver, item)
    return training.drop(serial, giver, item)
end
