-- ==============================================================================
-- Moongate - scripts/gumps/gmtools.lua
--
-- What it is for:
--   The script of the gump of the game master's tools (templates/gumps/gmtools.xml),
--   opened by .gmtools. The left column is a sidebar with one button for each tool
--   of the table `tools` below; the right column is the panel of the selected tool.
--   Adding a tool is one entry in `tools` and one panel function. Staff only: anyone
--   else sees an empty gump.
--
--   The weather tool shows the weather where the game master stands and forces none,
--   rain, snow or storm on that weather profile, as .weather does, until the next
--   game hour. The season tool shows the season where the game master stands and of
--   its map, and sets the season of the map until the restart, or gives it back its
--   own, as .season does. The time tool shows the game time and the moons where the
--   game master stands and the light, and gives every player the same light or goes
--   back to the time of day, as .globallight does. The events tool, for the
--   administrators only, lists the seasonal events with their dates, mode and
--   state, and sets one to auto, on or off, as .event does.
--
-- Functions:
--   tools(g, player, args)  fills the sidebar slot; args.tool is the selected tool
--   panel(g, player, args)  fills the panel slot with the panel of that tool
-- ==============================================================================

gmtools = {}

local row_height = 26

-- The frame is 560 wide: the sidebar starts at x = 20 and the panel at x = 190. The widths are those of their texts,
-- cut here instead of running over the divider or the edge.
local sidebar_width = 100
local panel_width = 330
local text_height = 20

local title_hue = 68
local text_hue = 1152

local function open(player, tool)
    gump.open(player, "gmtools", { tool = tool })
end

-- The kinds of weather, in the order of the buttons. Read when the gump opens, not when the script loads: the
-- enum is only there once the weather module is.
local function kinds()
    return {
        { kind = WeatherKindType.None, name = "none" },
        { kind = WeatherKindType.Rain, name = "rain" },
        { kind = WeatherKindType.Snow, name = "snow" },
        { kind = WeatherKindType.Storm, name = "storm" },
    }
end

local function kind_name(kind)
    for _, entry in ipairs(kinds()) do
        if entry.kind == kind then
            return entry.name
        end
    end

    return "unknown"
end

local function weather_panel(g, player)
    local profile = world.weather_profile(player)
    local sky = world.weather(player)

    if profile == nil or sky == nil then
        return
    end

    g:label_cropped{ x = 0, y = 0, width = panel_width, height = text_height, hue = title_hue, text = "Weather here: " .. profile }
    g:label_cropped{ x = 0, y = 22, width = panel_width, height = text_height,
        text = "Now: " .. kind_name(sky.kind) .. ", density " .. sky.density .. ", temperature " .. sky.temperature }
    g:label_cropped{ x = 0, y = 54, width = panel_width, height = text_height, text = "Force it until the next game hour:" }

    for index, entry in ipairs(kinds()) do
        local y = 80 + (index - 1) * row_height

        g:button{ x = 0, y = y, up = 4023, down = 4025, on_click = function(who)
            -- The rank may have gone while the gump was open.
            if not world.is_staff(who) then
                return
            end

            local where = world.weather_profile(who)

            if where ~= nil and world.set_weather(who, entry.kind) then
                mobile.message(who, "The weather of " .. where .. " is now " .. entry.name .. " until the next hour.")
            end

            open(who, "weather")
        end }
        g:label_cropped{ x = 35, y = y, width = panel_width - 35, height = text_height, text = entry.name }
    end
end

-- The seasons, in the order of the buttons. Read when the gump opens, as the weather kinds are.
local function seasons()
    return {
        { season = SeasonType.Spring, name = "spring" },
        { season = SeasonType.Summer, name = "summer" },
        { season = SeasonType.Fall, name = "fall" },
        { season = SeasonType.Winter, name = "winter" },
        { season = SeasonType.Desolation, name = "desolation" },
    }
end

local function season_name(season)
    for _, entry in ipairs(seasons()) do
        if entry.season == season then
            return entry.name
        end
    end

    return "unknown"
end

-- The season of the map the game master stands on, as the file or a script left it.
local function map_season(player)
    local where = mobile.location(player)

    return where and world.season(where.map)
end

local function season_panel(g, player)
    local here = world.season_here(player)
    local map = map_season(player)

    if here == nil or map == nil then
        return
    end

    g:label_cropped{ x = 0, y = 0, width = panel_width, height = text_height, hue = title_hue, text = "Season here: " .. season_name(here) }
    g:label_cropped{ x = 0, y = 22, width = panel_width, height = text_height, text = "Season of your map: " .. season_name(map) }
    g:label_cropped{ x = 0, y = 54, width = panel_width, height = text_height, text = "Set it until the restart:" }

    local rows = seasons()
    rows[#rows + 1] = { name = "auto" }

    for index, entry in ipairs(rows) do
        local y = 80 + (index - 1) * row_height

        g:button{ x = 0, y = y, up = 4023, down = 4025, on_click = function(who)
            -- The rank may have gone while the gump was open.
            if not world.is_staff(who) then
                return
            end

            if entry.season ~= nil then
                if world.set_season(who, entry.season) then
                    mobile.message(who, "The season of your map is now " .. entry.name .. ".")
                end
            elseif world.clear_season(who) then
                mobile.message(who, "The season of your map is back to its own: " .. season_name(map_season(who)) .. ".")
            end

            open(who, "season")
        end }
        g:label_cropped{ x = 35, y = y, width = panel_width - 35, height = text_height, text = entry.name }
    end
end

-- The names of the moon phases, as the time command writes them. Read when the gump opens, as the other enums are.
local function moon_name(phase)
    local names = {
        [MoonPhaseType.NewMoon] = "new moon",
        [MoonPhaseType.WaxingCrescent] = "waxing crescent",
        [MoonPhaseType.FirstQuarter] = "first quarter",
        [MoonPhaseType.WaxingGibbous] = "waxing gibbous",
        [MoonPhaseType.FullMoon] = "full moon",
        [MoonPhaseType.WaningGibbous] = "waning gibbous",
        [MoonPhaseType.LastQuarter] = "last quarter",
        [MoonPhaseType.WaningCrescent] = "waning crescent",
    }

    return names[phase] or "unknown"
end

-- The light levels of the buttons, from 0 (brightest) to 31 (darkest), and the one that goes back to the clock.
local light_levels = {
    { level = 0, name = "0 (brightest)" },
    { level = 12, name = "12" },
    { level = 26, name = "26" },
    { level = 31, name = "31 (darkest)" },
    { name = "auto (the time of day)" },
}

local function two_digits(number)
    return string.format("%02d", number)
end

local function time_panel(g, player)
    local where = mobile.location(player)
    local light = world.light_here(player)

    if where == nil or light == nil then
        return
    end

    local now = world.time(where.map, where.x)
    local global = world.global_light()

    g:label_cropped{ x = 0, y = 0, width = panel_width, height = text_height, hue = title_hue,
        text = "Game time here: " .. two_digits(now.hours) .. ":" .. two_digits(now.minutes) }
    g:label_cropped{ x = 0, y = 22, width = panel_width, height = text_height,
        text = "Moons: Trammel " .. moon_name(world.moon(MapType.Trammel, where.x)) .. ", Felucca " ..
            moon_name(world.moon(MapType.Felucca, where.x)) }
    g:label_cropped{ x = 0, y = 44, width = panel_width, height = text_height,
        text = "Light here: " .. light .. (global ~= nil and ", the same for every player" or ", following the time of day") }
    g:label_cropped{ x = 0, y = 76, width = panel_width, height = text_height, text = "Light of every player:" }

    for index, entry in ipairs(light_levels) do
        local y = 100 + (index - 1) * row_height

        g:button{ x = 0, y = y, up = 4023, down = 4025, on_click = function(who)
            -- The rank may have gone while the gump was open.
            if not world.is_staff(who) then
                return
            end

            if entry.level ~= nil then
                if world.set_global_light(entry.level) then
                    mobile.message(who, "The global light is now " .. entry.level .. ".")
                end
            elseif world.clear_global_light() then
                mobile.message(who, "The global light follows the time of day again.")
            end

            open(who, "time")
        end }
        g:label_cropped{ x = 35, y = y, width = panel_width - 35, height = text_height, text = entry.name }
    end
end

-- The events the panel lists, the height of each, and the modes of its buttons. The panel is 230 high: more events
-- than fit are left to the .event command.
local events_listed = 4
local event_height = 48
local event_modes = { "auto", "on", "off" }
local event_mode_width = 110

local function events_panel(g, player)
    local list = schedule.events()

    g:label_cropped{ x = 0, y = 0, width = panel_width, height = text_height, hue = title_hue, text = "Seasonal events" }

    if #list == 0 then
        g:label_cropped{ x = 0, y = 26, width = panel_width, height = text_height, text = "The schedule has no events." }

        return
    end

    for index, entry in ipairs(list) do
        local y = 26 + (index - 1) * event_height

        if index > events_listed then
            g:label_cropped{ x = 0, y = y, width = panel_width, height = text_height,
                text = (#list - events_listed) .. " more: use .event" }

            break
        end

        g:label_cropped{ x = 0, y = y, width = panel_width, height = text_height,
            text = entry.name .. " (" .. entry.from .. " to " .. entry.to .. "): " .. entry.mode .. ", " ..
                (entry.active and "on" or "off") }

        for slot, mode in ipairs(event_modes) do
            local x = (slot - 1) * event_mode_width

            g:button{ x = x, y = y + 22, up = 4023, down = 4025, on_click = function(who)
                -- The rank may have gone while the gump was open; .event is for the administrators.
                if not world.is_administrator(who) then
                    return
                end

                if schedule.set_event(entry.id, mode) then
                    mobile.message(who, entry.name .. " is now " .. mode .. ".")
                end

                open(who, "events")
            end }
            g:label_cropped{ x = x + 35, y = y + 22, width = event_mode_width - 40, height = text_height, text = mode }
        end
    end
end

-- The tools of the sidebar, in order; the first is the one shown when none is chosen. An admin tool is for the
-- administrators only, as the command it stands for.
local tools = {
    { id = "weather", title = "Weather", panel = weather_panel },
    { id = "season", title = "Season", panel = season_panel },
    { id = "time", title = "Time", panel = time_panel },
    { id = "events", title = "Events", panel = events_panel, admin = true },
}

-- The tools this player may use.
local function available(player)
    local list = {}

    for _, tool in ipairs(tools) do
        if not tool.admin or world.is_administrator(player) then
            list[#list + 1] = tool
        end
    end

    return list
end

local function selected(args, player)
    local list = available(player)

    for _, tool in ipairs(list) do
        if tool.id == args.tool then
            return tool
        end
    end

    return list[1]
end

function gmtools.tools(g, player, args)
    if not world.is_staff(player) then
        return
    end

    local current = selected(args, player)

    for index, tool in ipairs(available(player)) do
        local y = (index - 1) * row_height

        g:button{ x = 0, y = y, up = 4005, down = 4007, on_click = function(who)
            if world.is_staff(who) and (not tool.admin or world.is_administrator(who)) then
                open(who, tool.id)
            end
        end }
        g:label_cropped{ x = 35, y = y, width = sidebar_width, height = text_height,
            hue = tool == current and title_hue or text_hue, text = tool.title }
    end
end

function gmtools.panel(g, player, args)
    if not world.is_staff(player) then
        return
    end

    selected(args, player).panel(g, player)
end
