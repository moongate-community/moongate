local quests_module = quests

local quest_journal = {}

local GUMP_ID = 0xB951
local GUMP_X = 160
local GUMP_Y = 120
local GUMP_WIDTH = 540
local MIN_GUMP_HEIGHT = 240
local HEADER_HUE = 1152
local TEXT_HUE = 0
local MUTED_HUE = 943
local COMPLETED_HUE = 67
local SECTION_GAP = 16
local QUEST_GAP = 18

local function push(ui, entry)
    table.insert(ui, entry)
end

local function render_empty_state(ui, y)
    push(ui, { type = "label", x = 24, y = y, hue = MUTED_HUE, text = "No active quests are currently tracked." })

    return y + 24
end

local function render_objectives(ui, objectives, y)
    if objectives == nil or #objectives == 0 then
        return y
    end

    push(ui, { type = "label", x = 40, y = y, hue = HEADER_HUE, text = "Objective Progress" })
    y = y + 22

    for _, objective in ipairs(objectives) do
        local hue = objective.is_completed == true and COMPLETED_HUE or TEXT_HUE
        local progress_text = objective.progress_text or "0 / 0"
        local objective_text = objective.objective_text or objective.objective_type or "Objective"
        local line = progress_text .. " - " .. objective_text

        push(ui, { type = "label_cropped", x = 40, y = y, width = 460, height = 18, hue = hue, text = line })
        y = y + 18
    end

    return y
end

local function render_quest(ui, quest, y)
    push(ui, { type = "label_cropped", x = 24, y = y, width = 460, height = 18, hue = TEXT_HUE, text = quest.name or quest.quest_id or "Quest" })
    push(ui, { type = "label_cropped", x = 24, y = y + 18, width = 460, height = 18, hue = MUTED_HUE, text = quest.description or "" })
    push(ui, { type = "label", x = 24, y = y + 36, hue = MUTED_HUE, text = quest.status_text or "In progress" })

    y = y + 58
    y = render_objectives(ui, quest.objectives or {}, y)

    return y + QUEST_GAP
end

local function build_layout(session_id, character_id)
    if not quests_module.open_journal(session_id, character_id) then
        return nil, 0
    end

    local journal = quests_module.get_journal(session_id, character_id) or {}
    local layout = { ui = {} }
    local ui = layout.ui

    push(ui, { type = "background", x = 0, y = 0, gump_id = 9200, width = GUMP_WIDTH, height = MIN_GUMP_HEIGHT })
    push(ui, { type = "alpha_region", x = 12, y = 12, width = GUMP_WIDTH - 24, height = MIN_GUMP_HEIGHT - 24 })
    push(ui, { type = "label", x = 24, y = 18, hue = HEADER_HUE, text = "Quest Journal" })
    push(ui, {
        type = "label_cropped",
        x = 24,
        y = 42,
        width = 480,
        height = 18,
        hue = MUTED_HUE,
        text = "Read-only quest progress for your active journal entries."
    })

    local current_y = 72

    if #journal == 0 then
        current_y = render_empty_state(ui, current_y)
    else
        for _, quest in ipairs(journal) do
            current_y = render_quest(ui, quest, current_y)
        end
    end

    local height = math.max(MIN_GUMP_HEIGHT, current_y + 24)
    ui[1].height = height
    ui[2].height = height - 24

    return layout, tonumber(character_id) or 0
end

function quest_journal.open(session_id, character_id)
    if session_id == nil or character_id == nil then
        return false
    end

    local layout, sender_serial = build_layout(session_id, character_id)

    if layout == nil then
        return false
    end

    return gump.send_layout(session_id, layout, sender_serial, GUMP_ID, GUMP_X, GUMP_Y)
end

return quest_journal
