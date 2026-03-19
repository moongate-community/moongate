local help = {}

local GUMP_ID = 0xB900
local QUESTION_TEXT_GUMP_ID = 0xB901
local STUCK_TEXT_GUMP_ID = 0xB902
local BUG_TEXT_GUMP_ID = 0xB903
local ACCOUNT_TEXT_GUMP_ID = 0xB904
local SUGGESTION_TEXT_GUMP_ID = 0xB905
local OTHER_TEXT_GUMP_ID = 0xB906
local VERBAL_HARASSMENT_TEXT_GUMP_ID = 0xB907
local PHYSICAL_HARASSMENT_TEXT_GUMP_ID = 0xB908
local GUMP_X = 160
local GUMP_Y = 120
local TEXT_ENTRY_ID = 1
local SUBMIT_BUTTON_ID = 1

local CATEGORIES = {
    { button_id = 1, label = "Question", text_gump_id = QUESTION_TEXT_GUMP_ID },
    { button_id = 2, label = "Stuck", text_gump_id = STUCK_TEXT_GUMP_ID },
    { button_id = 3, label = "Bug", text_gump_id = BUG_TEXT_GUMP_ID },
    { button_id = 4, label = "Account", text_gump_id = ACCOUNT_TEXT_GUMP_ID },
    { button_id = 5, label = "Suggestion", text_gump_id = SUGGESTION_TEXT_GUMP_ID },
    { button_id = 6, label = "Other", text_gump_id = OTHER_TEXT_GUMP_ID },
    { button_id = 7, label = "VerbalHarassment", text_gump_id = VERBAL_HARASSMENT_TEXT_GUMP_ID },
    { button_id = 8, label = "PhysicalHarassment", text_gump_id = PHYSICAL_HARASSMENT_TEXT_GUMP_ID }
}

local function build_category_gump()
    local g = gump.create()
    g:resize_pic(0, 0, 9200, 360, 280)
    g:no_move()
    g:alpha_region(14, 14, 332, 252)
    g:text(24, 20, 1152, "Help Request")
    g:text(24, 54, 0, "Choose the category that best matches your ticket.")

    local current_y = 88

    for _, category in ipairs(CATEGORIES) do
        g:button(24, current_y, 4005, 4007, category.button_id)
        g:text(58, current_y - 2, 0, category.label)
        current_y = current_y + 24
    end

    return g
end

local function build_text_entry_gump(category_label)
    local g = gump.create()
    g:resize_pic(0, 0, 9200, 360, 220)
    g:no_move()
    g:alpha_region(14, 14, 332, 192)
    g:text(24, 20, 1152, "Help Request")
    g:text(24, 54, 0, "Category: " .. category_label)
    g:text(24, 78, 0, "Describe your issue and press Submit.")
    g:text_entry_limited(24, 112, 300, 20, 0, TEXT_ENTRY_ID, "", 200)
    g:button(24, 152, 4005, 4007, SUBMIT_BUTTON_ID)
    g:text(58, 150, 0, "Submit")
    return g
end

local function register_category_handler(category)
    gump.on(GUMP_ID, category.button_id, function(ctx)
        local g = build_text_entry_gump(category.label)
        return gump.send(ctx.session_id, g, ctx.character_id or 0, category.text_gump_id, GUMP_X, GUMP_Y)
    end)

    gump.on(category.text_gump_id, SUBMIT_BUTTON_ID, function(ctx)
        local text_entries = ctx.text_entries or {}
        local message = text_entries[TEXT_ENTRY_ID] or ""
        return help_tickets.submit(ctx.session_id, category.label, message)
    end)
end

for _, category in ipairs(CATEGORIES) do
    register_category_handler(category)
end

function help.open(session_id, character_id)
    if session_id == nil or character_id == nil then
        return false
    end

    return gump.send(session_id, build_category_gump(), character_id, GUMP_ID, GUMP_X, GUMP_Y)
end

return help
