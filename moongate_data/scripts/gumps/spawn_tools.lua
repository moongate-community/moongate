local spawn_tools = {}

local GUMP_ID = 0xB220
local GUMP_X = 120
local GUMP_Y = 80

local BUTTON_BASE = 100

local COMMANDS = {
    { key = "doors", label = "Spawn Doors", command = "spawn_doors" },
    { key = "signs", label = "Spawn Signs", command = "spawn_signs" },
    { key = "decorations", label = "Spawn Decorations", command = "spawn_decorations" },
    { key = "spawners", label = "Create Spawners", command = "create_spawners" }
}

local function find_command(button_id)
    local index = button_id - BUTTON_BASE + 1
    if index < 1 or index > #COMMANDS then
        return nil
    end

    return COMMANDS[index]
end

local function add_frame(ui)
    table.insert(ui, { type = "resize_pic", x = 0, y = 0, gump_id = 9200, width = 460, height = 250 })
    table.insert(ui, { type = "checker_trans", x = 12, y = 12, width = 436, height = 226 })
    table.insert(ui, { type = "label", x = 24, y = 20, hue = 1152, text = "Spawn Tools" })
    table.insert(ui, {
        type = "label_cropped",
        x = 24,
        y = 44,
        width = 410,
        height = 20,
        hue = 0,
        text = "Execute world generation commands."
    })
end

local function add_commands(ui)
    local y = 76

    for i, entry in ipairs(COMMANDS) do
        local button_id = BUTTON_BASE + i - 1
        table.insert(ui, { type = "button", id = button_id, x = 24, y = y, normal_id = 4005, pressed_id = 4007, onclick = "on_click" })
        table.insert(ui, { type = "label", x = 54, y = y + 2, hue = 0, text = entry.label })
        table.insert(ui, { type = "label", x = 220, y = y + 2, hue = 1102, text = "." .. entry.command })
        y = y + 34
    end
end

local function build_layout(session_id, character_id)
    local sender_serial = tonumber(character_id) or 0
    if sender_serial <= 0 then
        sender_serial = tonumber(session_id) or 1
    end

    local layout = { ui = {}, handlers = {} }
    local ui = layout.ui

    add_frame(ui)
    add_commands(ui)

    layout.handlers.on_click = function(ctx)
        local button_id = tonumber(ctx.button_id) or 0
        local session = tonumber(ctx.session_id) or 0
        local character = tonumber(ctx.character_id) or 0

        local selected = find_command(button_id)
        if selected == nil then
            return
        end

        speech.send(session, "Executing ." .. selected.command .. " ...")
        local lines = command.execute(selected.command, 1)

        if lines ~= nil then
            for i, line in ipairs(lines) do
                if type(line) == "string" and line ~= "" then
                    speech.send(session, line)
                end
            end
        end

        spawn_tools.open(session, character)
    end

    return layout, sender_serial
end

function spawn_tools.open(session_id, character_id)
    local layout, sender_serial = build_layout(session_id, character_id)
    return gump.send_layout(session_id, layout, sender_serial, GUMP_ID, GUMP_X, GUMP_Y)
end

return spawn_tools
