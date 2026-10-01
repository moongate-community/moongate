-- ==============================================================================
-- Moongate - scripts/mobiles/banker.lua
--
-- What it is for:
--   The bankers: a player who says "bank" nearby gets his bank box opened, as
--   ModernUO's banker does. The client turns the word into the speech keyword
--   SpeechKeywordType.Bank in any language ("banca", "Bank", ...); the plain
--   word is read too, for a client that sends no keywords. The templates
--   banker, m_banker and f_banker use it.
--
-- Functions:
--   on_speech(serial, speaker, text, keywords)  a player speaks within 15
--                                               cells: the bank keyword or
--                                               word opens his bank box
-- ==============================================================================

banker = {}

local function has_keyword(keywords, wanted)
    for _, keyword in ipairs(keywords or {}) do
        if keyword == wanted then
            return true
        end
    end

    return false
end

-- Called when a player says something within 15 cells. Every banker in range opens the same box: the client just
-- shows it once.
function banker.on_speech(serial, speaker, text, keywords)
    if has_keyword(keywords, SpeechKeywordType.Bank) or text:lower():find("bank", 1, true) then
        bank.open(speaker)
    end
end
