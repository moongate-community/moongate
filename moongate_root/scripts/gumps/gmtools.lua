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
--   game hour.
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

-- The tools of the sidebar, in order; the first is the one shown when none is chosen.
local tools = {
    { id = "weather", title = "Weather", panel = weather_panel },
}

local function selected(args)
    for _, tool in ipairs(tools) do
        if tool.id == args.tool then
            return tool
        end
    end

    return tools[1]
end

function gmtools.tools(g, player, args)
    if not world.is_staff(player) then
        return
    end

    local current = selected(args)

    for index, tool in ipairs(tools) do
        local y = (index - 1) * row_height

        g:button{ x = 0, y = y, up = 4005, down = 4007, on_click = function(who)
            if world.is_staff(who) then
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

    selected(args).panel(g, player)
end
