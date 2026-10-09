-- ==============================================================================
-- Moongate - scripts/gumps/help_page_kind.lua
--
-- What it is for:
--   The second step of Call a game master (templates/gumps/help_page_kind.xml): the
--   button names what the request is about, the player types a line in the journal
--   line, and the request goes to the queue of the game masters (the .pages gump).
--   It is refused before the prompt when the player already has a request open or
--   asked too soon, and again after it when something changed meanwhile. A player
--   in jail may ask too.
--
-- Functions:
--   question, bug, suggestion, harassment (player, response, args)
-- ==============================================================================

help_page_kind = {}

-- Moongate messages (data/messages/<language>/moongate.toml).
local type_message = 30211
local sent_message = 30212
local open_message = 30213
local wait_message = 30214
local nothing_message = 30215

local function say(player, id, ...)
    mobile.message(player, localization.get(id, ...))
end

-- Tells why a request was not made, from the reason and the seconds help.can_page and help.create_page give.
local function refuse(player, reason, seconds)
    if reason == "wait" then
        say(player, wait_message, seconds)
    elseif reason == "open" then
        say(player, open_message)
    else
        say(player, nothing_message)
    end
end

local function ask(player, kind)
    local can = help.can_page(player)

    if not can.ok then
        refuse(player, can.reason, can.seconds)

        return
    end

    say(player, type_message)

    if not prompt.ask(player, function(text)
        if text == nil then
            say(player, nothing_message)

            return
        end

        local made = help.create_page(player, kind, text)

        if made.id == nil then
            refuse(player, made.reason, made.seconds)

            return
        end

        say(player, sent_message)
    end) then
        say(player, nothing_message)
    end
end

function help_page_kind.question(player, response, args)
    ask(player, HelpPageKindType.Question)
end

function help_page_kind.bug(player, response, args)
    ask(player, HelpPageKindType.Bug)
end

function help_page_kind.suggestion(player, response, args)
    ask(player, HelpPageKindType.Suggestion)
end

function help_page_kind.harassment(player, response, args)
    ask(player, HelpPageKindType.Harassment)
end
