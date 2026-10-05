-- ==============================================================================
-- Moongate - scripts/gumps/jail_sentence.lua
--
-- What it is for:
--   The script of the gump of the jail (templates/gumps/jail_sentence.xml),
--   opened by .jail: it fills the "rows" slot. First the target: a button that
--   gives the cursor to pick the character to jail, and its name. Then the
--   cells of data/jail.toml, ten per page. A free cell has a button that sends
--   the character picked there for the days typed in the gump, with the
--   reason typed beside them; with nobody picked it has none. A cell that holds someone shows who, how long is left
--   and a button that releases it with no fine. Every cell has a button that
--   takes the game master into it, on the map of the jail. A character already
--   in jail has a line of its own at the top, with its release; one whose days
--   are over while it is offline is said to be free at its next login. Staff
--   only: anyone else sees an empty gump.
--
-- Functions:
--   rows(g, player, args)  fills the slot; args.target is the serial of the
--                          character to jail and args.name its name, both
--                          absent until one is picked; args.days and
--                          args.reason what the two fields show
-- ==============================================================================

jail_sentence = {}

local per_page = 10
local row_height = 22

-- The frame is 460 wide and the slot starts at 20: texts are cut here instead of running over the edge. From the
-- left: the button that jails, the cell, who is inside, the release and the go.
local number_width = 60
local row_width = 225
local text_height = 20
local release_x = 335
local go_x = 395

local free_hue = 68
local taken_hue = 38

-- "2d 4h", "5h 10m" or "12m": the two largest units, never less than a minute.
local function left(seconds)
    local minutes = math.floor(seconds / 60)
    local days = math.floor(minutes / 1440)
    local hours = math.floor((minutes % 1440) / 60)

    if days > 0 then
        return days .. "d " .. hours .. "h"
    end

    if hours > 0 then
        return hours .. "h " .. (minutes % 60) .. "m"
    end

    return math.max(minutes, 1) .. "m"
end

-- The gump again, with what was typed in its two fields when the answer of a button has them.
local function open(player, args, response)
    local text = response and response.text or {}

    gump.open(player, "jail_sentence", {
        target = args.target,
        name = args.name,
        days = text[1] or args.days or "1",
        reason = text[2] or args.reason or ""
    })
end

local function refusal(result)
    if result == JailResultType.Disabled then
        return "The jail is not set up."
    elseif result == JailResultType.NoSuchCell then
        return "That cell is gone."
    elseif result == JailResultType.CellOccupied then
        return "That cell is taken."
    elseif result == JailResultType.BadDays then
        return "Type the days as a whole number from 1 to " .. jail.max_days() .. "."
    elseif result == JailResultType.NotInWorld then
        return "That character is no longer here."
    elseif result == JailResultType.Refused then
        return "You cannot jail yourself or the staff of your rank."
    elseif result == JailResultType.MapNotLoaded then
        return "The jail map is not loaded."
    end

    return "The jail refused."
end

-- The cursor to pick who is jailed: the gump opens again on the character picked, or on the one it had.
local function pick(who, response, args)
    if not world.is_staff(who) then
        return
    end

    target.pick(who, function(picked)
        if not world.is_staff(who) then
            return
        end

        local name = picked.kind == "object" and mobile.name(picked.serial) or nil

        if name then
            open(who, { target = picked.serial, name = name }, response)
            return
        end

        if picked.kind == "canceled" then
            -- Put away by the player: the gump comes back as it was. Taken by another cursor, such as a second
            -- .jail, or lost with the player: nothing is opened over what came after.
            if picked.reason == "canceled" then
                open(who, args, response)
            end

            return
        end

        -- An item, the ground or someone gone is not a character.
        mobile.message(who, "That is not a character.")
        open(who, args, response)
    end)
end

-- Into the cell, on the map of the jail: a place of the same name may exist on another map.
local function go(who, response, args, cell)
    if not world.is_staff(who) then
        return
    end

    if not mobile.teleport(who, cell.x, cell.y, cell.z, cell.map) then
        mobile.message(who, "That cell cannot be reached.")
    end

    open(who, args, response)
end

local function send(who, response, args, cell)
    -- The rank may have gone while the gump was open.
    if not world.is_staff(who) then
        return
    end

    local typed = response.text[1] or ""
    local days = tonumber(typed)

    -- No number, a fraction or a sentence out of range: asked again, with one day in the field.
    if not days or days ~= math.floor(days) or days < 1 or days > jail.max_days() then
        mobile.message(who, refusal(JailResultType.BadDays))
        open(who, { target = args.target, name = args.name, days = "1", reason = response.text[2] })
        return
    end

    -- An empty field is no reason.
    local reason = response.text[2]

    if reason == "" then
        reason = nil
    end

    local result = jail.send(args.target, cell, days, who, reason)

    if result == JailResultType.Ok then
        mobile.message(who, args.name .. " is in cell " .. cell .. " for " .. math.floor(days) .. " days.")
        return
    end

    mobile.message(who, refusal(result))
    open(who, args, response)
end

local function release(who, args, prisoner, name)
    if world.is_staff(who) and jail.release(prisoner) then
        mobile.message(who, name .. " is released.")
    end

    open(who, args)
end

function jail_sentence.rows(g, player, args)
    if not world.is_staff(player) then
        return
    end

    -- Who is jailed, and the button that picks it.
    g:button{ x = 0, y = 0, up = 4005, down = 4007, on_click = function(who, response)
        pick(who, response, args)
    end }
    g:label_cropped{ x = 35, y = 0, width = row_width + number_width + 100, height = text_height,
        hue = args.target and free_hue or taken_hue,
        text = args.target and "Target: " .. args.name or "Target: nobody. Press the button to pick one." }

    local top = row_height
    local sentence = args.target and jail.sentence(args.target)

    -- The character is already in jail: its release comes first, the cells move it elsewhere.
    if sentence then
        g:button{ x = 0, y = top, up = 4017, down = 4019, on_click = function(who)
            release(who, args, args.target, args.name)
        end }
        g:label_cropped{ x = 35, y = top, width = row_width + number_width, height = text_height, hue = taken_hue,
            text = sentence.seconds_left > 0
                and "In cell " .. sentence.cell .. ", " .. left(sentence.seconds_left) .. " left"
                -- Its days are over and it is not in the world: the jail releases it when it is back.
                or "Sentence over: free at its next login" }
        top = top + row_height
    end

    -- What the three columns of buttons do.
    g:label_cropped{ x = 0, y = top, width = 35, height = text_height, text = "Jail" }
    g:label_cropped{ x = release_x - 10, y = top, width = 55, height = text_height, text = "Release" }
    g:label_cropped{ x = go_x + 5, y = top, width = 30, height = text_height, text = "Go" }
    top = top + row_height

    g:pager{ previous = { x = 0, y = top + per_page * row_height + 10 }, next = { x = 390, y = top + per_page * row_height + 10 } }

    for index, cell in ipairs(jail.cells()) do
        local y = top + g:paginate(index, per_page) * row_height

        g:label_cropped{ x = 35, y = y, width = number_width, height = text_height, text = "Cell " .. cell.number }

        if cell.prisoner and cell.prisoner ~= args.target then
            g:label_cropped{ x = 100, y = y, width = row_width, height = text_height, hue = taken_hue,
                text = cell.name .. " - " .. left(cell.seconds_left) .. " left" }
            g:button{ x = release_x, y = y, up = 4017, down = 4019, on_click = function(who)
                release(who, args, cell.prisoner, cell.name)
            end }
        else
            -- Nobody picked, nobody to send.
            if args.target then
                g:button{ x = 0, y = y, up = 4005, down = 4007, on_click = function(who, response)
                    send(who, response, args, cell.number)
                end }
            end

            g:label_cropped{ x = 100, y = y, width = row_width, height = text_height, hue = free_hue,
                text = cell.prisoner and "here now" or "free" }
        end

        g:button{ x = go_x, y = y, up = 4005, down = 4007, on_click = function(who, response)
            go(who, response, args, cell)
        end }
    end
end
