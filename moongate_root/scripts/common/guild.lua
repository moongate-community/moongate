-- ==============================================================================
-- Moongate - scripts/common/guild.lua
--
-- What it is for:
--   What the scripts of the guildmasters share, as ModernUO's: a player within 2
--   cells says the guildmaster's name and "join" or "member" to be told the price
--   of its guild (500 gold), drops exactly that gold on it to join, and says its
--   name and "resign" or "quit" to leave, a week after joining at the earliest.
--   A guildmaster is an NPC whose mobile template has an npc_guild. The server
--   keeps the membership (the npcguild module). Used with
--   local guild = require("common.guild").
--
-- Functions:
--   guild.listen(serial, speaker, text, keywords)  the words join and resign
--   guild.drop(serial, giver, item)                the answer of an on_drag_drop:
--                                                  true when the gold joined the guild
-- ==============================================================================

local guild = {}

-- How near a player must be, in cells.
local speech_range = 2

local function has_keyword(keywords, wanted)
    for _, keyword in ipairs(keywords or {}) do
        if keyword == wanted then
            return true
        end
    end

    return false
end

-- Whether the words begin with the name of the guildmaster, as ModernUO's WasNamed.
local function named(serial, text)
    local name = npc.name(serial)

    return name and text and text:lower():sub(1, #name) == name:lower()
end

function guild.listen(serial, speaker, text, keywords)
    if not npcguild.of(serial) then
        return
    end

    local joining = has_keyword(keywords, SpeechKeywordType.Join)
    local resigning = has_keyword(keywords, SpeechKeywordType.Resign)

    if not (joining or resigning) or not named(serial, text) then
        return
    end

    local here = npc.location(serial)
    local there = mobile.location(speaker)

    if not (here and there and here.map == there.map
        and math.abs(here.x - there.x) <= speech_range and math.abs(here.y - there.y) <= speech_range) then
        return
    end

    npc.look_at(serial, speaker)

    if joining then
        npcguild.quote(serial, speaker)
    else
        npcguild.resign(serial, speaker)
    end
end

function guild.drop(serial, giver, item)
    return npcguild.of(serial) ~= nil and npcguild.join(serial, giver, item)
end

return guild
