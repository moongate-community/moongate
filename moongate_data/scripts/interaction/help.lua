local help = require("gumps.help")

function on_help_request(session_id, character_id)
    if session_id == nil or character_id == nil then
        return false
    end

    return help.open(session_id, character_id)
end
