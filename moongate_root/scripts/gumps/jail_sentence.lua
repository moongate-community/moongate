-- ==============================================================================
-- Moongate - scripts/gumps/jail_sentence.lua
--
-- What it is for:
--   The script of the gump of the jail (templates/gumps/jail_sentence.xml),
--   opened by .jail on a character: it fills the "rows" slot with the cells of
--   data/jail.toml, ten per page. A free cell has a button that sends the
--   character there for the days typed in the gump; a cell that holds someone
--   shows who, how long is left and a button that releases it with no fine. A
--   character already in jail has a line of its own at the top, with its
--   release. Staff only: anyone else sees an empty gump.
--
-- Functions:
--   rows(g, player, args)  fills the slot; args.target is the serial of the
--                          character, args.name its name, args.days the days
--                          shown in the field
-- ==============================================================================

jail_sentence = {}

local per_page = 10
local row_height = 22

-- The frame is 460 wide and the slot starts at 20: texts are cut here instead of running over the edge.
local number_width = 60
local row_width = 250
local text_height = 20

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

local function open(player, args, days)
    gump.open(player, "jail_sentence", { target = args.target, name = args.name, days = days or args.days or "1" })
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
        open(who, args, "1")
        return
    end

    local result = jail.send(args.target, cell, days, who)

    if result == JailResultType.Ok then
        mobile.message(who, args.name .. " is in cell " .. cell .. " for " .. math.floor(days) .. " days.")
        return
    end

    mobile.message(who, refusal(result))
    open(who, args, typed)
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

    local top = 0
    local sentence = jail.sentence(args.target)

    -- The character is already in jail: its release comes first, the cells move it elsewhere.
    if sentence then
        g:button{ x = 0, y = 0, up = 4017, down = 4019, on_click = function(who)
            release(who, args, args.target, args.name)
        end }
        g:label_cropped{ x = 35, y = 0, width = row_width + number_width, height = text_height, hue = taken_hue,
            text = "In cell " .. sentence.cell .. ", " .. left(sentence.seconds_left) .. " left" }
        top = row_height
    end

    g:pager{ previous = { x = 0, y = top + per_page * row_height + 10 }, next = { x = 390, y = top + per_page * row_height + 10 } }

    for index, cell in ipairs(jail.cells()) do
        local y = top + g:paginate(index, per_page) * row_height

        g:label_cropped{ x = 35, y = y, width = number_width, height = text_height, text = "Cell " .. cell.number }

        if cell.prisoner and cell.prisoner ~= args.target then
            g:label_cropped{ x = 100, y = y, width = row_width, height = text_height, hue = taken_hue,
                text = cell.name .. " - " .. left(cell.seconds_left) .. " left" }
            g:button{ x = 360, y = y, up = 4017, down = 4019, on_click = function(who)
                release(who, args, cell.prisoner, cell.name)
            end }
        else
            g:button{ x = 0, y = y, up = 4005, down = 4007, on_click = function(who, response)
                send(who, response, args, cell.number)
            end }
            g:label_cropped{ x = 100, y = y, width = row_width, height = text_height, hue = free_hue,
                text = cell.prisoner and "here now" or "free" }
        end
    end
end
