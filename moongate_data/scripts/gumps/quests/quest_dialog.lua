local quests_module = quests

local quest_dialog = {}

local GUMP_ID = 0xB950
local GUMP_X = 160
local GUMP_Y = 120
local GUMP_WIDTH = 520
local MIN_GUMP_HEIGHT = 260
local ROW_HEIGHT = 46
local SECTION_GAP = 22
local HEADER_HUE = 1152
local TEXT_HUE = 0
local MUTED_HUE = 943
local ACCEPT_BUTTON_BASE = 1000
local COMPLETE_BUTTON_BASE = 2000

local function push(ui, entry)
    table.insert(ui, entry)
end

local function section_header(ui, y, title)
    push(ui, { type = "label", x = 24, y = y, hue = HEADER_HUE, text = title })

    return y + 24
end

local function render_empty_section(ui, y, message)
    push(ui, { type = "label", x = 24, y = y, hue = MUTED_HUE, text = message })

    return y + 24
end

local function render_available_quests(ui, available, accept_map, y)
    y = section_header(ui, y, "Available Quests")

    if #available == 0 then
        return render_empty_section(ui, y, "No quests are currently available.")
    end

    for index, quest in ipairs(available) do
        local button_id = ACCEPT_BUTTON_BASE + index - 1
        accept_map[button_id] = quest.quest_id

        push(ui, { type = "button", id = button_id, x = 24, y = y, normal_id = 4005, pressed_id = 4007, onclick = "on_accept" })
        push(ui, { type = "label_cropped", x = 58, y = y - 2, width = 416, height = 18, hue = TEXT_HUE, text = quest.name or quest.quest_id or "Quest" })
        push(ui, { type = "label_cropped", x = 58, y = y + 16, width = 416, height = 18, hue = MUTED_HUE, text = quest.description or "" })

        y = y + ROW_HEIGHT
    end

    return y
end

local function render_active_quests(ui, active, complete_map, y)
    y = section_header(ui, y, "Active Quests")

    if #active == 0 then
        return render_empty_section(ui, y, "No active quests for this NPC.")
    end

    for index, quest in ipairs(active) do
        local button_id = COMPLETE_BUTTON_BASE + index - 1
        local is_ready = quest.is_ready_to_turn_in == true

        if is_ready then
            complete_map[button_id] = quest.quest_id
            push(ui, { type = "button", id = button_id, x = 24, y = y, normal_id = 4005, pressed_id = 4007, onclick = "on_complete" })
            push(ui, { type = "label_cropped", x = 58, y = y - 2, width = 416, height = 18, hue = TEXT_HUE, text = quest.name or quest.quest_id or "Quest" })
            push(ui, { type = "label_cropped", x = 58, y = y + 16, width = 416, height = 18, hue = MUTED_HUE, text = quest.status_text or "Ready to turn in" })
        else
            push(ui, { type = "label_cropped", x = 24, y = y, width = 450, height = 18, hue = TEXT_HUE, text = quest.name or quest.quest_id or "Quest" })
            push(ui, { type = "label_cropped", x = 24, y = y + 16, width = 450, height = 18, hue = MUTED_HUE, text = quest.status_text or "In progress" })
        end

        y = y + ROW_HEIGHT
    end

    return y
end

local function build_layout(session_id, character_id, npc_serial)
    local available = quests_module.get_available(session_id, character_id, npc_serial) or {}
    local active = quests_module.get_active(session_id, character_id, npc_serial) or {}
    local accept_map = {}
    local complete_map = {}
    local layout = { ui = {}, handlers = {} }
    local ui = layout.ui

    push(ui, { type = "background", x = 0, y = 0, gump_id = 9200, width = GUMP_WIDTH, height = MIN_GUMP_HEIGHT })
    push(ui, { type = "alpha_region", x = 12, y = 12, width = GUMP_WIDTH - 24, height = MIN_GUMP_HEIGHT - 24 })
    push(ui, { type = "label", x = 24, y = 18, hue = HEADER_HUE, text = "Quest Journal" })
    push(ui, { type = "label_cropped", x = 24, y = 42, width = 460, height = 18, hue = MUTED_HUE, text = "Speak with the NPC to review available and active quests." })

    local current_y = 72
    current_y = render_available_quests(ui, available, accept_map, current_y)
    current_y = current_y + SECTION_GAP
    current_y = render_active_quests(ui, active, complete_map, current_y)

    local height = math.max(MIN_GUMP_HEIGHT, current_y + 24)
    ui[1].height = height
    ui[2].height = height - 24

    layout.handlers.on_accept = function(ctx)
        if ctx == nil or ctx.session_id == nil or ctx.character_id == nil or ctx.button_id == nil then
            return false
        end

        local button_id = tonumber(ctx.button_id) or 0
        local quest_id = accept_map[button_id]

        if quest_id == nil then
            return false
        end

        return quests_module.accept(ctx.session_id, ctx.character_id, npc_serial, quest_id)
    end

    layout.handlers.on_complete = function(ctx)
        if ctx == nil or ctx.session_id == nil or ctx.character_id == nil or ctx.button_id == nil then
            return false
        end

        local button_id = tonumber(ctx.button_id) or 0
        local quest_id = complete_map[button_id]

        if quest_id == nil then
            return false
        end

        return quests_module.complete(ctx.session_id, ctx.character_id, npc_serial, quest_id)
    end

    return layout, npc_serial or 0
end

function quest_dialog.open(session_id, character_id, npc_serial)
    if session_id == nil or character_id == nil or npc_serial == nil then
        return false
    end

    local layout, sender_serial = build_layout(session_id, character_id, npc_serial)

    return gump.send_layout(session_id, layout, sender_serial, GUMP_ID, GUMP_X, GUMP_Y)
end

return quest_dialog
