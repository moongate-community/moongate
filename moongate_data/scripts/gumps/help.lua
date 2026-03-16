local help = {}

local GUMP_ID = 0xB900
local GUMP_X = 160
local GUMP_Y = 120

function help.open(session_id, character_id)
    if session_id == nil or character_id == nil then
        return false
    end

    local g = gump.create()
    g:resize_pic(0, 0, 9200, 360, 220)
    g:no_move()
    g:alpha_region(14, 14, 332, 192)
    g:text(24, 20, 1152, "Help")
    g:text(24, 54, 0, "Welcome to Moongate.")
    g:text(24, 78, 0, "This Help button is now wired through packet 0x9B.")
    g:text(24, 102, 0, "The gump is scriptable from Lua and can evolve later.")
    g:text(24, 136, 0, "For now this is a lightweight custom help window.")
    g:text(24, 170, 0, "Future versions can add categories, staff paging, or links.")

    return gump.send(session_id, g, character_id, GUMP_ID, GUMP_X, GUMP_Y)
end

return help
