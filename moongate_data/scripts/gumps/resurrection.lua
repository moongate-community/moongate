local resurrection_module = resurrection

local GUMP_ID = 0xB940
local ACCEPT_BUTTON_ID = 1
local DECLINE_BUTTON_ID = 2
local GUMP_X = 180
local GUMP_Y = 120

local resurrection_gump = {}

local function resolve_source_text(source_type)
    if source_type == "ankh" then
        return "The ankh offers to restore your life."
    end

    return "A healer offers to restore your life."
end

local function build_gump(source_type)
    local g = gump.create()
    g:resize_pic(0, 0, 9200, 360, 180)
    g:no_move()
    g:text(24, 20, 1152, "Resurrection")
    g:text(24, 56, 0, resolve_source_text(source_type))
    g:text(24, 78, 0, "Do you wish to return to the living world?")
    g:button(24, 122, 4005, 4007, ACCEPT_BUTTON_ID)
    g:text(58, 120, 0, "Accept")
    g:button(180, 122, 4005, 4007, DECLINE_BUTTON_ID)
    g:text(214, 120, 0, "Decline")

    return g
end

gump.on(GUMP_ID, ACCEPT_BUTTON_ID, function(ctx)
    if ctx == nil or ctx.session_id == nil then
        return false
    end

    return resurrection_module.accept(ctx.session_id)
end)

gump.on(GUMP_ID, DECLINE_BUTTON_ID, function(ctx)
    if ctx == nil or ctx.session_id == nil then
        return false
    end

    return resurrection_module.decline(ctx.session_id)
end)

function resurrection_gump.open(session_id, character_id, source_type)
    if session_id == nil or character_id == nil then
        return false
    end

    local sender = character_id or 0
    local normalized_source_type = source_type or "healer"

    return gump.send(session_id, build_gump(normalized_source_type), sender, GUMP_ID, GUMP_X, GUMP_Y)
end

return resurrection_gump
