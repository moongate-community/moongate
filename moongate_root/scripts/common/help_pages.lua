-- ==============================================================================
-- Moongate - scripts/common/help_pages.lua
--
-- What it is for:
--   What the two gumps of the staff about the requests for the game masters share
--   (scripts/gumps/pages.lua and pages_detail.lua): the words for a kind, a map,
--   an age and a status, the line of a request in the list, and the arguments of
--   its detail gump. A script takes it with local pages = require("common.help_pages").
--
-- Functions:
--   pages.kind_name(kind)      Question, Bug, Suggestion or Harassment
--   pages.map_name(map)        the name of a MapType number
--   pages.age_text(seconds)    such as 3 min
--   pages.status_text(page)    open, or taken by <name>
--   pages.row_text(page)       the line of the list
--   pages.detail_args(page)    the arguments of pages_detail for a page of help.pages
-- ==============================================================================

local pages = {}

local kinds = { [0] = "Question", [1] = "Bug", [2] = "Suggestion", [3] = "Harassment" }
local maps = { [0] = "Felucca", [1] = "Trammel", [2] = "Ilshenar", [3] = "Malas", [4] = "Tokuno", [5] = "Ter Mur" }

function pages.kind_name(kind)
    return kinds[kind] or "?"
end

function pages.map_name(map)
    return maps[map] or "?"
end

function pages.age_text(seconds)
    if seconds < 60 then
        return "now"
    end

    if seconds < 3600 then
        return math.floor(seconds / 60) .. " min"
    end

    if seconds < 86400 then
        return math.floor(seconds / 3600) .. " h"
    end

    return math.floor(seconds / 86400) .. " d"
end

function pages.status_text(page)
    if page.status == HelpPageStatusType.Taken then
        return "taken by " .. page.taken_by
    end

    if page.status == HelpPageStatusType.Closed then
        return "closed"
    end

    return "open"
end

function pages.row_text(page)
    return string.format("#%d %s, %s, %s, %s", page.id, page.name, pages.kind_name(page.kind),
        pages.age_text(page.age_seconds), pages.status_text(page))
end

function pages.detail_args(page, answer)
    return {
        page = tostring(page.id),
        name = page.name,
        kind = pages.kind_name(page.kind),
        status = pages.status_text(page),
        age = pages.age_text(page.age_seconds),
        where = string.format("%s %d, %d, %d", pages.map_name(page.map), page.x, page.y, page.z),
        text = page.text,
        answer = answer or "",
    }
end

return pages
