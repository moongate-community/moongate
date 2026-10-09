-- ==============================================================================
-- Moongate - scripts/gumps/pages_detail.lua
--
-- What it is for:
--   The script of one request for a game master (templates/gumps/pages_detail.xml),
--   opened from the queue: go to the player, take the request, answer it or close it.
--   Staff only, checked again by every button since the rank may go while the gump is
--   open. A request that another game master closed meanwhile says so and nothing
--   changes.
--
-- Functions:
--   go(player, response, args)      takes the game master to the player
--   take(player, response, args)    marks the request as taken
--   answer(player, response, args)  sends the typed answer and closes the request
--   close(player, response, args)   closes the request with no answer
-- ==============================================================================

pages_detail = {}

local help_pages = require("common.help_pages")

local function tell(player, text)
    mobile.message(player, text)
end

-- The request of the gump when the player is staff and the request is still to be dealt with; else says why not.
local function active_page(player, args)
    if not world.is_staff(player) then
        return nil
    end

    local id = tonumber(args.page)
    local page = id and help.page(id)

    if page == nil or page.status == HelpPageStatusType.Closed then
        tell(player, "Request " .. tostring(args.page) .. " is already closed.")

        return nil
    end

    return page
end

local function show(player, page, answer)
    gump.open(player, "pages_detail", help_pages.detail_args(help.page(page.id), answer))
end

function pages_detail.go(player, response, args)
    local page = active_page(player, args)

    if page == nil then
        return
    end

    -- The player where it stands now; where it asked when it is offline.
    local where = page.online and mobile.location(page.player) or { x = page.x, y = page.y, z = page.z, map = page.map }

    if not mobile.teleport(player, where.x, where.y, where.z, where.map) then
        tell(player, "You cannot go to " .. page.name .. ": its map is not loaded.")
    end

    show(player, page)
end

function pages_detail.take(player, response, args)
    local page = active_page(player, args)

    if page == nil then
        return
    end

    if help.take(page.id, player) then
        tell(player, "Request " .. page.id .. " is yours.")
    end

    show(player, page)
end

function pages_detail.answer(player, response, args)
    local page = active_page(player, args)

    if page == nil then
        return
    end

    local typed = (response.text and response.text[1] or ""):match("^%s*(.-)%s*$")

    if typed == "" then
        tell(player, "Type an answer first.")
        show(player, page, "")

        return
    end

    if help.answer(page.id, player, typed) then
        tell(player, "Answered request " .. page.id .. ".")
    else
        tell(player, "Request " .. page.id .. " is already closed.")
    end

    gump.open(player, "pages")
end

function pages_detail.close(player, response, args)
    local page = active_page(player, args)

    if page == nil then
        return
    end

    if help.close(page.id, player) then
        tell(player, "Closed request " .. page.id .. ".")
    end

    gump.open(player, "pages")
end
